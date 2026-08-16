//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    /// <summary>
    /// Default <see cref="IDaprClientBuilder"/> that resolves the
    /// <see cref="DaprClient"/> registered through the standard
    /// <c>AddDaprClient()</c> DI flow.
    /// </summary>
    internal class DaprClientBuilder : IDaprClientBuilder
    {
        private readonly IServiceProvider _serviceProvider;

        public DaprClientBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public DaprClient CreateClient(string? name = null)
        {
            // When a name is supplied, try a keyed resolution first so callers
            // that registered a named DaprClient (via keyed services) keep
            // working; fall back to the unnamed singleton otherwise.
            if (!string.IsNullOrEmpty(name))
            {
                var keyed = _serviceProvider.GetKeyedService<DaprClient>(name);
                if (keyed is not null)
                    return keyed;
            }

            return _serviceProvider.GetRequiredService<DaprClient>();
        }
    }
}