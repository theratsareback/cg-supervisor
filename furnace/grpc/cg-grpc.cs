namespace furnace.grpc;
using System.Diagnostics.Tracing;
using Grpc.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class StreamServiceImpl : StreamService.StreamServiceBase
{
    public override async Task Stream(
        IAsyncStreamReader<Frame> requestStream,
        IServerStreamWriter<Frame> responseStream,
        ServerCallContext context)
    {
        await foreach (var frame in requestStream.ReadAllAsync())
        {
            // Echo back (or modify)
            var response = new Frame
            {
                Seq = frame.Seq,
                Payload = $"Server received: {frame.Payload}"
            };

            await responseStream.WriteAsync(response);
        }
    }
}

public class EventsServiceImpl : Events.EventsBase
{
    private readonly ILogger<EventsServiceImpl> _logger;

    public EventsServiceImpl(ILogger<EventsServiceImpl> logger)
    {
        _logger = logger;
    }

    public override Task<EventResponse> SendEvent(Event request, ServerCallContext context)
    {
        // Basic logging / inspection
        _logger.LogInformation(
            "Received event: Type={Type}, Index={Index}, PayloadLength={Len}",
            request.Type, request.Index, request.Payload?.Length ?? 0);

        // Example "router" based on EventType
        // var responsePayload = request.Type switch
        // {
        //     // EventType.NewFurnace        => HandleNewFurnace(request),
        //     // EventType.RemoveFurnace     => HandleRemoveFurnace(request),
        //     // EventType.ModifyFurnace     => HandleModifyFurnace(request),

        //     // EventType.NewProfile        => HandleNewProfile(request),
        //     // EventType.RemoveProfile     => HandleRemoveProfile(request),
        //     // EventType.ModifyProfile     => HandleModifyProfile(request),

        //     // EventType.RequestProfiles   => HandleRequestProfiles(request),
        //     // EventType.RequestFurnaces   => HandleRequestFurnaces(request),

        //     // EventType.SetFurnaceProfile => HandleSetFurnaceProfile(request),
        //     // EventType.AckFurnaceAlarm   => HandleAckFurnaceAlarm(request),

        //     _ => throw new RpcException(
        //             new Status(StatusCode.InvalidArgument, $"Unknown event type: {request.Type}"))
        // };

        var response = new EventResponse
        {
            Index = request.Index,           // common pattern: echo correlation index
            //Payload = responsePayload ?? ""  // never return null strings
        };

        return Task.FromResult(response);
    }
}