//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    public class GrpcEndpointValidationTests
    {
        [Fact]
        public void Validate_EmptyEndpoints_ReturnsFailure()
        {
            var options = new GrpcPublishOptions { Endpoints = [] };
            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(GrpcPublishOptions.Endpoints)));
        }

        [Fact]
        public void Validate_NullEndpointInList_ReturnsFailure()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = new GrpcEndpoint[] { null! },
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r => r.ErrorMessage!.Contains("null"));
        }

        [Fact]
        public void Validate_InvalidAddress_ReturnsFailure()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "not-a-url" }],
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r => r.ErrorMessage!.Contains("valid absolute URL"));
        }

        [Fact]
        public void Validate_RelativeAddress_ReturnsFailure()
        {
            // "relative/path" (without a leading slash) is not parsed as a file URI
            // by Uri.TryCreate with UriKind.Absolute, so it fails the absolute-URI check.
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "relative/path" }],
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r => r.ErrorMessage!.Contains("valid absolute URL"));
        }

        [Fact]
        public void Validate_ValidEndpoint_ReturnsNoFailures()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }],
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Empty(results);
        }

        [Fact]
        public void Validate_HttpScheme_IsAllowed()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "http://localhost:5000" }],
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Empty(results);
        }

        [Theory]
        [InlineData("ftp://svc.example.com:5001")]
        [InlineData("file://some/path")]
        public void Validate_NonHttpScheme_ReturnsFailure(string address)
        {
            var options = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = address }],
            };

            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r =>
                r.MemberNames.Contains($"{nameof(GrpcPublishOptions.Endpoints)}[0].Address") &&
                r.ErrorMessage!.Contains("http or https"));
        }

        [Fact]
        public void Validate_InvalidHeaderKey_ReturnsFailure()
        {
            // gRPC ASCII metadata keys only allow lowercase alphanumeric characters,
            // underscores, hyphens and dots (grpc-dotnet throws at Metadata.Add).
            var endpoint = new GrpcEndpoint { Address = "https://svc.example.com:5001" };
            endpoint.Headers["x custom"] = "value";

            var options = new GrpcPublishOptions { Endpoints = [endpoint] };
            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r =>
                r.MemberNames.Contains($"{nameof(GrpcPublishOptions.Endpoints)}[0].Headers") &&
                r.ErrorMessage!.Contains("x custom"));
        }

        [Fact]
        public void Validate_BinaryHeaderKeyWithStringValue_ReturnsFailure()
        {
            // "-bin" keys denote binary metadata and cannot be carried as string
            // values through the endpoint headers dictionary.
            var endpoint = new GrpcEndpoint { Address = "https://svc.example.com:5001" };
            endpoint.Headers["trace-bin"] = "whatever";

            var options = new GrpcPublishOptions { Endpoints = [endpoint] };
            var results = options.Validate(new ValidationContext(options));

            Assert.Contains(results, r =>
                r.MemberNames.Contains($"{nameof(GrpcPublishOptions.Endpoints)}[0].Headers"));
        }

        [Fact]
        public void Validate_ValidHeaders_ReturnsNoFailures()
        {
            var endpoint = new GrpcEndpoint { Address = "https://svc.example.com:5001" };
            endpoint.Headers["x-custom"] = "value123";
            endpoint.Headers["trace.id_1"] = "value456";

            var options = new GrpcPublishOptions { Endpoints = [endpoint] };
            var results = options.Validate(new ValidationContext(options));

            Assert.Empty(results);
        }

        [Fact]
        public void GetChannelMetadata_ReturnsGrpcTransportWithAddress()
        {
            var options = new GrpcPublishOptions
            {
                Endpoints =
                [
                    new GrpcEndpoint { Address = "https://svc1.example.com:5001" },
                    new GrpcEndpoint { Address = "https://svc2.example.com:5001" },
                ],
            };

            var metadata = ((IChannelMetadataSource)options).GetChannelMetadata();

            Assert.Equal(EventTransports.Grpc, metadata.Transport);
            Assert.Equal("https://svc1.example.com:5001", metadata.Properties["address"]);
            Assert.Equal("https://svc2.example.com:5001", metadata.Properties["address.1"]);
        }

        [Fact]
        public void Merge_OverridesEndpoints()
        {
            var baseOptions = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://base.example.com:5001" }],
            };
            var overrideOptions = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://override.example.com:5001" }],
            };

            var merged = GrpcPublishOptions.Merge(baseOptions, overrideOptions);

            Assert.Single(merged.Endpoints);
            Assert.Equal("https://override.example.com:5001", merged.Endpoints[0].Address);
        }

        [Fact]
        public void Merge_NullOverride_KeepsBaseEndpoints()
        {
            var baseOptions = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://base.example.com:5001" }],
            };
            var overrideOptions = new GrpcPublishOptions
            {
                Endpoints = null!,
            };

            var merged = GrpcPublishOptions.Merge(baseOptions, overrideOptions);

            Assert.Single(merged.Endpoints);
            Assert.Equal("https://base.example.com:5001", merged.Endpoints[0].Address);
        }

        [Fact]
        public void Merge_EmptyOverride_KeepsBaseEndpoints()
        {
            // An empty (not just null) override list must not clobber the base
            // endpoints: options instances initialize Endpoints to an empty array,
            // so typed/per-call options that did not configure endpoints would
            // otherwise silently replace them and fail every publish.
            var baseOptions = new GrpcPublishOptions
            {
                Endpoints = [new GrpcEndpoint { Address = "https://base.example.com:5001" }],
            };
            var overrideOptions = new GrpcPublishOptions
            {
                Endpoints = [],
            };

            var merged = GrpcPublishOptions.Merge(baseOptions, overrideOptions);

            Assert.Single(merged.Endpoints);
            Assert.Equal("https://base.example.com:5001", merged.Endpoints[0].Address);
        }
    }
}
