namespace ConverterWorker.Extensions
{
    using Converter;

    using ConverterWorker.EventHandling;
    using ConverterWorker.Events;

    using EventBus;
    using EventBus.Abstractions;

    using EventBusRabbitMQ;

    using RabbitMQ.Client;

    public static class ConfigurationExtensionMethod
    {
        public static IHostBuilder AddEventBus(this IHostBuilder hostBuilder)
        {
            return hostBuilder
                .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                    var configuration = sp.GetRequiredService<IConfiguration>();

                    var factory = new ConnectionFactory()
                    {
                        HostName = configuration["EventBusConnection"],
                        DispatchConsumersAsync = true
                    };

                    if (!string.IsNullOrEmpty(configuration["EventBusUserName"]))
                    {
                        factory.UserName = configuration["EventBusUserName"];
                    }

                    if (!string.IsNullOrEmpty(configuration["EventBusPassword"]))
                    {
                        factory.Password = configuration["EventBusPassword"];
                    }

                    var retryCount = 5;
                    if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"]))
                    {
                        retryCount = int.Parse(configuration["EventBusRetryCount"]);
                    }

                    return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
                });

                services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                    var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                    var eventBusSubscriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                    var subscriptionClientName = configuration["SubscriptionClientName"];

                    var retryCount = 5;
                    if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"]))
                    {
                        retryCount = int.Parse(configuration["EventBusRetryCount"]);
                    }

                    return new EventBusRabbitMQ(
                        rabbitMQPersistentConnection,
                        logger,
                        subscriptionClientName,
                        eventBusSubscriptionsManager,
                        sp,
                        retryCount);
                });

                services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
            });
        }

        public static IHostBuilder AddHtmlToPdfConverter(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IHtmlToPdfConverter, PuppeteerConverter>(sp =>
                {
                    return new PuppeteerConverter();
                });

                var sp = services.BuildServiceProvider();
                var eventBus = sp.GetRequiredService<IEventBus>();
                eventBus.Subscribe<StartConvertHtmlFileIntegrationEvent, StartConvertHtmlFileIntegrationEventHandler>();
            });

            return hostBuilder;
        }
    }
}
