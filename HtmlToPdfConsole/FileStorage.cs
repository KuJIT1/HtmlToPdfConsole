namespace HtmlToPdfConsole
{
    using System;
    using System.IO;

    internal class FileStorage: IFileStorage
    {
        [Obsolete("Используется для отладки")]
        public string SaveFile(string filePath, string processId)
        {
            return filePath;
        }

        public string SaveFile(Stream filePath, string processId)
        {
            throw new NotImplementedException();
        }
    }

    internal interface IFileStorage
    {
        [Obsolete("Используется для отладки")]
        public string SaveFile(string filePath, string processId);

        public string SaveFile(Stream filePath, string processId);
    }
}
