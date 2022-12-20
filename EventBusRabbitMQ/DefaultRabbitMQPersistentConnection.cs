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
    public class DefaultRabbitMQPersistentConnection : IRabbitMQPersistentConnection, IDisposable
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

        private bool isConnected => !this.disposedValue && IsConnectionOpen(this.connection);

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
            if (!this.isConnected && !this.tryConnect())
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
        private bool tryConnect()
        {
            this.logger.LogInformation("RabbitMQ Client is trying to connect");

            if (this.isConnected)
            {
                this.logger.LogInformation("RabbitMQ Client is still connected");
                return true;
            }

            lock (this.syncRoot)
            {
                if (this.isConnected)
                {
                    this.logger.LogInformation("RabbitMQ Client is still connected");
                    return true;
                }

                var newConnection = this.tryCreateConnection();
                if (newConnection is not null)
                {
                    this.clearConnection(this.connection);
                    this.connection = newConnection;
                    this.subscribeConnection(this.connection!);

                    this.logger.LogInformation(
                        "RabbitMQ Client acquired a persistent connection to '{HostName}' and " +
                        "is subscribed to failure events",
                        this.connection!.Endpoint.HostName);
                }
                else
                {
                    this.logger.LogCritical("FATAL ERROR: {message}", "RabbitMQ connections could not be created and opened");
                    return false;
                }
            }

            return this.isConnected || this.tryConnect(); // На случай, если соединение создалось нормально, но умерло до подписок
        }

        public void Dispose()
        {
            if (this.disposedValue)
            {
                return;
            }

            this.disposedValue = true;
            this.clearConnection(this.connection);
            //GC.SuppressFinalize(this); TODO: Узнать, как правильно его использовать
        }

        private static bool IsConnectionOpen(IConnection? connection) => connection is { IsOpen: true };

        private IConnection? tryCreateConnection()
        {
            var policy = Policy<IConnection>.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(this.retryCount,
                                  retryAttempt =>
                                  {
                                      return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                                  },
                                  // TODO: деструктуризация для delegateResult.Exception ?
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
                connection.ConnectionShutdown -= onConnectionShutdown;
                connection.CallbackException -= onCallbackException;
                connection.ConnectionBlocked -= onConnectionBlocked;

                connection.Close(); // TODO: убедиться, что нужно закрывать соединение перед освобождением
                connection.Dispose();
            }
            catch(IOException ex) // TODO: Узнать почему эта ошибка может возникнуть
            {
                this.logger.LogCritical("FATAL ERROR: {message}", ex.ToString());
            }
        }

        private void subscribeConnection(IConnection connection)
        {
            connection.ConnectionShutdown += onConnectionShutdown;
            connection.CallbackException += onCallbackException;
            connection.ConnectionBlocked += onConnectionBlocked;
        }

        private void onConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
        {
            if (this.disposedValue)
            {
                return;
            }

            this.logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");
            tryConnect();
        }

        private void onCallbackException(object? sender, CallbackExceptionEventArgs e)
        {
            if (this.disposedValue)
            {
                return;
            }

            this.logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect..."); ;
            tryConnect();
        }

        private void onConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            if (this.disposedValue)
            {
                return;
            }

            this.logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
            tryConnect();
        }
    }
}
