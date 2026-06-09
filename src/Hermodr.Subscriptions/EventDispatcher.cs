//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    internal class EventDispatcher : IEventMiddleware
    {
        private readonly SubscriptionTelemetry _telemetry = new();
        private readonly IReadOnlyList<IEventSubscriptionResolver> _resolvers;
        private readonly EventDispatcherOptions _options;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new <see cref="EventDispatcher"/> backed by multiple resolvers.
        /// Constructor dependencies are resolved from the DI container by
        /// <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>.
        /// </summary>
        public EventDispatcher(
            IEnumerable<IEventSubscriptionResolver> resolvers,
            IOptions<EventDispatcherOptions> options,
            ILogger<EventDispatcher>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(resolvers);
            _resolvers = resolvers.ToList();
            _options = options?.Value ?? new EventDispatcherOptions();
            _logger = logger ?? NullLogger<EventDispatcher>.Instance;
        }

        private async Task DispatchWithServicesAsync(
            CloudEvent @event,
            IServiceProvider? services,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var context = services is not null ? new EventSubscriptionContext(services) : null;

            // Fan-out resolver queries in parallel for lower latency.
            var resolveTasks = _resolvers
                .Select(r => r.ResolveSubscriptionsAsync(@event, context, cancellationToken))
                .ToList();

            var resolved = await Task.WhenAll(resolveTasks);
            var subscriptions = resolved.SelectMany(s => s).ToList();

            if (subscriptions.Count == 0)
            {
                _logger.LogNoMatchingSubscriptions(@event.Type);
                return;
            }

            _telemetry.RecordMatched(subscriptions.Count, @event.Type ?? "unknown");

            foreach (var subscription in subscriptions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = subscription.Name ?? "<anonymous>";
                _logger.LogDispatching(@event.Type, name);

                var sw = Stopwatch.StartNew();

                try
                {
                    await subscription.HandleAsync(@event, cancellationToken);
                    _logger.LogDispatched(@event.Type, name);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogSubscriptionHandlerError(ex, name, @event.Type);
                    _telemetry.AddError(@event.Type ?? "unknown", name);

                    if (_options.ThrowOnHandlerError)
                        throw;
                }
                finally
                {
                    sw.Stop();
                    _telemetry.RecordHandleDuration(sw.Elapsed.TotalSeconds, @event.Type ?? "unknown", name);
                }
            }
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            await DispatchWithServicesAsync(context.Event, context.Services, context.CancellationToken);
            await next(context);
        }
    }
}
