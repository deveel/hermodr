using Azure.Messaging.ServiceBus;

using NSubstitute;

namespace Deveel.Events {
	class TestServiceBusClientFactory : IServiceBusClientFactory {
		private readonly Action<ServiceBusMessage> _messageReceived;

		public TestServiceBusClientFactory(Action<ServiceBusMessage> messageReceived) {
			_messageReceived = messageReceived;
		}

		public ServiceBusClient CreateClient(string connectionString, ServiceBusClientOptions options) {
			var senderMock = Substitute.For<ServiceBusSender>();
			senderMock.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
				.Returns(call => { 
					_messageReceived(call.Arg<ServiceBusMessage>());
					return Task.CompletedTask; 
				});

			var clientMock = Substitute.For<ServiceBusClient>();
			clientMock.CreateSender(Arg.Any<string>())
				.Returns(senderMock);

			return clientMock;
		}
	}
}