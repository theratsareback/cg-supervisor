using System.Diagnostics.Tracing;
using Grpc.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Channels;
using furnace.eurotherm;

namespace furnace.grpc;

public class StreamServiceImpl : StreamService.StreamServiceBase
{
    private readonly Coordinator _coord;
    public StreamServiceImpl(Coordinator coordinator) => _coord = coordinator;

    public override async Task Stream(
        IAsyncStreamReader<Frame> requestStream,
        IServerStreamWriter<Frame> responseStream,
        ServerCallContext context)
    {
        await foreach (var frame in requestStream.ReadAllAsync())
        {
            var response = new Frame
            {
                Seq = frame.Seq,
                Payload = "" // TODO stream responses
            };

            await responseStream.WriteAsync(response);
        }
    }
}

public class EventsServiceImpl : Events.EventsBase
{
    private readonly ILogger<EventsServiceImpl> _logger;
    private readonly Coordinator _coord;

    public EventsServiceImpl(Coordinator coordinator, ILogger<EventsServiceImpl> logger)
    {
        _coord = coordinator;
        _logger = logger;
    }

    public override Task<EventResponse> SendEvent(Event request, ServerCallContext context)
    {
        try{
        Console.WriteLine("Task Invoked");
        Console.WriteLine(request.Index);
        Console.WriteLine(request.Payload);

        _logger.LogInformation(
            "Received event: Type={Type}, Index={Index}, PayloadLength={Len}",
            request.Type, request.Index, request.Payload?.Length ?? 0);
        string payload = _coord.Handle(request);
        var response = new EventResponse
        {
            Index = request.Index,
            Payload = payload
        };

        return Task.FromResult(response);
        }
        catch (RpcException ex)
        {
            Console.WriteLine($"gRPC error: {ex.StatusCode} - {ex.Status.Detail}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Event loop crashed: {ex}");
            throw;
        }
        
    }
    
}