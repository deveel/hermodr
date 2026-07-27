//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Transport-agnostic metadata describing a single registered
    /// <see cref="IEventPublishChannel"/> instance, exposed for tooling such as
    /// AsyncAPI / OpenAPI document generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The metadata is intentionally a <strong>free-form</strong> surface: the
    /// <see cref="Transport"/> value is a string (see <see cref="EventTransports"/>
    /// for the well-known constants) and the <see cref="Properties"/> bag uses
    /// convention-only keys. This keeps the abstraction open to third-party
    /// channels without requiring a framework release to add a new transport.
    /// </para>
    /// <para>
    /// Channels whose <see cref="Transport"/> equals <see cref="EventTransports.Other"/>
    /// represent storage / deferral / recovery layers (for example the Audit Trail
    /// or the Outbox) and are excluded from exported contracts.
    /// </para>
    /// </remarks>
    public interface IEventPublishChannelMetadata
    {
        /// <summary>
        /// The logical name of this channel instance, or <c>null</c> for an
        /// anonymous channel. Mirrors <see cref="INamedEventPublishChannel.Name"/>.
        /// </summary>
        string? ChannelName { get; }

        /// <summary>
        /// A free-form, case-sensitive transport identifier. See
        /// <see cref="EventTransports"/> for the well-known constants published by
        /// the framework; third-party channels may supply any string.
        /// </summary>
        string Transport { get; }

        /// <summary>
        /// A read-only, convention-only key/value bag carrying transport-specific
        /// addressing information (for example <c>exchange</c>, <c>routingKey</c>,
        /// <c>queue</c>, <c>topic</c>, <c>url</c>, <c>contentType</c>).
        /// Keys are not validated; unknown keys are passed through unchanged by
        /// exporters so the contract remains lossless.
        /// </summary>
        IReadOnlyDictionary<string, string?> Properties { get; }
    }
}