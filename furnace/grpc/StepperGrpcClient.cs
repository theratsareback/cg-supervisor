using System.Diagnostics.Tracing;
using Grpc.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Channels;
using furnace.eurotherm;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Grpc.Net.Client;

namespace furnace.grpc;

public sealed class StepperGrpcClient : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly StepperService.StepperServiceClient _stepperClient;

    private readonly CancellationTokenSource _cts = new();

    private AsyncServerStreamingCall<DataPacket>? _dataCall;
    private Task? _dataReceiveTask;

    private readonly Action<uint[], double[]> _onFrameReceived;

    /// <summary>
    /// Start a gRPC client connected to the stepper driver
    /// </summary>
    /// <param name="address">IP address string for the driver, formatted "http://a.b.c.d:port"</param>
    /// <param name="onFrameReceived">Callback to process a packet of data</param>
    /// <param name="options"></param>
    /// 
    /// public StreamServiceImpl(Coordinator coordinator) => _coord = coordinator;
    public StepperGrpcClient(string address, Action<uint[], double[]> onFrameReceived, GrpcChannelOptions? options = null)
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        _channel = GrpcChannel.ForAddress(address, options ?? new GrpcChannelOptions());
        _stepperClient = new StepperService.StepperServiceClient(_channel);
        _onFrameReceived = onFrameReceived;
    }

    /// <summary>
    /// Open the unidirectional stream and start receiving data.
    /// </summary>
    public void Start()
    {
        if (_dataCall != null) throw new InvalidOperationException("Client already started.");

        _dataCall = _stepperClient.StreamData(new StreamRequest(), cancellationToken: _cts.Token);
        _dataReceiveTask = ReceiveDataLoopAsync(_dataCall.ResponseStream, _cts.Token);
    }

    /// <summary>
    /// Receive data and process it
    /// </summary>
    private async Task ReceiveDataLoopAsync(IAsyncStreamReader<DataPacket> responseStream, CancellationToken ct)
    {
        try
        {
            while (await responseStream.MoveNext(ct).ConfigureAwait(false))
            {
                Console.WriteLine(responseStream.Current);
                _onFrameReceived(responseStream.Current.Timestamps.ToArray(), responseStream.Current.Values.ToArray());
            }
        }
        catch (OperationCanceledException) { }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
    }

    /// <summary>
    /// Set the parameters for a stepper motor.
    /// </summary>
    /// <param name="id">Integer ID for the target stepper</param>
    /// <param name="frequency">Frequency in Hz</param>
    /// <param name="direction"></param>
    public async void SetStepper(uint id, ulong frequency, bool direction)
    {
        StepperBuf _buf = new StepperBuf
        {
            StepperId = id,
            Frequency = frequency,
            Direction = direction
        };

        _ = _stepperClient.SetStepperAsync(_buf);
    }
    
    public async ValueTask DisposeAsync()
    {
        _dataReceiveTask.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _channel.Dispose();
    }
}