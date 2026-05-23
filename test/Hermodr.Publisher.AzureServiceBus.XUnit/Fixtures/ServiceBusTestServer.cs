using Testcontainers.ServiceBus;

namespace Hermodr
{
    /// <summary>
    /// An xUnit class fixture that starts an Azure Service Bus emulator
    /// via Testcontainers and exposes its connection string.
    /// </summary>
    public sealed class ServiceBusTestServer : IAsyncLifetime
    {
        private static readonly string ConfigFilePath =
            Path.Join(AppContext.BaseDirectory, "service-bus-config.json");

        private readonly ServiceBusContainer _container;

        public ServiceBusTestServer()
        {
            _container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
                .WithAcceptLicenseAgreement(true)
                .WithConfig(ConfigFilePath)
                .Build();
        }

        /// <summary>
        /// The AMQP connection string for the running Service Bus emulator.
        /// </summary>
        public string ConnectionString => _container.GetConnectionString();

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}


