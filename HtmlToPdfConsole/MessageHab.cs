namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using RabbitMQ.Client;

    internal class MessageHab
    {
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

            chanel.BasicPublish()
        }


        /* Ожидает сообщение вида:
         * Процесс завершён уачно! processId, pathToPdf
         * 
         * Процесс завершён неудочано! processId, Ошибка
        */

        public event Action<string, string> ProcessFinished;
    }
}
