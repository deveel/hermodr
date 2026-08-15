//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Provides a base class for channels that publish events to a specific transport.
    /// </summary>
    /// <typeparam name="TOptions">
    /// The type of the channel-specific options that control per-delivery behaviour.
    /// </typeparam>
    public abstract class EventPublishChannel<TOptions> : INamedEventPublishChannel
        where TOptions : EventPublishOptions
    {
        private readonly ChannelTelemetry _telemetry = new();
        private readonly TOptions _defaultOptions;

        /// <summary>
        /// Gets the service provider for lazy resolution of dependencies.
        /// </summary>
        protected IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Initialises the channel with its channel-level defaults and optional extra
        /// validators.
        /// </summary>
        /// <param name="defaultOptions">
        /// The channel-level default options used when no per-call overrides are supplied.
        /// Must not be <c>null</c>.
        /// </param>
        /// <param name="serviceProvider">
        /// The service provider used to resolve <see cref="IValidateOptions{TOptions}"/>
        /// services lazily.
        /// </param>
        protected EventPublishChannel(
            TOptions defaultOptions,
            IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(defaultOptions);
            _defaultOptions = defaultOptions;
            ServiceProvider = serviceProvider;
        }

        private IEnumerable<IValidateOptions<TOptions>> Validators =>
            ServiceProvider.GetServices<IValidateOptions<TOptions>>();

        /// <inheritdoc cref="INamedEventPublishChannel.Name"/>
        /// <remarks>
        /// The value is read from <see cref="DefaultOptions"/> when those options implement
        /// <see cref="INamedChannelFilter"/>; otherwise <c>null</c>.
        /// </remarks>
        public string? Name => (_defaultOptions as INamedChannelFilter)?.ChannelName;

        /// <summary>Gets the channel-level default options.</summary>
        protected TOptions DefaultOptions => _defaultOptions;

        /// <summary>
        /// Merges the channel-level <paramref name="defaults"/> with the per-call
        /// <paramref name="perCallOptions"/>.
        /// </summary>
        /// <remarks>
        /// The base implementation performs a coarse merge: it returns
        /// <paramref name="perCallOptions"/> when non-<c>null</c>, otherwise
        /// <paramref name="defaults"/>. Override in a derived class to implement
        /// fine-grained, property-level merging.
        /// </remarks>
        protected virtual TOptions MergeOptions(TOptions defaults, TOptions? perCallOptions)
            => perCallOptions ?? defaults;

        /// <summary>
        /// Validates <paramref name="options"/> using any
        /// <see cref="IValidateOptions{TOptions}"/> registered in the DI container.
        /// When no validators are registered the method falls back to
        /// <see cref="Validator.ValidateObject(object, ValidationContext, bool)"/> (DataAnnotations).
        /// </summary>
        /// <param name="options">The effective options to validate.</param>
        /// <exception cref="OptionsValidationException">
        /// Thrown when an <see cref="IValidateOptions{TOptions}"/> reports one or more
        /// failures.
        /// </exception>
        /// <exception cref="ValidationException">
        /// Thrown when the DataAnnotations fallback path reports a failure.
        /// </exception>
        protected virtual void ValidateOptions(TOptions options)
        {
            if (!Validators.Any())
            {
                // No IValidateOptions<T> registered – fall back to DataAnnotations.
                var ctx = new ValidationContext(options);
                Validator.ValidateObject(options, ctx, validateAllProperties: true);
                return;
            }

            var failures = new List<string>();
            foreach (var validator in Validators)
            {
                var result = validator.Validate(null, options);
                if (result.Failed)
                    failures.AddRange(result.Failures ?? []);
            }

            if (failures.Count > 0)
                throw new OptionsValidationException(typeof(TOptions).Name, typeof(TOptions), failures);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// When <paramref name="options"/> is non-<c>null</c> it must be castable to
        /// <typeparamref name="TOptions"/>; passing an options object of an incompatible
        /// type throws <see cref="ArgumentException"/>.
        /// </remarks>
        Task IEventPublishChannel.PublishAsync(CloudEvent @event, EventPublishOptions? options,
            CancellationToken cancellationToken)
        {
            if (options != null && options is not TOptions)
                throw new ArgumentException($"Per-call options must be of type {typeof(TOptions).Name} or null.", nameof(options));

            return PublishAsync(@event, options as TOptions, cancellationToken);
        }

        /// <summary>
        /// Merges <paramref name="options"/> with the channel-level defaults, validates the
        /// result, and then delegates to <see cref="PublishCoreAsync"/>.
        /// </summary>
        /// <param name="event">The event to deliver.</param>
        /// <param name="options">
        /// Per-delivery overrides; pass <c>null</c> to use the channel defaults.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <exception cref="OptionsValidationException">
        /// Thrown when the effective (merged) options fail
        /// <see cref="IValidateOptions{TOptions}"/> validation.
        /// </exception>
        /// <exception cref="ValidationException">
        /// Thrown when the effective options fail the DataAnnotations fallback validation.
        /// </exception>
        public async Task PublishAsync(
            CloudEvent @event,
            TOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);
            var effectiveOptions = MergeOptions(_defaultOptions, options);
            ValidateOptions(effectiveOptions);

            var sw = Stopwatch.StartNew();
            try
            {
                await PublishCoreAsync(@event, effectiveOptions, cancellationToken);
            }
            finally
            {
                sw.Stop();
                _telemetry.RecordChannelPublishDuration(sw.Elapsed.TotalSeconds, @event.Type ?? "unknown", GetType().Name);
            }
        }

        /// <summary>
        /// Performs the actual channel-specific delivery of <paramref name="event"/> with
        /// the already-merged-and-validated <paramref name="options"/>.
        /// </summary>
        /// <param name="event">The event to deliver.</param>
        /// <param name="options">
        /// The effective options produced by <see cref="MergeOptions"/> and successfully
        /// validated by <see cref="ValidateOptions"/>.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        protected abstract Task PublishCoreAsync(
            CloudEvent @event,
            TOptions options,
            CancellationToken cancellationToken);
    }
}

