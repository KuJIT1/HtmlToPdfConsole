namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;

    internal class ProcessStorage : IProcessStorate
    {
        private Dictionary<string, ProcessItem> _innerStorage = new Dictionary<string, ProcessItem>();

        /// <summary>
        /// Сохраянет информацию о Процессе
        /// </summary>
        /// <param name="item">Информация о Процессе</param>
        /// <exception cref="ArgumentException">Если не задан идентификатор Процесса</exception>
        public void SaveProcess(ProcessItem item)
        {
            _innerStorage[item.processId ?? throw new ArgumentException()] = item;
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

    internal interface IProcessStorate
    {
        public void SaveProcess(ProcessItem item);

        public ProcessItem GetProcessById(string processId);
    }

    public class ProcessNotFoundException: Exception
    {
        public ProcessNotFoundException(string processId) 
            : base($"Не найден процесс по идентификатору: \"{processId}\"")
        {

        }
    }
}
