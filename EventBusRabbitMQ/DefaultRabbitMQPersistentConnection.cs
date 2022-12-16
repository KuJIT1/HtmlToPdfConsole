namespace EventBusRabbitMQ
{
    using System.Net.Sockets;

    using Microsoft.Extensions.Logging;

    using Polly;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using RabbitMQ.Client.Exceptions;

    /// <summary>
    /// Поддерживает постоянные соедиение, пытается переоткрыть его при ошибках соединения
    /// Копипаст из примера от Майкрософт
    /// </summary>
    public class DefaultRabbitMQPersistentConnection : IRabbitMQPersistentConnection
    {
        private readonly IConnectionFactory connectionFactory;
        private readonly ILogger<DefaultRabbitMQPersistentConnection> logger;
        private readonly int retryCount;
        private volatile IConnection? connection; //TODO: определить необходимость volatile и затраты на него
        private bool disposedValue;

        private readonly object syncRoot = new();

        public DefaultRabbitMQPersistentConnection(
            IConnectionFactory connectionFactory,
            ILogger<DefaultRabbitMQPersistentConnection> logger,
            int retryCount = 5)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentException(null, nameof(connectionFactory));
            this.logger = logger ?? throw new ArgumentException(null, nameof(logger));
            this.retryCount = retryCount;
        }

        public bool IsConnected => !this.disposedValue && this.isConnectionOpen(this.connection);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// Отличия: Что если, соедиениение пересоздаётся в этот момент?
        /// Мы не можем отличить, пытается ли в этот момент соединение пересоздаться или оно уже умерло и не воскреснет
        /// или вообще ещё не открывалось. Поэтому тут добавляю TryConnect для случая, когда соединение пересоздаётся
        public IModel CreateModel()
        {
            if (!this.IsConnected && !this.TryConnect())
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }

            return this.connection!.CreateModel();
        }

        /// <summary>
        /// Пытается подключается к брокеру
        /// </summary>
        /// <returns>Удачное ли подключение</returns>
        /// Отличие: не открывать соединение, если оно уже открыто. TODO: Проверить, достаточно ли IsConnected
        /// Освобождение существующего соединения при открытии нового
        /// Кажется логичным добавить такие проверки на IsConnected и volatile на this.connection для надёжности.
        /// Обработать случай, если TryConnect вызовут несколько раз. В том числе и по ошибке
        /// TODO: PolicyRegistry
        public bool TryConnect()
        {
            this.logger.LogInformation("RabbitMQ Client is trying to connect");

            if (this.IsConnected)
            {
                this.logger.LogInformation("RabbitMQ Client is still connected");
                return true;
            }

            lock (this.syncRoot)
            {
                if (this.IsConnected)
                {
                    this.logger.LogInformation("RabbitMQ Client is still connected");
                    return true;
                }

                var newConnection = this.tryCreateConnection();
                if (this.isConnectionOpen(newConnection))
                {
                    this.clearConnection(this.connection);
                    this.connection = newConnection;
                    this.subscribeConnection(this.connection!);

                    this.logger.LogInformation(
                        "RabbitMQ Client acquired a persistent connection to '{HostName}' and " +
                        "is subscribed to failure events",
                        this.connection!.Endpoint.HostName);

                    return true;
                }
                else
                {
                    this.logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");

                    return false;
                }
            }
        }

        public void Dispose()
        {
            if (this.disposedValue)
            {
                return;
            }

            this.disposedValue = true;
            this.clearConnection(this.connection);
        }

        private bool isConnectionOpen(IConnection? connection) => connection is { IsOpen: true };

        private IConnection? tryCreateConnection()
        {
            var policy = Policy<IConnection>.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(this.retryCount,
                                  retryAttempt =>
                                  {
                                      return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                                  },
                                  onRetry: (delegateResult, time) =>
                                  {
                                      this.logger.LogWarning(
                                          delegateResult.Exception,
                                          "RabbitMQ Client could not connect after {TimeOUt}s ({ExceptionMessage})",
                                          $"{time.TotalSeconds:n1}",
                                          delegateResult.Exception);
                                  });

            var connection = policy.Execute(() =>
            {
                return this.connectionFactory.CreateConnection();
            });

            return connection;
        }

        private void clearConnection(IConnection? connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                connection.ConnectionShutdown -= OnConnectionShutdown;
                connection.CallbackException -= OnCallbackException;
                connection.ConnectionBlocked -= OnConnectionBlocked;

                connection.Dispose();
            }
            catch(IOException ex) // TODO: Узнать почему эта ошибка может возникнуть
            {
                this.logger.LogCritical(ex.ToString());
            }
        }

        private void subscribeConnection(IConnection connection)
        {
            connection.ConnectionShutdown += OnConnectionShutdown;
            connection.CallbackException += OnCallbackException;
            connection.ConnectionBlocked += OnConnectionBlocked;
        }

        private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
        {
            if (this.disposedValue)
            {
                return;
            }

            this.logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");
            TryConnect();
        }

        private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
        {
            if (this.disposedValue)
            {
                return;
            }

            this.logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect..."); ;
            TryConnect();
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            if (this.disposedValue)
            {
                return;
            }

            this.logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
            TryConnect();
        }
    }
}
