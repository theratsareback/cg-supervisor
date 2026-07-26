namespace furnace.camera;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Basler.Pylon;
using OpenCvSharp;


public readonly record struct FrameInfo(
    long Sequence,
    DateTimeOffset ReceivedAtUtc);

/// <summary>
/// Captures camera frames on one worker and processes them with OpenCV on another worker
/// </summary>
public sealed class BaslerCapture : IAsyncDisposable
{
    private readonly Camera _camera;
    private readonly PixelDataConverter _converter;
    private readonly Channel<CapturedFrame> _frameChannel;

    private readonly Func<Mat, FrameInfo, CancellationToken, ValueTask>
        _frameProcessor;

    private readonly object _stateLock = new();

    private CancellationTokenSource? _workerCancellation;
    private Task? _captureTask;
    private Task? _processingTask;
    private bool _started;
    private bool _disposed;

    public event EventHandler<Exception>? Faulted;
    public event EventHandler<string>? GrabFailed;

    public long CapturedFrames { get; private set; }
    public long ProcessedFrames { get; private set; }
    public long DroppedFrames { get; private set; }

    public BaslerCapture(
        ICameraInfo cameraInfo,
        Func<Mat, FrameInfo, CancellationToken, ValueTask> frameProcessor,
        int queueDepth = 2)
    {
        ArgumentNullException.ThrowIfNull(cameraInfo);
        ArgumentNullException.ThrowIfNull(frameProcessor);

        if (queueDepth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueDepth),
                "Queue depth must be at least one.");
        }

        _frameProcessor = frameProcessor;

        _camera = new Camera(cameraInfo);

        _camera.CameraOpened += Configuration.AcquireContinuous;

        _converter = new PixelDataConverter
        {
            OutputPixelFormat = PixelType.BGR8packed
        };

        _frameChannel = Channel.CreateBounded<CapturedFrame>(
            new BoundedChannelOptions(queueDepth)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = false
            });
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_stateLock)
        {
            if (_started)
            {
                throw new InvalidOperationException(
                    "The worker has already been started.");
            }

            _started = true;
            _workerCancellation = new CancellationTokenSource();
        }

        var cameraStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationToken workerToken = _workerCancellation.Token;

        _processingTask = Task.Run(
            () => ProcessingLoopAsync(workerToken),
            CancellationToken.None);

        _captureTask = Task.Run(
            () => CaptureLoop(cameraStarted, workerToken),
            CancellationToken.None);

        try
        {
            await cameraStarted.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task? captureTask;
        Task? processingTask;

        lock (_stateLock)
        {
            cancellation = _workerCancellation;
            captureTask = _captureTask;
            processingTask = _processingTask;
        }

        cancellation?.Cancel();

        await IgnoreExpectedCancellationAsync(captureTask)
            .ConfigureAwait(false);

        _frameChannel.Writer.TryComplete();

        await IgnoreExpectedCancellationAsync(processingTask)
            .ConfigureAwait(false);

        cancellation?.Dispose();

        lock (_stateLock)
        {
            _workerCancellation = null;
            _captureTask = null;
            _processingTask = null;
        }
    }

    private void CaptureLoop(
        TaskCompletionSource<bool> cameraStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            _camera.Open();

            _camera.Parameters[PLCameraInstance.MaxNumBuffer]
                .TrySetValue(10);

            _camera.StreamGrabber.Start(
                GrabStrategy.OneByOne,
                GrabLoop.ProvidedByUser);

            cameraStarted.TrySetResult(true);

            while (!cancellationToken.IsCancellationRequested &&
                   _camera.StreamGrabber.IsGrabbing)
            {
                using IGrabResult? grabResult =
                    _camera.StreamGrabber.RetrieveResult(
                        100,
                        TimeoutHandling.Return);

                if (grabResult is null)
                {
                    continue;
                }

                if (!grabResult.GrabSucceeded)
                {
                    ReportGrabFailure(
                        $"Grab failed: 0x{grabResult.ErrorCode:X8}: " +
                        grabResult.ErrorDescription);

                    continue;
                }

                CapturedFrame frame = ConvertToOpenCv(grabResult);

                CapturedFrames++;

                PublishLatestFrame(frame);
            }
        }
        catch (Exception exception)
        {
            cameraStarted.TrySetException(exception);

            if (!cancellationToken.IsCancellationRequested)
            {
                ReportFault(exception);
            }
        }
        finally
        {
            cameraStarted.TrySetCanceled();

            try
            {
                if (_camera.StreamGrabber.IsGrabbing)
                {
                    _camera.StreamGrabber.Stop();
                }
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ReportFault(exception);
                }
            }

            try
            {
                _camera.Close();
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ReportFault(exception);
                }
            }

            _frameChannel.Writer.TryComplete();
        }
    }

    private CapturedFrame ConvertToOpenCv(IGrabResult grabResult)
    {
        int width = grabResult.Width;
        int height = grabResult.Height;

        // BGR8packed contains three bytes per pixel.
        long destinationSize = checked((long)width * height * 3);

        var image = new Mat(
            rows: height,
            cols: width,
            type: MatType.CV_8UC3);

        try
        {
            _converter.Convert(
                image.Data,
                destinationSize,
                grabResult);

            long sequence = CapturedFrames + 1;

            return new CapturedFrame(
                image,
                new FrameInfo(
                    sequence,
                    DateTimeOffset.UtcNow));
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private void PublishLatestFrame(CapturedFrame newFrame)
    {
        if (_frameChannel.Writer.TryWrite(newFrame))
        {
            return;
        }

        if (_frameChannel.Reader.TryRead(out CapturedFrame? staleFrame))
        {
            staleFrame.Dispose();
            DroppedFrames++;
        }

        if (!_frameChannel.Writer.TryWrite(newFrame))
        {
            newFrame.Dispose();
            DroppedFrames++;
        }
    }

    private async Task ProcessingLoopAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                CapturedFrame frame in
                _frameChannel.Reader.ReadAllAsync(cancellationToken)
                    .ConfigureAwait(false))
            {
                using (frame)
                {
                    await _frameProcessor(
                            frame.Image,
                            frame.Info,
                            cancellationToken)
                        .ConfigureAwait(false);

                    ProcessedFrames++;
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) { }

        catch (Exception exception)
        {
            ReportFault(exception);
            _workerCancellation?.Cancel();
        }

        finally
        {
            while (_frameChannel.Reader.TryRead(
                       out CapturedFrame? remainingFrame))
            {
                remainingFrame.Dispose();
            }
        }
    }

    private void ReportFault(Exception exception)
    {
        try
        {
            Faulted?.Invoke(this, exception);
        }
        catch { }
    }

    private void ReportGrabFailure(string message)
    {
        try
        {
            GrabFailed?.Invoke(this, message);
        }
        catch { }
    }

    private static async Task IgnoreExpectedCancellationAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopAsync().ConfigureAwait(false);

        _converter.Dispose();
        _camera.Dispose();
    }

    private sealed class CapturedFrame : IDisposable
    {
        public CapturedFrame(Mat image, FrameInfo info)
        {
            Image = image;
            Info = info;
        }

        public Mat Image { get; }
        public FrameInfo Info { get; }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}