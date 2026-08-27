//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    public class GrpcOptionsMergeAndMetadataTests
    {
        [Fact]
        public void Merge_ChannelName_Overrides()
        {
            var baseOptions = new GrpcPublishOptions
            {
                ChannelName = "base-channel",
                Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }],
            };
            var overrideOptions = new GrpcPublishOptions
            {
                ChannelName = "override-channel",
            };

            var merged = GrpcPublishOptions.Merge(baseOptions, overrideOptions);

            Assert.Equal("override-channel", merged.ChannelName);
        }

        [Fact]
        public void Merge_ScheduleDeliveryAt_Overrides()
        {
            var baseTime = DateTimeOffset.UtcNow;
            var overrideTime = baseTime.AddHours(1);

            var baseOptions = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }],
                ScheduleDeliveryAt = baseTime,
            };
            var overrideOptions = new GrpcPublishOptions
            {
                ScheduleDeliveryAt = overrideTime,
            };

            var merged = GrpcPublishOptions.Merge(baseOptions, overrideOptions);

            Assert.Equal(overrideTime, merged.ScheduleDeliveryAt);
        }

        [Fact]
        public void Validate_WhitespaceAddress_ReturnsFailure()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "   " }],
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r => r.ErrorMessage!.Contains("required and must not be empty"));
        }

        [Fact]
        public void GetChannelMetadata_SingleEndpoint_UsesAddressKey()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }],
            };

            var metadata = ((IChannelMetadataSource)options).GetChannelMetadata();

            Assert.Single(metadata.Properties);
            Assert.Equal("https://svc.example.com:5001", metadata.Properties["address"]);
            // No address.1 key for a single endpoint
            Assert.False(metadata.Properties.ContainsKey("address.1"));
        }

        [Fact]
        public void GetChannelMetadata_EmptyAddressEndpoint_Skipped()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints =
                [
                    new GrpcEndpoint { Address = "https://svc.example.com:5001" },
                    new GrpcEndpoint { Address = "" },
                ],
            };

            var metadata = ((IChannelMetadataSource)options).GetChannelMetadata();

            // Only the non-empty endpoint should be in the properties
            Assert.Single(metadata.Properties);
            Assert.Equal("https://svc.example.com:5001", metadata.Properties["address"]);
        }
    }
}
