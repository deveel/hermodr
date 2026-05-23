//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Represents an element (schema, property) that carries a <see cref="System.Version"/>.
    /// Used internally by <see cref="EventPropertyCollection"/> to enforce that child properties
    /// do not declare a version greater than the owning element.
    /// </summary>
    interface IVersionedElement {
        /// <summary>Gets the version of this element.</summary>
        Version Version { get; }
    }
}
