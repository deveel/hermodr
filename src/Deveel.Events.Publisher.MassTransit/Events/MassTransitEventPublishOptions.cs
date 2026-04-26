//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Options that configure the <see cref="MassTransitEventPublishChannel"/>.
    /// </summary>
    public class MassTransitEventPublishOptions : EventPublishOptions
    {
        /// <summary>
        /// Merges <paramref name="baseOptions"/> with <paramref name="typedOptions"/>,
        /// where every non-<c>null</c> property in <paramref name="typedOptions"/>
        /// overrides the corresponding property from <paramref name="baseOptions"/>.
        /// </summary>
        public static MassTransitEventPublishOptions Merge(
            MassTransitEventPublishOptions baseOptions,
            MassTransitEventPublishOptions typedOptions)
        {
            return new MassTransitEventPublishOptions
            {
                DestinationAddress     = typedOptions.DestinationAddress     ?? baseOptions.DestinationAddress,
                MapAttributesToHeaders = typedOptions.MapAttributesToHeaders ?? baseOptions.MapAttributesToHeaders,
            };
        }


        /// <summary>
        /// Gets or sets the destination address to send the event to.
        /// When <c>null</c> the event is published using <see cref="MassTransit.IPublishEndpoint"/>;
        /// when set, the event is sent to the specified endpoint via
        /// <see cref="MassTransit.ISendEndpointProvider"/>.
        /// </summary>
        public Uri? DestinationAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the CloudEvent attributes
        /// (id, type, source, time, datacontenttype, etc.) should be mapped
        /// to MassTransit message headers.
        /// When <c>null</c> in a per-call override the channel default is used;
        /// the effective default is <c>true</c>.
        /// </summary>
        public bool? MapAttributesToHeaders { get; set; }
    }
}

