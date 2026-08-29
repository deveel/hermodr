//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

namespace Hermodr
{
    public class GrpcTransportTelemetryTests
    {
        /// <summary>
        /// Creates an <see cref="ActivityListener"/> that listens to all sources
        /// and samples all data, so <c>ActivitySource.StartActivity</c> returns
        /// non-null activities.
        /// </summary>
        private static ActivityListener CreateListener()
            => new()
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };

        [Fact]
        public void StartPublishActivity_ReturnsActivityWithGrpcSpanName()
        {
            using var listener = CreateListener();
            ActivitySource.AddActivityListener(listener);

            var telemetry = TestGrpc.CreateTelemetry();
            using var activity = TestGrpc.StartPublishActivity(telemetry, "order.created", "https://svc.example.com:5001");

            Assert.NotNull(activity);
            Assert.Equal(TelemetryConstants.SpanTransportGrpc, activity!.OperationName);
            Assert.Equal(ActivityKind.Producer, activity.Kind);
        }

        [Fact]
        public void StartPublishActivity_NullEventType_UsesUnknown()
        {
            using var listener = CreateListener();
            ActivitySource.AddActivityListener(listener);

            var telemetry = TestGrpc.CreateTelemetry();
            using var activity = TestGrpc.StartPublishActivity(telemetry, null, "https://svc.example.com:5001");

            Assert.NotNull(activity);
            var tagValue = activity!.GetTagItem(TelemetryTags.EventType)?.ToString();
            Assert.Equal("unknown", tagValue);
        }

        [Fact]
        public void StartPublishActivity_CustomRpcType_SetsTag()
        {
            using var listener = CreateListener();
            ActivitySource.AddActivityListener(listener);

            var telemetry = TestGrpc.CreateTelemetry();
            using var activity = TestGrpc.StartPublishActivity(
                telemetry, "order.created", "https://svc.example.com:5001", "client_streaming");

            Assert.NotNull(activity);
            var rpcTag = activity!.GetTagItem("rpc.type")?.ToString();
            Assert.Equal("client_streaming", rpcTag);
        }

        [Fact]
        public void Constructor_WithCustomActivitySource_UsesProvidedSource()
        {
            using var listener = CreateListener();
            ActivitySource.AddActivityListener(listener);

            using var customSource = new ActivitySource("Test.Grpc.Telemetry.Custom");
            var telemetry = TestGrpc.CreateTelemetry(customSource);
            using var activity = TestGrpc.StartPublishActivity(telemetry, "test.event", "https://svc.example.com:5001");

            // The activity should be created from the custom source, so it
            // should be non-null and its source should match.
            Assert.NotNull(activity);
            Assert.Equal("Test.Grpc.Telemetry.Custom", activity!.Source.Name);
        }
    }
}
