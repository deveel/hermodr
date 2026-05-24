//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Hermodr {
    /// <summary>
    /// Internal shim that delegates to <see cref="EventSchemaFactory.Default"/> so that
    /// the static convenience method <see cref="EventSchema.FromDataType(Type)"/> continues
    /// to compile without changes while the actual logic lives in <see cref="EventSchemaFactory"/>.
    /// </summary>
    static class EventSchemaCreator {
        public static EventSchema FromEventData(Type dataType)
            => EventSchemaFactory.Default.CreateFromType(dataType);
    }
}
