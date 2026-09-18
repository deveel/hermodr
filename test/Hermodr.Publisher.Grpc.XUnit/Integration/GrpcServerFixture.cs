//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermodr;

/// <summary>
/// Hosts a real gRPC server (Grpc.AspNetCore) inside
/// <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> so the publisher
/// channel under test drives an actual HTTP/2 gRPC call — no Docker or network
/// listener required.
/// </summary>
public sealed class GrpcServerFixture : IDisposable
{
    private readonly WebApplication _app;

    public GrpcServerFixture()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<TestOrderEventsService>();
        builder.WebHost.UseTestServer();

        _app = builder.Build();
        _app.MapGrpcService<TestOrderEventsService>();
        _app.Start();
    }

    /// <summary>Gets the test server hosting the gRPC service.</summary>
    public TestServer Server => _app.GetTestServer();

    /// <summary>Gets the singleton service implementation recording received events.</summary>
    public TestOrderEventsService Service =>
        _app.Services.GetRequiredService<TestOrderEventsService>();

    public void Dispose()
        => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
