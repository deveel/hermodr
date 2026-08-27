//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    public class GrpcChannelFactoryTests
    {
        /// <summary>
        /// Builds a real <see cref="IHttpClientFactory"/> via DI so the internal
        /// <c>GrpcChannelFactory</c> can be constructed through reflection.
        /// </summary>
        private static (object factory, IServiceProvider sp) CreateFactory()
        {
            var services = new ServiceCollection();
            services.AddHttpClient(GrpcDefaults.HttpClientName);
            var sp = services.BuildServiceProvider();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var factory = TestGrpc.CreateChannelFactory(httpClientFactory);
            return (factory, sp);
        }

        private static Grpc.Net.Client.GrpcChannel InvokeCreateChannel(object factory, string address, string? httpClientName = null)
        {
            var type = factory.GetType();
            var method = type.GetMethod("CreateChannel")!;
            return (Grpc.Net.Client.GrpcChannel)method.Invoke(factory, [address, httpClientName])!;
        }

        private static CallInvoker InvokeCreateCallInvoker(object factory, string address, string? httpClientName = null)
        {
            var type = factory.GetType();
            var method = type.GetMethod("CreateCallInvoker")!;
            return (CallInvoker)method.Invoke(factory, [address, httpClientName])!;
        }

        [Fact]
        public void CreateChannel_NullAddress_ThrowsArgumentNullException()
        {
            var (factory, _) = CreateFactory();

            // Reflection Invoke wraps exceptions in TargetInvocationException on
            // some runtimes; unwrap and assert the inner exception type.
            var ex = Assert.ThrowsAny<Exception>(() => InvokeCreateChannel(factory, null!));
            var actual = ex is TargetInvocationException tie ? tie.InnerException : ex;
            Assert.IsType<ArgumentNullException>(actual);
        }

        [Fact]
        public void CreateChannel_WithHttpAddress_ReturnsGrpcChannel()
        {
            var (factory, sp) = CreateFactory();
            try
            {
                var channel = InvokeCreateChannel(factory, "http://localhost:5001");
                Assert.NotNull(channel);
            }
            finally
            {
                (factory as Grpc.Net.Client.GrpcChannel)?.Dispose();
            }
        }

        [Fact]
        public void CreateChannel_SameAddressAndName_ReturnsCachedInstance()
        {
            var (factory, _) = CreateFactory();

            var ch1 = InvokeCreateChannel(factory, "http://localhost:5001", "client-a");
            var ch2 = InvokeCreateChannel(factory, "http://localhost:5001", "client-a");

            Assert.Same(ch1, ch2);
        }

        [Fact]
        public void CreateChannel_DifferentAddresses_ReturnDifferentInstances()
        {
            var (factory, _) = CreateFactory();

            var ch1 = InvokeCreateChannel(factory, "http://localhost:5001");
            var ch2 = InvokeCreateChannel(factory, "http://localhost:5002");

            Assert.NotSame(ch1, ch2);
        }

        [Fact]
        public void CreateChannel_DifferentNames_ReturnDifferentInstances()
        {
            var (factory, _) = CreateFactory();

            var ch1 = InvokeCreateChannel(factory, "http://localhost:5001", "client-a");
            var ch2 = InvokeCreateChannel(factory, "http://localhost:5001", "client-b");

            Assert.NotSame(ch1, ch2);
        }

        [Fact]
        public void CreateChannel_NullClientName_UsesDefaultName()
        {
            var (factory, _) = CreateFactory();

            // Null client name should fall back to the default. Two calls with
            // the same address and null name should return the same cached instance.
            var ch1 = InvokeCreateChannel(factory, "http://localhost:5001", null);
            var ch2 = InvokeCreateChannel(factory, "http://localhost:5001", null);

            Assert.Same(ch1, ch2);
        }

        [Fact]
        public void CreateCallInvoker_ReturnsNonNullInvoker()
        {
            var (factory, _) = CreateFactory();

            var invoker = InvokeCreateCallInvoker(factory, "http://localhost:5001");

            Assert.NotNull(invoker);
        }
    }
}
