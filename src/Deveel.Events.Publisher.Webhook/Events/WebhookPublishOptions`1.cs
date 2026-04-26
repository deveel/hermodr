//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Type-specific publish options for a <see cref="WebhookEventPublishChannel{TEvent}"/>
    /// that routes events of type <typeparamref name="TEvent"/> to a webhook endpoint.
    /// </summary>
    /// <remarks>
    /// Any property left at its default (<c>null</c>) will be inherited from the
    /// general-purpose <see cref="WebhookPublishOptions"/> registered alongside
    /// the non-typed channel.  Channel-structural properties
    /// (<c>SignatureHeaderName</c>, <c>RetryableStatusCodes</c>, etc.) are always
    /// taken from the base options regardless of what is set here.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this set of options is keyed against.
    /// </typeparam>
    public class WebhookPublishOptions<TEvent> : WebhookPublishOptions
        where TEvent : class
    {
    }
}
