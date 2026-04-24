//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Reflection;

namespace Deveel.Events {
    class EventPublisherOptionsValidator : IValidateOptions<EventPublisherOptions> {
        private readonly IServiceCollection _services;

        public EventPublisherOptionsValidator(IServiceCollection services) {
            _services = services;
        }

        public ValidateOptionsResult Validate(string? name, EventPublisherOptions options) {
            if (options.DataSchemaBaseUri != null)
                return ValidateOptionsResult.Success;

            var eventTypes = GetRegisteredEventTypesUsingDataVersion();
            if (eventTypes.Count == 0)
                return ValidateOptionsResult.Success;

            var typeNames = String.Join(", ", eventTypes.Select(x => x.FullName ?? x.Name));
            return ValidateOptionsResult.Fail(
                $"EventPublisherOptions.DataSchemaBaseUri must be configured because the following registered event types use [Event] DataVersion: {typeNames}. "+
                "Configure it via services.AddEventPublisher(options => options.DataSchemaBaseUri = new Uri(\"https://schemas.example.com/events\"));");
        }

        private IReadOnlyCollection<Type> GetRegisteredEventTypesUsingDataVersion() {
            var eventTypes = new HashSet<Type>();

            foreach (var descriptor in _services) {
                AddIfVersionedEvent(eventTypes, descriptor.ServiceType);
                AddIfVersionedEvent(eventTypes, descriptor.ImplementationType);

                if (descriptor.ImplementationInstance != null)
                    AddIfVersionedEvent(eventTypes, descriptor.ImplementationInstance.GetType());
            }

            return eventTypes;
        }

        private static void AddIfVersionedEvent(HashSet<Type> eventTypes, Type? type) {
            if (type == null)
                return;

            var eventAttribute = type.GetCustomAttribute<EventAttribute>(false);
            if (eventAttribute == null)
                return;

            if (eventAttribute.DataSchema != null || String.IsNullOrWhiteSpace(eventAttribute.DataVersion))
                return;

            eventTypes.Add(type);
        }
    }
}

