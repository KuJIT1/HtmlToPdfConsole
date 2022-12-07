namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public interface IConverterClient
    {
        string SendHtmlToConvert(string htmlFilePath);

        bool CheckIfConvertFinished(string processId);

        bool DownloadPdfFile(string processId);

        void OnConvertFinished(string processId);
    }
}
