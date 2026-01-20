using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace furnace.grpc;

public sealed class FurnaceGrpcServer : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri? Address { get; private set; }

    public async Task StartAsync(int port = 5000, CancellationToken ct = default)
    {
        if (_app != null) throw new InvalidOperationException("Server already started.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FurnaceGrpcServer).Assembly.FullName
        });

        // If you want plaintext HTTP/2 (h2c), configure Kestrel:
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(port, listen => listen.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddLogging();
        builder.Services.AddGrpc();

        // Register your services’ dependencies (if any)
        // e.g. builder.Services.AddSingleton<Something>();

        var app = builder.Build();

        app.MapGrpcService<StreamServiceImpl>();
        app.MapGrpcService<EventsServiceImpl>();

        // Optional: basic health/info endpoint
        app.MapGet("/", () => "gRPC server is running.");

        _app = app;

        await app.StartAsync(ct);

        // Kestrel can bind multiple addresses; simplest:
        Address = new Uri($"http://localhost:{port}");
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app == null) return;
        await _app.StopAsync(ct);
        await _app.DisposeAsync();
        _app = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
