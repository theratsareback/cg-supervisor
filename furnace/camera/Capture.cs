namespace furnace.camera;

using System;
using OpenCvSharp;

public class Capture(string ipAddress, int port)
{
    private readonly string _rtspUrl = $"rtsp://{ipAddress}:{port}/cam";
    private Mat _latestFrame = new Mat();
    private Circle _bestFit = new Circle();
    private readonly object _frameLock = new object();
    private CancellationTokenSource? _cts;
    public event EventHandler? StreamInterrupted;
    public event EventHandler? StreamStarted;
    private bool _streamIsActive;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ProcessStream(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// use ONLY for debug. Frame copy is exceedingly slow
    /// </summary>
    public Mat? GetLatestFrame()
    {
        lock (_frameLock)
        {
            return _latestFrame?.Clone(); // Return a copy to avoid threading issues
        }
    }

    public Circle GetBestFit()
    {
        return _bestFit;
    }

    private void ProcessStream(CancellationToken token)
    {
        using var capture = new VideoCapture(_rtspUrl);

        if (!capture.IsOpened())
        {
            StreamInterrupted?.Invoke(this, EventArgs.Empty);
            return;
        }

        _streamIsActive = true;
        StreamStarted?.Invoke(this, EventArgs.Empty);

        //var frame = new Mat();

        while (!token.IsCancellationRequested)
        {
            try
            {
                var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty())
                {
                    if (_streamIsActive)
                    {
                        _streamIsActive = false;
                        StreamInterrupted?.Invoke(this, EventArgs.Empty);
                    }
                    Thread.Sleep(500);
                    continue;
                }

                lock (_frameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = ProcessFrame(frame);
                }

                if (!_streamIsActive)
                {
                    _streamIsActive = true;
                    StreamStarted?.Invoke(this, EventArgs.Empty);
                }

                Thread.Sleep(30); // Tune based on stream FPS
            }
            catch (Exception)
            {
                if (_streamIsActive)
                {
                    _streamIsActive = false;
                    StreamInterrupted?.Invoke(this, EventArgs.Empty);
                }
                Thread.Sleep(1000);
            }
        }

        capture.Release();
    }

    private Mat ProcessFrame(Mat input)
    {
        var circle = DetectCircle(input);
        if (circle != null)
        {
            _bestFit = circle;
        }
        else
        {
            _bestFit.Center = new Point2f(0, 0);
            _bestFit.Radius = 0;
            _bestFit.AccumulatorScore = 0;
            _bestFit.Timestamp = DateTime.UtcNow;
        }
        return input;
    }

    private Circle? DetectCircle(Mat input)
    {
        var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, gray, new Size(9, 9), 2, 2);

        var circles = Cv2.HoughCircles(
            gray,
            HoughModes.Gradient,
            dp: 1,
            minDist: 50,
            param1: 100,
            param2: 30,
            minRadius: 10,
            maxRadius: 100);

        if (circles.Length > 0)
        {
            var c = circles[0];
            return new Circle
            {
                Center = c.Center,
                Radius = c.Radius,
                Timestamp = DateTime.UtcNow // capture timestamp here
            };
        }
        return null;
    }

}