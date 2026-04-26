//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

namespace Deveel.Events {
    /// <summary>
    /// An <see cref="IEventPublishCallback"/> that wraps a caller-supplied delegate.
    /// </summary>
    /// <remarks>
    /// Created by the overloads of
    /// <see cref="EventPublisherBuilderExtensions.AddTestChannel(EventPublisherBuilder,System.Func{CloudNative.CloudEvents.CloudEvent,System.Threading.Tasks.Task})"/>
    /// and its synchronous variant.
    /// </remarks>
    class DelegatedEventPublishCallback : IEventPublishCallback {
        private readonly Func<CloudEvent, Task>? _asyncCallback;
        private readonly Action<CloudEvent>? _callback;

        /// <summary>
        /// Constructs the callback with an asynchronous delegate.
        /// </summary>
        public DelegatedEventPublishCallback(Func<CloudEvent, Task> callback) {
            _asyncCallback = callback;
        }

        /// <summary>
        /// Constructs the callback with a synchronous delegate.
        /// </summary>
        public DelegatedEventPublishCallback(Action<CloudEvent> callback) {
            _callback = callback;
        }

        /// <inheritdoc/>
        public Task OnEventPublishedAsync(CloudEvent @event) {
            if (_callback != null) {
                _callback(@event);
                return Task.CompletedTask;
            }

            if (_asyncCallback != null)
                return _asyncCallback(@event);

            return Task.CompletedTask;
        }
    }
}
