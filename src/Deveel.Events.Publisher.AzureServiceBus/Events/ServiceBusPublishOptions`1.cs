//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Azure.Messaging.ServiceBus;

namespace Deveel.Events
{
    /// <summary>
    /// Type-specific publish options for a <see cref="ServiceBusEventPublishChannel{TEvent}"/>
    /// that routes events of type <typeparamref name="TEvent"/> to an Azure Service Bus queue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ConnectionString"/> and <see cref="QueueName"/> are redeclared as nullable
    /// so that leaving them unset signals "inherit from the base channel options".
    /// </para>
    /// <para>
    /// Any property left at <c>null</c> (or at its zero-value for strings) will be
    /// inherited from the general <see cref="ServiceBusPublishOptions"/>
    /// registered alongside the non-typed channel.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this set of options is keyed against.
    /// </typeparam>
    public class ServiceBusPublishOptions<TEvent> : ServiceBusPublishOptions
        where TEvent : class
    {
        /// <summary>
        /// Merges a base <see cref="ServiceBusPublishOptions"/> with the typed
        /// overrides in <paramref name="typedOpts"/>. Non-null / non-empty values from
        /// <paramref name="typedOpts"/> take precedence; all other values fall back to
        /// <paramref name="baseOpts"/>.
        /// </summary>
        public static ServiceBusPublishOptions Merge(
            ServiceBusPublishOptions baseOpts,
            ServiceBusPublishOptions<TEvent> typedOpts)
        {
            return new ServiceBusPublishOptions
            {
                ConnectionString = !string.IsNullOrWhiteSpace(typedOpts.ConnectionString)
                    ? typedOpts.ConnectionString!
                    : baseOpts.ConnectionString,
                QueueName = !string.IsNullOrWhiteSpace(typedOpts.QueueName)
                    ? typedOpts.QueueName!
                    : baseOpts.QueueName,
                ClientOptions = typedOpts.ClientOptions ?? baseOpts.ClientOptions,
            };
        }
    }
}
