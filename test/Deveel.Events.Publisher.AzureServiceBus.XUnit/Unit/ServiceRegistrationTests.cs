using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Channel", "ServiceBus")]
    [Trait("Function", "Registration")]
    public static class ServiceRegistrationTests
    {
        // ── Helper ────────────────────────────────────────────────────────────

        private class SomeEvent { }

        // ── existing tests ────────────────────────────────────────────────────

        [Fact]
        public static void AddServiceBusPublishChannel_WasSuccessful()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddServiceBus(options =>
                {
                    options.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc12345";
                    options.QueueName = "my-queue";
                    options.ClientOptions.TransportType = ServiceBusTransportType.AmqpWebSockets;
                });

            var serviceProvider = services.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<EventPublisher>());
            // Channels are registered as keyed services under the publisher name (empty string = default).
            var channel = serviceProvider.GetKeyedService<IEventPublishChannel>(string.Empty);
            Assert.NotNull(channel);
            Assert.Equal("ServiceBusPublishChannel", channel!.GetType().Name);
            Assert.NotNull(serviceProvider.GetService<IServiceBusClientFactory>());

            var options = serviceProvider.GetService<IOptions<ServiceBusPublishOptions>>();
            Assert.NotNull(options);
            Assert.Equal("my-queue", options.Value.QueueName);
            Assert.Equal("Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc12345", options.Value.ConnectionString);
            Assert.NotNull(options.Value.ClientOptions);
            Assert.Equal(ServiceBusTransportType.AmqpWebSockets, options.Value.ClientOptions.TransportType);
        }

        [Fact]
        public static void AddServiceBus_WithSectionPath_BindsOptionsFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ServiceBus:ConnectionString"] = "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc12345",
                    ["ServiceBus:QueueName"] = "my-queue-from-config",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddServiceBus("ServiceBus");

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<EventPublisher>());
            // Channels are registered as keyed services under the publisher name (empty string = default).
            var channel = provider.GetKeyedService<IEventPublishChannel>(string.Empty);
            Assert.NotNull(channel);
            Assert.Equal("ServiceBusPublishChannel", channel!.GetType().Name);

            var options = provider.GetService<IOptions<ServiceBusPublishOptions>>();
            Assert.NotNull(options);
            Assert.Equal("my-queue-from-config", options!.Value.QueueName);
            Assert.Equal(
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc12345",
                options.Value.ConnectionString);
        }

        // ── Typed channel via configure action ────────────────────────────────

        [Fact]
        public static void AddServiceBus_Typed_WithConfigureAction_RegistersTypedChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddServiceBus(opts =>
                {
                    opts.ConnectionString =
                        "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base";
                    opts.QueueName = "base-queue";
                })
                .AddServiceBus<SomeEvent>(opts =>
                {
                    opts.QueueName = "some-event-queue";
                });

            var provider = services.BuildServiceProvider();

            // The typed channel must be resolvable as IEventPublishChannel<SomeEvent>
            // (registered as a keyed service under the publisher name)
            var typedChannel = provider.GetKeyedService<IEventPublishChannel<SomeEvent>>(string.Empty);
            Assert.NotNull(typedChannel);

            var typedOpts = provider.GetRequiredService<IOptions<ServiceBusPublishOptions<SomeEvent>>>();
            Assert.Equal("some-event-queue", typedOpts.Value.QueueName);
        }

        // ── Typed channel via config section ──────────────────────────────────

        [Fact]
        public static void AddServiceBus_Typed_WithSectionPath_BindsTypedOptionsFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ServiceBus:ConnectionString"] =
                        "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base",
                    ["ServiceBus:QueueName"] = "base-queue",
                    ["ServiceBus:SomeEvent:ConnectionString"] =
                        "Endpoint=sb://typed.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=typed",
                    ["ServiceBus:SomeEvent:QueueName"] = "typed-queue-from-config",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddServiceBus("ServiceBus")
                .AddServiceBus<SomeEvent>("ServiceBus:SomeEvent");

            var provider = services.BuildServiceProvider();

            var typedOpts = provider.GetRequiredService<IOptions<ServiceBusPublishOptions<SomeEvent>>>();
            Assert.Equal("typed-queue-from-config", typedOpts.Value.QueueName);
            Assert.Contains("typed.servicebus.windows.net", typedOpts.Value.ConnectionString);
        }

        // ── ConfigureIdentifier post-configure ────────────────────────────────

        [Fact]
        public static void AddServiceBus_WithPublisherSource_SetsClientOptionsIdentifier()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(pub =>
            {
                pub.Source = new Uri("https://api.example.com/my-service");
            })
            .AddServiceBus(opts =>
            {
                opts.ConnectionString =
                    "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=abc";
                opts.QueueName = "q";
                // Leave ClientOptions.Identifier empty → should be filled by post-configure
            });

            var provider = services.BuildServiceProvider();
            var opts = provider.GetRequiredService<IOptions<ServiceBusPublishOptions>>();

            Assert.Equal("https://api.example.com/my-service", opts.Value.ClientOptions.Identifier);
        }

        [Fact]
        public static void AddServiceBus_WithExplicitIdentifier_DoesNotOverrideIdentifier()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(pub =>
            {
                pub.Source = new Uri("https://api.example.com/my-service");
            })
            .AddServiceBus(opts =>
            {
                opts.ConnectionString =
                    "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=abc";
                opts.QueueName = "q";
                opts.ClientOptions.Identifier = "my-explicit-id";
            });

            var provider = services.BuildServiceProvider();
            var opts = provider.GetRequiredService<IOptions<ServiceBusPublishOptions>>();

            // Explicit identifier must be preserved
            Assert.Equal("my-explicit-id", opts.Value.ClientOptions.Identifier);
        }

        // ── Infrastructure singletons ─────────────────────────────────────────

        [Fact]
        public static void AddServiceBus_RegistersClientFactorySingleton()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddServiceBus(opts =>
                {
                    opts.ConnectionString =
                        "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=abc";
                    opts.QueueName = "q";
                });

            var provider = services.BuildServiceProvider();

            var factory1 = provider.GetRequiredService<IServiceBusClientFactory>();
            var factory2 = provider.GetRequiredService<IServiceBusClientFactory>();

            Assert.Same(factory1, factory2);
        }

        [Fact]
        public static void AddServiceBus_RegistersMessageFactorySingleton()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddServiceBus(opts =>
                {
                    opts.ConnectionString =
                        "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=abc";
                    opts.QueueName = "q";
                });

            var provider = services.BuildServiceProvider();

            var mf1 = provider.GetRequiredService<ServiceBusMessageFactory>();
            var mf2 = provider.GetRequiredService<ServiceBusMessageFactory>();

            Assert.Same(mf1, mf2);
        }
    }
}
