namespace EventBusRabbitMQ
{
    using System;
    using System.Net.Sockets;
    using System.Text.Json;

    using EventBus.Abstractions;
    using EventBus.Events;

    using Microsoft.Extensions.Logging;

    using Polly;
    using Polly.Retry;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Exceptions;

    // TODO: изучить RabbitMQ на предмет полезных настрек
    public class EventBusRabbitMQ : IEventBus
    {
        const string BROKER_NAME = "converter_event_bus"; // TODO: выводить из имени приложения или из настроек

        private readonly IRabbitMQPersistentConnection persistentConnection;
        private readonly ILogger<EventBusRabbitMQ> logger;
        private readonly int retryCount;
        private readonly string queueName;

        private volatile IModel? consumerChannel; // TODO: нужен ли volatile и как его использовать правильно, к чему приведёт

        public EventBusRabbitMQ(
            IRabbitMQPersistentConnection persistentConnection,
            ILogger<EventBusRabbitMQ> logger,
            string queueName,
            int retryCount = 5)
        {
            this.persistentConnection = persistentConnection;
            this.logger = logger;
            this.retryCount = retryCount;
            this.queueName = queueName;
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = GetEventName(@event);
            var body = EventToByteArray(@event);

            this.publishInternal(@event.Id, eventName, body);
        }

        private void publishInternal(Guid eventId, string eventName, byte[] body)
        {
            this.doWithChannel(() =>
            {
                var policy = this.creatPublishEventPolicy(eventId);

                this.logger.LogTrace(
                    "Creating RabbitMQ channel to publish event: {EventId} ({EventName})",
                    eventId,
                    eventName);

                using var channel = this.persistentConnection.CreateModel();
                this.logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", eventId);

                DeclareEchange(channel);

                policy.Execute(() =>
                {
                    this.publishEventToChannel(channel, eventName, eventId, body);
                });
            });
        }

        private void doWithChannel(Action action)
        {
            var policy = Policy.Handle<AlreadyClosedException>() // AlreadyClosedException  - TODO: проверить что тот тип ошибки
                .WaitAndRetry(this.retryCount,
                              retryAttemt =>
                              {
                                  return TimeSpan.FromSeconds(Math.Pow(2, retryAttemt));
                              },
                              (ex, time) =>
                              {
                                  this.logger.LogWarning(
                                      ex,
                                      "Working with closed channel: after {Timeout}s ({ExceptionMessage})",
                                      $"{time.TotalSeconds:n1}",
                                      ex.Message);
                              });

            policy.Execute(action);
        }

        public void Subscribe<T, TH>() 
            where T : IntegrationEvent 
            where TH: IIntegrationEventHandler<T>
        {

        }

        private void createConsumerChannel()
        {
            this.logger.LogTrace("Creating RabbitMQ consumer channel");

            var channel = this.persistentConnection.CreateModel();
            // TODO: что если, в этот момент канал уже умер?
            DeclareEchange(channel);

            // TODO: проверить, придут ли сообщения, которые были отправлены до подписки.
            // обработка повторых сообщений
            channel.QueueDeclare(this.queueName, true, false, false, null);

            channel.CallbackException += this.channel_CallbackException;


            this.consumerChannel = channel;

        }

        private void channel_CallbackException(object? sender, RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void DeclareEchange(IModel channel)
        {
            channel.ExchangeDeclare(exchange: BROKER_NAME, type: ExchangeType.Direct);
        }

        private static byte[] EventToByteArray(IntegrationEvent @event)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                @event,
                @event.GetType(),
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        private static string GetEventName(IntegrationEvent @event)
        {
            return @event.GetType().Name;
        }

        private void publishEventToChannel(IModel channel, string eventName, Guid eventId, byte[] body)
        {
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            this.logger.LogTrace("Publish event to RabbitMQ: {EventID}", eventId);

            channel.BasicPublish(
                exchange: BROKER_NAME,
                routingKey: eventName,
                mandatory: true,
                basicProperties: properties,
                body: body);
        }

        private RetryPolicy creatPublishEventPolicy(Guid eventId)
        {
            return Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(this.retryCount,
                              retryAttemt =>
                              {
                                  return TimeSpan.FromSeconds(Math.Pow(2, retryAttemt));
                              },
                              (ex, time) =>
                              {
                                  this.logger.LogWarning(
                                      ex,
                                      "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})",
                                      eventId,
                                      $"{time.TotalSeconds:n1}",
                                      ex.Message);
                              });
        }
    }
}
