namespace EventBusRabbitMQ
{
    using System;

    using RabbitMQ.Client;

    /// <summary>
    /// 
    /// </summary>
    /// Непонятно для чего публичный TryConnect и IsConnected.
    /// В голову приходит только то, что могут пригодиться для оптимизации.
    /// С другой стороны, как будто они могут быть инкапсулированы.
    /// В примере используется только в связке !IsConnected => TryConnect.
    /// Что можно инкапсуилровать в CreateModel
    public interface IRabbitMQPersistentConnection
    {
      //  bool IsConnected { get; }

      //  bool TryConnect();

        IModel CreateModel();
    }
}
