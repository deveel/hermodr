//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Describes an individual, statically-configured gRPC endpoint that the
    /// <see cref="GrpcPublishChannel"/> delivers CloudEvents to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each endpoint is delivered to independently: the channel fans out a
    /// single <c>PublishAsync</c> call to every configured endpoint
    /// concurrently, using the endpoint's own named
    /// <see cref="System.Net.Http.HttpClient"/> (and therefore its own TLS/mTLS
    /// and <c>CallCredentials</c> configuration).
    /// </para>
    /// <para>
    /// The <see cref="SenderName"/> property optionally selects a named
    /// <see cref="IGrpcEventSender"/> registered in the DI container, allowing
    /// different endpoints to target different <c>.proto</c>-generated gRPC
    /// services. When <c>null</c> the default (unnamed) sender is used.
    /// </para>
    /// </remarks>
    public class GrpcEndpoint
    {
        /// <summary>
        /// Gets or sets the absolute address of the gRPC endpoint the event is
        /// delivered to (for example <c>https://svc.example.com:5001</c>).
        /// </summary>
        [Required(AllowEmptyStrings = false,
            ErrorMessage = "The endpoint address is required and must not be empty.")]
        [Url(ErrorMessage = "The endpoint address must be a valid absolute URL.")]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional name used to resolve the named
        /// <see cref="System.Net.Http.HttpClient"/> for this endpoint through
        /// <see cref="System.Net.Http.IHttpClientFactory"/>. When <c>null</c>
        /// the default channel name (<see cref="GrpcDefaults.HttpClientName"/>)
        /// is used.
        /// </summary>
        public string? HttpClientName { get; set; }

        /// <summary>
        /// Gets or sets an optional name used to resolve a named
        /// <see cref="IGrpcEventSender"/> for this endpoint. When <c>null</c>
        /// the default (unnamed) sender is used.
        /// </summary>
        /// <remarks>
        /// Use this when different endpoints target different
        /// <c>.proto</c>-generated gRPC services that require distinct sender
        /// implementations.
        /// </remarks>
        public string? SenderName { get; set; }

        /// <summary>
        /// Gets or sets the deadline applied to each individual gRPC call for
        /// this endpoint. When <c>null</c> the channel default
        /// (<see cref="GrpcDefaults.DefaultDeadline"/>) is used.
        /// </summary>
        public TimeSpan? Deadline { get; set; }

        /// <summary>
        /// Gets or sets additional gRPC call metadata (headers) merged into
        /// every call for this endpoint.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
