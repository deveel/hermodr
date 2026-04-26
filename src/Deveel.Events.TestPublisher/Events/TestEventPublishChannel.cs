//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events {
	class TestEventPublishChannel : IEventPublishChannel {
		private readonly IEventPublishCallback _callback;

		public TestEventPublishChannel(IEventPublishCallback callback) {
			_callback = callback;
		}

		public Task PublishAsync(CloudEvent @event, EventPublishChannelOptions? options = null, CancellationToken cancellationToken = default) {
			return _callback.OnEventPublishedAsync(@event);
		}
	}
}
