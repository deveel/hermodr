//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

namespace Hermodr
{
    /// <summary>
    /// Carries the per-call gRPC primitives that an <see cref="IGrpcEventSender"/>
    /// needs to invoke a generated gRPC client: the <see cref="CallInvoker"/>,
    /// the per-endpoint call <see cref="Headers"/>, the <see cref="Deadline"/>,
    /// and the <see cref="CancellationToken"/>.
    /// </summary>
    /// <remarks>
    /// All members are standard <c>grpc-dotnet</c> / <c>Grpc.Core.Api</c> types;
    /// this is a thin parameter carrier, not a custom transport implementation.
    /// A sender typically constructs its generated client from
    /// <see cref="CallInvoker"/> and forwards <see cref="Headers"/>,
    /// <see cref="Deadline"/> and <see cref="CancellationToken"/> to the RPC call.
    /// </remarks>
    public readonly record struct GrpcCallContext(
        CallInvoker CallInvoker,
        Metadata Headers,
        DateTime Deadline,
        CancellationToken CancellationToken);
}
