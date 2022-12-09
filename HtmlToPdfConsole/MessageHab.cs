namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    public class MessageHab
    {
        private const string EXCHANGE_NAME = "ExchangeName";
        private const string PROCESS_START_QUEUE_NAME = "ProcessStart";
        private Channel<SendMessage> _messageChannel = Channel.CreateUnbounded<SendMessage>();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _callbackMapper =
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        private readonly ConcurrentDictionary<ulong, SendMessage> _outstendingConfirms = new ConcurrentDictionary<ulong, SendMessage>();

        private readonly IConnection _connection;
        private readonly IModel _channel;

        public MessageHab()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ConfirmSelect();

            _channel.BasicAcks += Chanel_BasicAcks;
            _channel.BasicNacks += Chanel_BasicNacks;

            _channel.ExchangeDeclare(EXCHANGE_NAME, "direct", true, false);
            _channel.QueueDeclare("", true, true, false); // Отправка?
        }

        public void StartProcess(string htmlFilePath, string processId)
        {
            /* Отправить сообщение вида:
             * Начни процесс! processId, htmlFilePath
             * 
             * Получи подтверждение по отправке
            */

            var factory = new ConnectionFactory() { HostName = "localhost" };
            var connection = factory.CreateConnection();

            var chanel = connection.CreateModel(); // На каждую отправку создавать свой chanell  ??

            chanel.ConfirmSelect();

            chanel.BasicAcks += Chanel_BasicAcks;
            chanel.BasicNacks += Chanel_BasicNacks;

        }

        public Task StartProcessAsync(string htmlFilePath, string processId)
        {
            _messageChannel.Writer.WriteAsync(new SendMessage() { Guid = processId, Path = htmlFilePath }); //Вроде должен синхронно записываться

            var tcs = new TaskCompletionSource<bool>();

            _callbackMapper.AddOrUpdate(processId,
                _ => tcs,
                (_, old) =>
                {
                    old.SetException(new ProcessResendedException());
                    return tcs;
                });

            return tcs.Task;
        }

        public async Task SendMessages()
        {
            while(await _messageChannel.Reader.WaitToReadAsync())
            {
                while (_messageChannel.Reader.TryRead(out var sendMessage))
                {
                    _outstendingConfirms.TryAdd(_channel.NextPublishSeqNo, sendMessage);
                    _channel.BasicPublish(EXCHANGE_NAME, PROCESS_START_QUEUE_NAME, body: sendMessage.ToBody());
                }
            }
        }

        private void Chanel_BasicNacks(object sender, BasicNackEventArgs e)
        {
            this.ProcessAckNack(e.Multiple, e.DeliveryTag, false);
        }

        private void Chanel_BasicAcks(object sender, BasicAckEventArgs e)
        {
            this.ProcessAckNack(e.Multiple, e.DeliveryTag, true);
        }

        private void ProcessAckNack(bool multiple, ulong index, bool ok)
        {
            if (multiple)
            {
                foreach (var id in _outstendingConfirms.Keys.Where(k => k <= index).ToArray())
                {
                    this.DoProcessAckNack(id, ok);
                }
            }
            else
            {
                this.DoProcessAckNack(index, ok);
            }
        }

        private void DoProcessAckNack(ulong index, bool ok)
        {
            if (_outstendingConfirms.TryRemove(index, out var sendMessage))
            {
                if (_callbackMapper.TryRemove(sendMessage.Guid, out var tcs))
                {
                    if (ok)
                    {
                        tcs.SetResult(true);
                    }
                    else
                    {
                        // Попытка переслать
                        // TODO: проверка на переполнение, если сплошные наки
                        _messageChannel.Writer.WriteAsync(sendMessage);
                    }
                }
            }
        }


        /* Ожидает сообщение вида:
         * Процесс завершён уачно! processId, pathToPdf
         * 
         * Процесс завершён неудочано! processId, Ошибка
        */

        public event Action<string, string> ProcessFinished;


        private class SendMessage
        {
            public string Guid;
            public string Path;

            public byte[] ToBody()
            {
                return Array.Empty<byte>();
            }
        }
    }

    public class ProcessResendedException: Exception
    {

    }
}
