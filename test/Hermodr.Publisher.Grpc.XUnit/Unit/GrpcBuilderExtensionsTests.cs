//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    public class GrpcBuilderExtensionsTests
    {
        [Fact]
        public void AddGrpcEventPublisherChannel_NullConfigure_Throws()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            Assert.Throws<ArgumentNullException>(() =>
                builder.AddGrpcEventPublisherChannel((Action<GrpcPublishOptions>)null!));
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_EmptySectionPath_Throws()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            Assert.Throws<ArgumentException>(() =>
                builder.AddGrpcEventPublisherChannel(""));
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_SectionPath_BindsOptions()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Grpc:Endpoints:0:Address"] = "https://cfg-svc.example.com:5001",
                    ["Grpc:Endpoints:0:HttpClientName"] = "cfg-client",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel("Grpc")
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<IOptions<GrpcPublishOptions>>().Value;

            Assert.Single(options.Endpoints);
            Assert.Equal("https://cfg-svc.example.com:5001", options.Endpoints[0].Address);
            Assert.Equal("cfg-client", options.Endpoints[0].HttpClientName);
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_Typed_SectionPath_BindsOptions()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GrpcTyped:Endpoints:0:Address"] = "https://typed-cfg.example.com:5001",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel<TypedEvent>("GrpcTyped")
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<IOptions<GrpcPublishOptions<TypedEvent>>>().Value;

            Assert.Single(options.Endpoints);
            Assert.Equal("https://typed-cfg.example.com:5001", options.Endpoints[0].Address);
        }

        [Fact]
        public void AddGrpcEventSender_Named_EmptyName_Throws()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            Assert.Throws<ArgumentException>(() =>
                builder.AddGrpcEventSender<TestGrpc.FakeSender>(""));
        }

        [Fact]
        public void ConfigureGrpcChannel_EmptyClientName_Throws()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            Assert.Throws<ArgumentException>(() =>
                builder.ConfigureGrpcChannel("", b => { }));
        }

        [Fact]
        public void ConfigureGrpcChannel_NullConfigure_Throws()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            Assert.Throws<ArgumentNullException>(() =>
                builder.ConfigureGrpcChannel("test-client", null!));
        }

        public class TypedEvent { public string Id { get; set; } = ""; }
    }
}
