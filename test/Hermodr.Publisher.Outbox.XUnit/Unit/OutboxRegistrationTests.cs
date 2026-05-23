//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Hermodr.Fakes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Tests that verify the DI registration helpers — <see cref="EventPublisherBuilderExtensions"/>
    /// and <see cref="OutboxChannelBuilder{TMessage}"/> — wire all required services correctly.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Outbox")]
    public static class OutboxRegistrationTests
    {
        // ── AddOutbox<TMessage>() — base overload ─────────────────────────────

        [Fact]
        public static void AddOutbox_RegistersOutboxChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();
            
            var provider = services.BuildServiceProvider();

            // The publisher must be resolvable — it internally holds the outbox channel.
            Assert.NotNull(provider.GetService<EventPublisher>());
        }

        [Fact]
        public static void AddOutbox_WithFactory_FactoryIsRegistered()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>();

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IOutboxMessageFactory<FakeOutboxMessage>>());
            Assert.IsType<FakeOutboxMessageFactory>(
                provider.GetService<IOutboxMessageFactory<FakeOutboxMessage>>());
        }

        [Fact]
        public static void AddOutbox_WithRepository_RepositoryIsRegistered()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithRepository<FakeOutboxMessageRepository>();

            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IOutboxMessageRepository<FakeOutboxMessage>>());
        }

        [Fact]
        public static void AddOutbox_WithRepository_ManagerIsRegistered()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithRepository<FakeOutboxMessageRepository>();

            var provider = services.BuildServiceProvider();

            // OutboxPublishChannel depends on OutboxMessageManager, so the manager
            // must be resolvable whenever the repository is registered.
            Assert.NotNull(provider.GetService<OutboxMessageManager<FakeOutboxMessage>>());
        }

        // ── AddOutbox<TMessage>(configure) overload ───────────────────────────

        [Fact]
        public static void AddOutbox_WithConfigureAction_OptionsAreApplied()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>(configure =>
                    {
                        // OutboxPublishOptions is currently empty; the test confirms
                        // the configure delegate is invoked without throwing.
                    })
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();
            
            var provider = services.BuildServiceProvider();
            var opts     = provider.GetRequiredService<IOptions<OutboxPublishOptions>>();

            Assert.NotNull(opts.Value);
        }

        // ── AddOutbox<TMessage>(sectionPath) overload ─────────────────────────

        [Fact]
        public static void AddOutbox_WithSectionPath_OptionsAreBound()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // OutboxPublishOptions has no custom properties yet, but binding
                    // should not throw.
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>("Events:Outbox")
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();
            
            var provider = services.BuildServiceProvider();
            var opts     = provider.GetRequiredService<IOptions<OutboxPublishOptions>>();

            Assert.NotNull(opts.Value);
        }

        // ── OutboxChannelBuilder.Configure(action) ────────────────────────────

        [Fact]
        public static void OutboxChannelBuilder_Configure_CanBeCalledFluently()
        {
            var services = new ServiceCollection();

            // Should not throw.
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .Configure(_ => { })
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();
            
            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<EventPublisher>());
        }

        // ── OutboxChannelBuilder.Configure(sectionPath) ───────────────────────

        [Fact]
        public static void OutboxChannelBuilder_Configure_SectionPath_CanBeCalledFluently()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);

            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .Configure("Events:Outbox")
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();
            
            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<EventPublisher>());
        }

        // ── OutboxChannelBuilder.Services ─────────────────────────────────────

        [Fact]
        public static void OutboxChannelBuilder_Services_ExposesUnderlyingServiceCollection()
        {
            var services = new ServiceCollection();
            var builder  = services.AddEventPublisher()
                                   .AddOutbox<FakeOutboxMessage>();

            Assert.Same(services, builder.Services);
        }
    }
}

