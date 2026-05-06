//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

internal sealed class DeadLetterStorageRegistrationMarker(string publisherName)
{
    public string PublisherName { get; } = publisherName;
}
