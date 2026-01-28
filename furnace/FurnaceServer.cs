using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using furnace.grpc;

namespace furnace;

public sealed class FurnaceServer : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri? Address { get; private set; }

    public async Task StartAsync(int port = 5000, CancellationToken ct = default)
    {
        if (_app != null) throw new InvalidOperationException("Server already started.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FurnaceServer).Assembly.FullName
        });

        builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();

        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(port, listen => listen.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddLogging();
        
        builder.Services.AddGrpc();

        builder.Services.AddSingleton<Coordinator>();
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<Coordinator>());



        var app = builder.Build();

        app.MapGrpcService<StreamServiceImpl>();
        app.MapGrpcService<EventsServiceImpl>();
        app.MapGet("/", () => "gRPC server is running.");

        _app = app;

        await app.StartAsync(ct);

        Address = new Uri($"http://localhost:{port}");
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app == null) return;

        _app.Lifetime.StopApplication();

        await _app.StopAsync(ct);
        await _app.DisposeAsync();
        _app = null;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private sealed class NoopHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

