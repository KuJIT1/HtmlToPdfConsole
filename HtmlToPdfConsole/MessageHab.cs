namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class MessageHab
    {
        public void StartProcess(string htmlFilePath, string processId)
        {
            /* Отправить сообщение вида:
             * Начни процесс! processId, htmlFilePath
             * 
             * Получи подтверждение по отправке
            */
        }


        /* Ожидает сообщение вида:
         * Процесс завершён уачно! processId, pathToPdf
         * 
         * Процесс завершён неудочано! processId, Ошибка
        */

        public event Action<string, string> ProcessFinished;
    }
}
