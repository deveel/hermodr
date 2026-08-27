//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    public class TypedGrpcChannelTests
    {
        public class OrderPlaced { public string OrderId { get; set; } = ""; }
        public class PersonCreated { public string Name { get; set; } = ""; }

        [Fact]
        public void AddGrpcEventPublisherChannel_Typed_RegistersTypedChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventPublisherChannel<OrderPlaced>()
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();

            var typedChannel = sp.GetKeyedService<IEventPublishChannel<OrderPlaced>>("");
            Assert.NotNull(typedChannel);
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_Typed_DoesNotReceiveOtherEventTypes()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel<OrderPlaced>()
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();
            var personChannel = sp.GetKeyedService<IEventPublishChannel<PersonCreated>>("");
            Assert.Null(personChannel);
        }

        [Fact]
        public void TypedChannel_MergesBaseAndTypedOptions()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://base.example.com:5001" }];
                })
                .AddGrpcEventPublisherChannel<OrderPlaced>(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://typed.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();

            // The typed channel should use the typed endpoints (override)
            var publisher = (EventPublisher)sp.GetRequiredService<IEventPublisher>();
            var metadata = publisher.GetChannelMetadata();

            Assert.Contains(metadata, m =>
                m.Transport == EventTransports.Grpc &&
                m.Properties.TryGetValue("address", out var addr) &&
                addr == "https://typed.example.com:5001");
        }
    }
}
