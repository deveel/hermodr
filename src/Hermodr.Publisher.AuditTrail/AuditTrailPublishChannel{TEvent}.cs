using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr;

/// <summary>
/// A typed <see cref="AuditTrailPublishChannel"/> that records only events whose
/// data class is <typeparamref name="TEvent"/>. This channel implements
/// <see cref="IEventPublishChannel{TEvent}"/> to participate in the publisher's
/// typed channel routing mechanism.
/// </summary>
/// <typeparam name="TEvent">
/// The annotated event data class this channel is keyed against.
/// </typeparam>
public class AuditTrailPublishChannel<TEvent> : AuditTrailPublishChannel, IEventPublishChannel<TEvent>
    where TEvent : class
{
    /// <summary>
    /// Creates a new instance of <see cref="AuditTrailPublishChannel{TEvent}"/>.
    /// </summary>
    /// <param name="writer">
    /// The audit trail writer used to append events.
    /// </param>
    /// <param name="options">
    /// The channel-level default options resolved from the DI container.
    /// </param>
    /// <param name="validators">
    /// An optional collection of <see cref="IValidateOptions{AuditTrailPublishOptions}"/> services.
    /// </param>
    /// <param name="logger">
    /// An optional logger.
    /// </param>
    public AuditTrailPublishChannel(
        IAuditTrailWriter writer,
        IOptions<AuditTrailPublishOptions> options,
        IEnumerable<IValidateOptions<AuditTrailPublishOptions>>? validators = null,
        ILogger<AuditTrailPublishChannel>? logger = null)
        : base(writer, options, validators, logger)
    {
    }
}
