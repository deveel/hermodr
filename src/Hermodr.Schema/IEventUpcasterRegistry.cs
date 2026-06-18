//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Stores registered <see cref="IEventUpcaster"/> instances.
    /// </summary>
    public interface IEventUpcasterRegistry
    {
        /// <summary>
        /// Registers an upcaster.
        /// </summary>
        /// <param name="upcaster">The upcaster to register.</param>
        void Register(IEventUpcaster upcaster);

        /// <summary>
        /// Gets all registered upcasters.
        /// </summary>
        /// <returns>A read-only list of registered upcasters.</returns>
        IReadOnlyList<IEventUpcaster> GetUpcasters();
    }
}
