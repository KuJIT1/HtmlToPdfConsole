namespace HtmlToPdfConsole
{
    using System;

    internal class ProcessItem
    {
        public string processId;
        public string htmlFilePath;
        public string pdfFilePath;

        public bool convertStartSended;
        public DateTime convertStartSendedTime;

        public bool convertFinishRecived;
        public DateTime convertFinishRecivedTime;
    }
}
