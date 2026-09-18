using Microsoft.AspNetCore.Server.Kestrel.Core;

using OrderService.GrpcServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

// gRPC requires HTTP/2: expose a dedicated HTTPS (dev-certificate) endpoint.
// For an unencrypted HTTP/2 (h2c) endpoint, use `listenOptions.Protocols =
// HttpProtocols.Http2` without UseHttps() and configure the publisher's
// HttpClient accordingly (see the gRPC channel documentation).
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5095, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

var app = builder.Build();

app.MapGrpcService<OrderEventsGrpcService>();

app.MapGet("/", () => "OrderService gRPC receiver. Publish events with a gRPC client.");

app.Run();
