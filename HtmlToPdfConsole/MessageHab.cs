namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class MessageHab
    {
        public void StartProcess(string htmlFilePath, string processId)
        {

        }

        public event Action<string, string> ProcessFinished;
    }
}
