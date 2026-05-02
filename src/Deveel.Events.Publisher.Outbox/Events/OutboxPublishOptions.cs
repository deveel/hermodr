//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// Configuration options for the <see cref="OutboxPublishChannel{TMessage}"/>.
/// </summary>
/// <remarks>
/// This class currently carries no outbox-specific properties; it exists so that
/// callers can supply per-call overrides through the standard
/// <see cref="EventPublishOptions"/>-based mechanism and so that future versions
/// can add outbox-specific settings without a breaking change.
/// </remarks>
public class OutboxPublishOptions : EventPublishOptions
{
}