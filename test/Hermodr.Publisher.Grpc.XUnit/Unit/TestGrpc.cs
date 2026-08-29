//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;

namespace Hermodr
{
    /// <summary>
    /// Test helpers for the gRPC publisher channel: a fake sender that records
    /// calls, a stub channel factory, and a builder utility.
    /// </summary>
    internal static class TestGrpc
    {
        public static CloudEvent MakeEvent(string type = "person.created") => new()
        {
            Type = type,
            Source = new Uri("https://api.example.com/svc"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = """{"name":"John Doe"}""",
        };

        public static GrpcEndpoint MakeEndpoint(string address = "https://svc.example.com:5001")
            => new()
            {
                Address = address,
                HttpClientName = "test-grpc-client",
            };

        /// <summary>
        /// A fake <see cref="IGrpcEventSender"/> that records every call and
        /// optionally throws.
        /// </summary>
        public sealed class FakeSender : IGrpcEventSender
        {
            private readonly Exception? _throwOnCall;

            public List<CloudEvent> SentEvents { get; } = new();
            public List<IReadOnlyList<CloudEvent>> SentBatches { get; } = new();
            public List<GrpcCallContext> CapturedContexts { get; } = new();

            public FakeSender(Exception? throwOnCall = null)
            {
                _throwOnCall = throwOnCall;
            }

            public Task SendAsync(CloudEvent @event, GrpcCallContext context)
            {
                if (_throwOnCall is not null)
                    return Task.FromException(_throwOnCall);

                SentEvents.Add(@event);
                CapturedContexts.Add(context);
                return Task.CompletedTask;
            }

            public Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
            {
                if (_throwOnCall is not null)
                    return Task.FromException(_throwOnCall);

                SentBatches.Add(events);
                CapturedContexts.Add(context);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// A stub <see cref="IGrpcChannelFactory"/> that records resolved
        /// addresses and returns a dummy <see cref="CallInvoker"/> — no real
        /// gRPC channel is created.
        /// </summary>
        public sealed class StubChannelFactory : IGrpcChannelFactory
        {
            public List<string> ResolvedAddresses { get; } = new();
            public List<string?> ResolvedClientNames { get; } = new();

            public Grpc.Net.Client.GrpcChannel CreateChannel(string address, string? httpClientName = null)
            {
                ResolvedAddresses.Add(address);
                ResolvedClientNames.Add(httpClientName);
                throw new NotSupportedException(
                    "StubChannelFactory does not create real GrpcChannel. Use CreateCallInvoker for tests.");
            }

            public CallInvoker CreateCallInvoker(string address, string? httpClientName = null)
            {
                ResolvedAddresses.Add(address);
                ResolvedClientNames.Add(httpClientName);
                return new DummyCallInvoker();
            }
        }

        private sealed class DummyCallInvoker : CallInvoker
        {
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException("DummyCallInvoker is not invocable.");

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException("DummyCallInvoker is not invocable.");

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options)
                => throw new NotSupportedException("DummyCallInvoker is not invocable.");

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException("DummyCallInvoker is not invocable.");

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string? host, CallOptions options)
                => throw new NotSupportedException("DummyCallInvoker is not invocable.");
        }

        /// <summary>
        /// Builds a gRPC publisher channel through DI with the given endpoints and
        /// a fake sender / stub channel factory, returning the resolved channel
        /// and the fake sender for assertions.
        /// </summary>
        public static (IEventPublishChannel channel, FakeSender sender, StubChannelFactory channelFactory) BuildChannel(
            IReadOnlyList<GrpcEndpoint> endpoints,
            Exception? throwOnCall = null)
        {
            var sender = new FakeSender(throwOnCall);
            var channelFactory = new StubChannelFactory();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = endpoints;
                });

            // Override the infrastructure with our fakes.
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(sender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(channelFactory);

            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            return (channel, sender, channelFactory);
        }

        // ───────────────────────────────────────────────────────────────
        // Reflection helpers for internal types
        // (no InternalsVisibleTo — mirrors WebhookCoverageTests pattern)
        // ───────────────────────────────────────────────────────────────

        private static readonly Type ChannelFactoryType =
            typeof(GrpcPublishOptions).Assembly.GetType("Hermodr.GrpcChannelFactory")!;

        private static readonly Type TelemetryType =
            typeof(GrpcPublishOptions).Assembly.GetType("Hermodr.GrpcTransportTelemetry")!;

        private static readonly Type ThrowingSenderType =
            typeof(GrpcPublishOptions).Assembly.GetType("Hermodr.ThrowingGrpcEventSender")!;

        /// <summary>
        /// Creates an instance of the internal <c>GrpcChannelFactory</c> via
        /// reflection, wrapping the supplied <see cref="IHttpClientFactory"/>.
        /// </summary>
        public static object CreateChannelFactory(IHttpClientFactory httpClientFactory)
            => Activator.CreateInstance(ChannelFactoryType, httpClientFactory)!;

        /// <summary>
        /// Creates an instance of the internal <c>GrpcTransportTelemetry</c>
        /// via reflection using the default constructor (which binds the
        /// framework's shared <c>ActivitySource</c>).
        /// </summary>
        public static object CreateTelemetry()
            => Activator.CreateInstance(TelemetryType)!;

        /// <summary>
        /// Creates an instance of the internal <c>GrpcTransportTelemetry</c>
        /// via reflection using the parameterized constructor that accepts a
        /// custom <see cref="ActivitySource"/>.
        /// </summary>
        public static object CreateTelemetry(ActivitySource activitySource)
            => Activator.CreateInstance(TelemetryType, activitySource)!;

        /// <summary>
        /// Invokes the <c>StartPublishActivity</c> method on the telemetry
        /// instance via reflection.
        /// </summary>
        public static Activity? StartPublishActivity(
            object telemetry, string? eventType, string? address, string rpcType = "unary")
        {
            var method = TelemetryType.GetMethod(
                "StartPublishActivity",
                BindingFlags.Instance | BindingFlags.Public)!;
            return (Activity?)method.Invoke(telemetry, [eventType, address, rpcType]);
        }

        /// <summary>
        /// Creates an instance of the internal <c>ThrowingGrpcEventSender</c>
        /// via reflection.
        /// </summary>
        public static IGrpcEventSender CreateThrowingSender()
            => (IGrpcEventSender)Activator.CreateInstance(ThrowingSenderType)!;

        // ───────────────────────────────────────────────────────────────
        // Collecting logger for asserting log output (warnings, etc.)
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// An <see cref="ILoggerProvider"/> that records every log entry so tests
        /// can assert that specific warnings (e.g. plaintext endpoints, failures
        /// superseded by cancellation) were emitted.
        /// </summary>
        public sealed class CollectingLoggerProvider : ILoggerProvider
        {
            private readonly object _lock = new();
            private readonly List<Entry> _entries = new();

            /// <summary>Gets a snapshot of all recorded log entries.</summary>
            public IReadOnlyList<Entry> Entries
            {
                get
                {
                    lock (_lock)
                        return _entries.ToArray();
                }
            }

            public ILogger CreateLogger(string categoryName) => new CollectingLogger(this, categoryName);

            public void Dispose()
            {
            }

            public sealed record Entry(LogLevel Level, string Category, string Message, Exception? Exception);

            private sealed class CollectingLogger(CollectingLoggerProvider owner, string categoryName) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                    lock (owner._lock)
                    {
                        owner._entries.Add(
                            new Entry(logLevel, categoryName, formatter(state, exception), exception));
                    }
                }
            }
        }

        // ───────────────────────────────────────────────────────────────
        // Permissive-validator helper for defensive-guard tests
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// An <see cref="IValidateOptions{TOptions}"/> that always reports
        /// success, used to bypass DataAnnotations validation and reach the
        /// channel's defensive empty-endpoints guard.
        /// </summary>
        public sealed class PermissiveValidator : IValidateOptions<GrpcPublishOptions>
        {
            public ValidateOptionsResult Validate(string? name, GrpcPublishOptions options)
                => ValidateOptionsResult.Success;
        }

        /// <summary>
        /// Builds a gRPC publisher channel through DI with a permissive options
        /// validator so that empty/null endpoint lists bypass DataAnnotations
        /// validation and reach the channel's defensive guard.
        /// </summary>
        public static (IEventPublishChannel channel, FakeSender sender) BuildChannelWithPermissiveValidator(
            IReadOnlyList<GrpcEndpoint> endpoints)
        {
            var sender = new FakeSender();
            var channelFactory = new StubChannelFactory();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = endpoints;
                });

            // Register the permissive validator so DataAnnotations fallback is skipped.
            services.AddSingleton<IValidateOptions<GrpcPublishOptions>, PermissiveValidator>();

            // Override the infrastructure with our fakes.
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(sender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(channelFactory);

            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            return (channel, sender);
        }
    }
}
