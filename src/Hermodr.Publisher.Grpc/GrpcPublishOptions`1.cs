//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Type-specific publish options for a <see cref="GrpcPublishChannel{TEvent}"/>
    /// that routes events of type <typeparamref name="TEvent"/> to the configured
    /// gRPC endpoints.
    /// </summary>
    /// <remarks>
    /// Any property left at its default (<c>null</c>) will be inherited from the
    /// general-purpose <see cref="GrpcPublishOptions"/> registered alongside
    /// the non-typed channel.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this set of options is keyed against.
    /// </typeparam>
    public class GrpcPublishOptions<TEvent> : GrpcPublishOptions
        where TEvent : class
    {
    }
}
