//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Thread-safe, in-memory implementation of <see cref="IEventUpcasterRegistry"/>.
    /// </summary>
    public sealed class EventUpcasterRegistry : IEventUpcasterRegistry
    {
        private readonly List<IEventUpcaster> _upcasters = new();
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventUpcasterRegistry"/> class.
        /// </summary>
        /// <param name="upcasters">
        /// Optional upcasters resolved from the dependency injection container. These are merged
        /// with any upcasters registered manually through <see cref="Register"/>.
        /// </param>
        public EventUpcasterRegistry(IEnumerable<IEventUpcaster>? upcasters = null)
        {
            if (upcasters != null)
            {
                _upcasters.AddRange(upcasters);
            }
        }

        /// <inheritdoc/>
        public void Register(IEventUpcaster upcaster)
        {
            ArgumentNullException.ThrowIfNull(upcaster);
            lock (_lock)
                _upcasters.Add(upcaster);
        }

        /// <inheritdoc/>
        public IReadOnlyList<IEventUpcaster> GetUpcasters()
        {
            lock (_lock)
                return _upcasters.ToList();
        }
    }
}
