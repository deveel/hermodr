//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

namespace Hermodr
{
    /// <summary>
    /// Default implementation of <see cref="IEventUpcastingPipeline"/>.
    /// </summary>
    /// <remarks>
    /// The pipeline discovers intermediate schema versions from the optional <see cref="IEventSchemaRegistry"/>.
    /// When no registry is available it attempts a direct upcast from the source to the target version.
    /// </remarks>
    public sealed class EventUpcastingPipeline : IEventUpcastingPipeline
    {
        private readonly IEventUpcasterRegistry _registry;
        private readonly IEventSchemaRegistry? _schemaRegistry;

        /// <summary>
        /// Creates a new <see cref="EventUpcastingPipeline"/>.
        /// </summary>
        /// <param name="registry">The upcaster registry.</param>
        /// <param name="schemaRegistry">
        /// Optional schema registry used to discover intermediate versions when chaining upcasters.
        /// </param>
        public EventUpcastingPipeline(IEventUpcasterRegistry registry, IEventSchemaRegistry? schemaRegistry = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _schemaRegistry = schemaRegistry;
        }

        /// <inheritdoc/>
        public async Task<JsonNode> UpcastAsync(
            string eventType,
            JsonNode data,
            Version fromVersion,
            Version toVersion,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(eventType);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(fromVersion);
            ArgumentNullException.ThrowIfNull(toVersion);

            var currentVersion = fromVersion;
            var currentData = data;

            while (currentVersion < toVersion)
            {
                var nextStep = FindNextStep(eventType, currentVersion, toVersion);
                if (nextStep == null)
                {
                    throw new EventUpcastingException(
                        $"No upcaster chain found from version {currentVersion} to {toVersion} " +
                        $"for event type '{eventType}'.");
                }

                var (upcaster, intermediateVersion) = nextStep.Value;
                var context = new UpcastContext(eventType, currentVersion, intermediateVersion, currentData);
                currentData = await upcaster.UpcastAsync(context, cancellationToken);
                currentVersion = intermediateVersion;
            }

            return currentData;
        }

        private (IEventUpcaster upcaster, Version toVersion)? FindNextStep(
            string eventType,
            Version currentVersion,
            Version finalVersion)
        {
            var candidates = GetCandidateVersions(eventType, currentVersion, finalVersion);
            var upcasters = _registry.GetUpcasters();

            foreach (var candidate in candidates)
            {
                var upcaster = upcasters.FirstOrDefault(u => u.CanUpcast(eventType, currentVersion, candidate));
                if (upcaster != null)
                    return (upcaster, candidate);
            }

            return null;
        }

        private IEnumerable<Version> GetCandidateVersions(string eventType, Version currentVersion, Version finalVersion)
        {
            var versions = new List<Version> { finalVersion };

            if (_schemaRegistry != null)
            {
                foreach (var schema in _schemaRegistry.GetAllVersions(eventType))
                {
                    if (Version.TryParse(schema.Version, out var version) &&
                        version > currentVersion &&
                        version <= finalVersion)
                    {
                        versions.Add(version);
                    }
                }
            }

            return versions.Distinct().OrderByDescending(v => v);
        }
    }
}
