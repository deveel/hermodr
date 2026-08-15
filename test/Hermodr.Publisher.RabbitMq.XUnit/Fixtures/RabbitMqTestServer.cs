
using Testcontainers.RabbitMq;

namespace Hermodr
{
    public class RabbitMqTestServer : IAsyncLifetime
    {
        private readonly RabbitMqContainer rabbitMq;

        public RabbitMqTestServer()
        {
rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine")
            .Build();
        }

        public string ConnectionString => rabbitMq.GetConnectionString();


        public async ValueTask DisposeAsync()
        {
            await rabbitMq.StopAsync();
            await rabbitMq.DisposeAsync();
        }

        public async ValueTask InitializeAsync()
        {
            await rabbitMq.StartAsync();
        }
    }
}
