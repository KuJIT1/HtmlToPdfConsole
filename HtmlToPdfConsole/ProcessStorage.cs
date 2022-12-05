namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class ProcessStorage
    {
        private Dictionary<string, ProcessItem> _innerStorage = new Dictionary<string, ProcessItem>();

        public void SaveProcess(ProcessItem item)
        {
            _ = item.processId ?? throw new ArgumentException();
            _innerStorage[item.processId] = item;
        }

        /// <summary>
        /// Получает информацию о Процесса по его идентификатору
        /// </summary>
        /// <param name="processId">Идентификатор процесса</param>
        /// <returns></returns>
        /// <exception cref="ProcessNotFoundException">Процесс с таким идентификатором не найден</exception>
        public ProcessItem GetProcessById(string processId)
        {
            if (!_innerStorage.TryGetValue(processId, out var processItem))
            {
                throw new ProcessNotFoundException(processId);
            }

            return processItem;
        }
    }

    public class ProcessNotFoundException: Exception
    {
        public ProcessNotFoundException(string processId) 
            : base($"Не найден процесс по идентификатору: \"{processId}\"")
        {

        }
    }
}
