//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;

using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Deserialises the event body to <typeparamref name="T"/> using a content-type-driven
/// <see cref="EventDataDeserializerProvider"/> and applies a predicate.
/// </summary>
/// <typeparam name="T">Target CLR type (must be a reference type).</typeparam>
/// <remarks>
/// <para>
/// Deserialization is delegated to an <see cref="EventDataDeserializerProvider"/> resolved
/// in this order:
/// </para>
/// <list type="number">
///   <item><description>
///     Fast-path — when <c>event.Data</c> is already an instance of <typeparamref name="T"/>
///     the provider is bypassed entirely.
///   </description></item>
///   <item><description>
///     A provider explicitly supplied at construction time (design-time scenarios).
///   </description></item>
///   <item><description>
///     A <see cref="EventDataDeserializerProvider"/> resolved from the DI
///     <see cref="IServiceProvider"/> passed to
///     <see cref="Matches(CloudEvent, IServiceProvider?)"/> — supports runtime scenarios
///     where the filter is restored from a database and deserializers are registered
///     via DI.
///   </description></item>
///   <item><description>
///     <see cref="EventDataDeserializerProvider.Default"/> — the built-in JSON-only
///     provider, used when neither of the above sources is available.
///   </description></item>
/// </list>
/// <para>
/// Register <see cref="IEventDataDeserializer"/> implementations with DI via
/// <see cref="EventPublisherBuilderExtensions.AddEventDataDeserializer{TDeserializer}"/>
/// and the <see cref="EventDispatcher"/> will automatically supply the resolved provider
/// when evaluating filters at runtime.
/// </para>
/// </remarks>
public sealed class TypedDataFilter<T> : EventDataFilter
    where T : class
{
    private readonly Func<T, bool> _predicate;
    private readonly EventDataDeserializerProvider? _provider;

    /// <summary>
    /// Initialises the filter.
    /// </summary>
    /// <param name="predicate">Predicate applied to the deserialized value.</param>
    /// <param name="provider">
    /// An explicit provider to use.  When <c>null</c> the filter resolves a provider from
    /// the DI <see cref="IServiceProvider"/> at evaluation time, falling back to
    /// <see cref="EventDataDeserializerProvider.Default"/> (JSON-only) if none is found.
    /// </param>
    public TypedDataFilter(Func<T, bool> predicate, EventDataDeserializerProvider? provider = null)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _provider = provider;
    }

    /// <summary>
    /// Gets the provider set at construction time, or <c>null</c> when the filter is
    /// configured to resolve the provider from DI at evaluation time.
    /// </summary>
    public EventDataDeserializerProvider? Provider => _provider;

    /// <inheritdoc/>
    /// <remarks>
    /// Delegates to <see cref="Matches(CloudEvent, IServiceProvider?)"/> with a <c>null</c>
    /// service provider — uses the construction-time provider or
    /// <see cref="EventDataDeserializerProvider.Default"/>.
    /// </remarks>
    public override bool Matches(CloudEvent @event)
        => Matches(@event, null);

    /// <summary>
    /// Evaluates the filter, resolving the <see cref="EventDataDeserializerProvider"/>
    /// from DI <paramref name="services"/> when none was supplied at construction time.
    /// </summary>
    /// <remarks>
    /// Provider resolution order:
    /// <list type="number">
    ///   <item><description>Construction-time provider (if not <c>null</c>).</description></item>
    ///   <item><description><see cref="EventDataDeserializerProvider"/> from <paramref name="services"/>.</description></item>
    ///   <item><description><see cref="EventDataDeserializerProvider.Default"/> (JSON-only fallback).</description></item>
    /// </list>
    /// </remarks>
    public override bool Matches(CloudEvent @event, IServiceProvider? services)
    {
        var provider = _provider
            ?? services?.GetService<EventDataDeserializerProvider>()
            ?? EventDataDeserializerProvider.Default;

        if (!provider.TryDeserialize<T>(@event, out var obj) || obj is null)
            return false;

        try
        {
            return _predicate(obj);
        }
        catch
        {
            return false;
        }
    }
}