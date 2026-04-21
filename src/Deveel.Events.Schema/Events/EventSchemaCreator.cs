//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Deveel.Events {
	// Kept as an internal shim so EventSchema.FromDataType(Type) continues to compile
	// without changes. All logic now lives in EventSchemaFactory.
	static class EventSchemaCreator {
		public static EventSchema FromEventData(Type dataType)
			=> EventSchemaFactory.Default.CreateFromType(dataType);
	}
}
