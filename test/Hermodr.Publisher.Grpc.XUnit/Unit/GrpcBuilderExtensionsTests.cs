//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
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
        public void AddGrpcEventPublisherChannel_Twice_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            builder.AddGrpcEventPublisherChannel(options =>
            {
                options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
            });

            // A second registration would add a second channel to the pipeline and
            // deliver every event multiple times; it must fail fast instead.
            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://other.example.com:5001" }];
                }));

            Assert.Contains("already been added", ex.Message);
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_TypedAfterUntyped_IsAllowed()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();

            builder.AddGrpcEventPublisherChannel(options =>
            {
                options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
            });

            // Typed channels are separate registrations and must not trip the
            // duplicate guard.
            builder.AddGrpcEventPublisherChannel<TypedEvent>();

            var sp = services.BuildServiceProvider();
            Assert.NotNull(sp.GetKeyedService<IEventPublishChannel>(""));
            Assert.NotNull(sp.GetKeyedService<IEventPublishChannel<TypedEvent>>(""));
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_DefaultHttpClient_HandlerLifetimeIsInfinite()
        {
            // The GrpcChannelFactory caches GrpcChannel instances (and the
            // HttpClient/handler they captured) for the application lifetime, so
            // the default client must not use a rotating handler.
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                });

            var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();

            Assert.Equal(
                Timeout.InfiniteTimeSpan,
                options.Get(GrpcDefaults.HttpClientName).HandlerLifetime);
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
