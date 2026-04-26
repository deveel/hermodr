//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Options;

using System.ComponentModel.DataAnnotations;

namespace Deveel.Events
{
    /// <summary>
    /// Provides a base implementation of <see cref="IEventPublishChannel"/> that
    /// merges per-call <see cref="EventPublishOptions"/> overrides with the
    /// channel-level defaults, validates the effective options, and then delegates to
    /// the concrete channel delivery logic.
    /// </summary>
    /// <typeparam name="TOptions">
    /// The concrete <see cref="EventPublishOptions"/> subtype that carries the
    /// channel-level defaults and any per-call overrides.
    /// When a call-specific instance is not supplied the channel-level defaults are
    /// used as-is; when one is supplied the two are merged via <see cref="MergeOptions"/>
    /// and then validated via <see cref="ValidateOptions"/>.
    /// </typeparam>
    /// <remarks>
    /// Validation is performed in two steps:
    /// <list type="number">
    ///   <item>
    ///     If one or more <see cref="IValidateOptions{TOptions}"/> services were registered
    ///     in the DI container and injected through the constructor those are called in
    ///     order.  Any failure messages are collected and surfaced as a single
    ///     <see cref="OptionsValidationException"/>.
    ///   </item>
    ///   <item>
    ///     When no <see cref="IValidateOptions{TOptions}"/> are present the method falls
    ///     back to <see cref="Validator.ValidateObject"/> (DataAnnotations), which honours
    ///     attributes such as <c>[Required]</c>, <c>[Range]</c>, <c>[Url]</c>, etc.
    ///   </item>
    /// </list>
    /// </remarks>
    public abstract class EventPublishChannelBase<TOptions> : IEventPublishChannel
        where TOptions : EventPublishOptions
    {
        private readonly TOptions _defaultOptions;
        private readonly IEnumerable<IValidateOptions<TOptions>> _validators;

        /// <summary>
        /// Initialises the channel with its channel-level defaults and optional extra
        /// validators.
        /// </summary>
        /// <param name="defaultOptions">
        /// The channel-level default options used when no per-call overrides are supplied.
        /// Must not be <c>null</c>.
        /// </param>
        /// <param name="validators">
        /// An optional collection of <see cref="IValidateOptions{TOptions}"/> services
        /// registered in the DI container.  When the collection is empty or <c>null</c>
        /// validation falls back to
        /// <see cref="Validator.ValidateObject(object, ValidationContext)"/> (DataAnnotations).
        /// </param>
        protected EventPublishChannelBase(
            TOptions defaultOptions,
            IEnumerable<IValidateOptions<TOptions>>? validators = null)
        {
            ArgumentNullException.ThrowIfNull(defaultOptions);
            _defaultOptions = defaultOptions;
            _validators = validators ?? [];
        }

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
        /// <see cref="Validator.ValidateObject"/> (DataAnnotations).
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
            if (!_validators.Any())
            {
                // No IValidateOptions<T> registered – fall back to DataAnnotations.
                var ctx = new ValidationContext(options);
                Validator.ValidateObject(options, ctx, validateAllProperties: true);
                return;
            }

            var failures = new List<string>();
            foreach (var validator in _validators)
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
        Task IEventPublishChannel.PublishAsync(CloudEvent @event, EventPublishOptions? options = null,
            CancellationToken cancellationToken = default)
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
            await PublishCoreAsync(@event, effectiveOptions, cancellationToken);
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

