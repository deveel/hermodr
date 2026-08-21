//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Net;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    /// <summary>
    /// Shared test fakes for the HTTP publisher test suite.
    /// </summary>
    internal static class TestHttp
    {
        public static HttpResponseMessage OK() => new(HttpStatusCode.OK);

        /// <summary>
        /// Builds the HTTP channel for the given endpoints, overriding each endpoint's
        /// named client primary handler with <paramref name="handler"/>.
        /// </summary>
        public static IEventPublishChannel BuildChannel(
            IEnumerable<HttpEndpoint> endpoints,
            HttpMessageHandler handler,
            Action<HttpPublishOptions>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddHttpEventPublisherChannel(options =>
                {
                    options.Endpoints = endpoints.ToList();
                    configure?.Invoke(options);
                });

            // Override the default client too, so per-call endpoint overrides that do
            // not name their own client (and therefore fall back to the default name)
            // are also routed through the fake handler.
            services.AddHttpClient(HttpDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            foreach (var endpoint in endpoints)
            {
                services.AddHttpClient(endpoint.HttpClientName!)
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            }

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredKeyedService<IEventPublishChannel>("");
        }

        public sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = _handler(request);
                response.RequestMessage = request;
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// A <see cref="DelegatingHandler"/> that appends a fixed header to every request,
        /// used to exercise custom-handler registration via <c>ConfigureHttpClient</c>.
        /// </summary>
        public sealed class CustomHeaderHandler : DelegatingHandler
        {
            private readonly string _name;
            private readonly string _value;

            public CustomHeaderHandler(string name, string value)
            {
                _name = name;
                _value = value;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                request.Headers.TryAddWithoutValidation(_name, _value);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}