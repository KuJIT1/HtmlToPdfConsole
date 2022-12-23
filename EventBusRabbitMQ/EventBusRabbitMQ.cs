namespace EventBusRabbitMQ
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;

    using EventBus;
    using EventBus.Abstractions;
    using EventBus.Events;

    using Microsoft.Extensions.Logging;

    using Polly;
    using Polly.Retry;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using RabbitMQ.Client.Exceptions;

    // TODO: изучить RabbitMQ на предмет полезных настрек
    public class EventBusRabbitMQ : IEventBus
    {
        const string BROKER_NAME = "converter_event_bus"; // TODO: выводить из имени приложения или из настроек

        private readonly IRabbitMQPersistentConnection persistentConnection;
        private readonly ILogger<EventBusRabbitMQ> logger;
        private readonly int retryCount;
        private readonly string queueName;
        private readonly IEventBusSubscriptionsManager subsManager;
        private readonly IServiceProvider serviceProvider;

        private volatile IModel? consumerChannel; // TODO: нужен ли volatile и как его использовать правильно, к чему приведёт

        private object lockObj = new();

        public EventBusRabbitMQ(
            IRabbitMQPersistentConnection persistentConnection,
            ILogger<EventBusRabbitMQ> logger,
            string queueName,
            IEventBusSubscriptionsManager subsManager,
            IServiceProvider serviceProvider,
            int retryCount = 5)
        {
            this.persistentConnection = persistentConnection ?? throw new ArgumentException(null, nameof(persistentConnection));
            this.logger = logger ?? throw new ArgumentException(null, nameof(logger));
            this.retryCount = retryCount;
            this.queueName = queueName;
            this.subsManager = subsManager ?? throw new ArgumentException(null, nameof(subsManager));
            this.serviceProvider = serviceProvider ?? throw new ArgumentException(null, nameof(serviceProvider));

            this.createConsumerChannel();
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = this.getEventName(@event);
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

        // Не потокобезопасно
        public void Subscribe<T, TH>() 
            where T : IntegrationEvent 
            where TH: IIntegrationEventHandler<T>
        {
            var eventName = this.subsManager.GetEventKey<T>(null);
            if (!this.subsManager.HasSubscriptionsForEvent(eventName))
            {
                this.doWithChannel(() =>
                {
                    // TODO: если можно подписаться несколько раз, то, кажется, лучше подписаться.
                    // TODO: Будет ли работать подписка после пересоздания consumerChannel?
                    this.consumerChannel.QueueBind(this.queueName, BROKER_NAME, eventName);
                });
            }

            this.logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);
        }

        private void createConsumerChannel()
        {
            this.logger.LogTrace("{IsNew} RabbitMQ consumer channel"
                , this.consumerChannel is null ? "Creating" : "Recreating");

            if (this.consumerChannel is { IsOpen: true })
            {
                this.logger.LogInformation("RabbitMQ consumer channel is still oppened");
                return;
            }

            var newChannelWasCreated = false;

            lock (this.lockObj)
            {
                if (this.consumerChannel is { IsOpen: true })
                {
                    this.logger.LogInformation("RabbitMQ consumer channel is still oppened");
                    return;
                }

                this.clearConsumerChannel(this.consumerChannel);

                this.doWithChannel(() => 
                {
                    IModel? newChannel = null;
                    try
                    {
                        newChannel = this.persistentConnection.CreateModel();
                        DeclareEchange(newChannel);

                        newChannel.QueueDeclare(this.queueName, true, false, false, null);

                        this.consumerChannel = newChannel;
                        this.consumerChannel.CallbackException += this.consumerChannelCallbackException;

                        var consumer = new AsyncEventingBasicConsumer(newChannel);

                        consumer.Received += this.consumerReceived;
                        newChannel.BasicConsume(this.queueName, false, consumer);
                        newChannelWasCreated = true;
                    }
                    catch (Exception ex)
                    {
                        this.clearConsumerChannel(newChannel);
                        this.logger.LogWarning(
                            ex, 
                            "RabbitMQ consumer channel was broken in creating process: {Meassage}", 
                            ex.Message);

                        throw;
                    }
                });
            }

            if (newChannelWasCreated && this.consumerChannel is not { IsOpen: true })
            {
                this.createConsumerChannel();
            }
        }

        private async Task consumerReceived(object sender, BasicDeliverEventArgs ea)
        {
            var eventName = ea.RoutingKey;
            var message = Encoding.UTF8.GetString(ea.Body.Span);
            try
            {
                await this.processEvent(eventName, message);
            }
            catch(Exception ex)
            {
                this.logger.LogWarning(ex, "----- ERROR Processing message \"{Message}\"", message);
                this.doWithChannel(() => this.consumerChannel!.BasicNack(ea.DeliveryTag, false, true));
            }

            this.doWithChannel(() => this.consumerChannel!.BasicAck(ea.DeliveryTag, false));
        }

        private async Task processEvent(string eventName, string message)
        {
            this.logger.LogTrace("Processing RabbitMQ event: {EventName}", eventName);

            var handlers = this.subsManager.GetHandlersForEvent(eventName);
            if (handlers is null)
            {
                this.logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
                return;
            }

            foreach(var handlerType in handlers)
            {
                var handler = this.serviceProvider.GetService(handlerType);
                if (handler is null)
                {
                    continue;
                }

                var eventType = this.subsManager.GetEventType(eventName);
                if (eventType is null)
                {
                    continue;
                }

                var integrationEvent = JsonSerializer.Deserialize(
                    message, 
                    eventType, 
                    new JsonSerializerOptions() 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                await Task.Yield(); // TODO: как это работает?
                await (Task)handlerType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.Handle))!
                    .Invoke(handler, new object[] { integrationEvent })!;
            }
        }

        private void clearConsumerChannel(IModel? consumerChannel)
        {
            if (consumerChannel is null)
            {
                return;
            }

            consumerChannel.CallbackException -= this.consumerChannelCallbackException;
            consumerChannel.Close();
            consumerChannel.Dispose();
        }

        private void consumerChannelCallbackException(object? sender, CallbackExceptionEventArgs e)
        {
            this.createConsumerChannel();
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

        private string getEventName(IntegrationEvent @event)
        {
            return this.subsManager.GetEventKey(@event);
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
