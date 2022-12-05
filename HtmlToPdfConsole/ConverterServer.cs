namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public class ConverterServer
    {
        /// <summary>
        /// Хранилище файлов
        /// </summary>
        private FileStorage _fileStorage = new FileStorage();
        private ProcessStorage _processStorage = new ProcessStorage();
        private MessageHab _messageHab = new MessageHab();


        /// <summary>
        /// Событие завершения процесса. Аргумент - идентификатор процесса
        /// </summary>
        public event Action<string> ProcessFinished;

        public ConverterServer()
        {
            _messageHab.ProcessFinished += OnProcessFinished;
        }

        /// <summary>
        /// Начинает процесс конвертации
        /// </summary>
        /// <param name="htmlFilePath">Файл</param>
        /// <returns>Идентификатор процесса</returns>
        public string StartConvert(string htmlFilePath)
        {
            var processItem = this.prepareProcess(htmlFilePath);
            this.runProcess(processItem);
            return processItem.processId;
        }

        /// <summary>
        /// Сохраняет файл. Сохраняет информацию о созданном Процессе. Возвращает идентификатор Процесса
        /// </summary>
        /// <param name="htmlFilePath">Файл для Процесса</param>
        /// <returns>Идентификатор процесса</returns>
        private ProcessItem prepareProcess(string htmlFilePath)
        {
            var processId = Guid.NewGuid().ToString();

            // TODO: Рассмотреть вариант сохранения файла в другом потоке,
            // чтобы как можно быстрее вернуть иднетификатор зарегистрированного процесса
            var savedFilePath = _fileStorage.SaveFile(htmlFilePath, processId);
            var processItem = new ProcessItem()
            {
                htmlFilePath = savedFilePath,
                processId = processId
            };

            _processStorage.SaveProcess(processItem);
            return processItem;
        }

        // TODO: Разобраться в жизненном цикле
        /// <summary>
        /// Создаёт таск на отправку (синхронную?) события создания процесса, помечает Процесс, как начатый.
        /// </summary>
        /// <param name="processItem"></param>
        /// <returns>Таск</returns>
        private Task runProcess(ProcessItem processItem)
        {
            // TODO: Если процесс не удалось запустить, кто-то должен пробовать запустить его снова
            return Task.Run(() =>
            {
                _messageHab.StartProcess(processItem.htmlFilePath, processItem.processId);
                processItem.convertStartSended = true;
                processItem.convertStartSendedTime = DateTime.Now;

                _processStorage.SaveProcess(processItem);
            });
        }

        /// <summary>
        /// Получить результат выполнения Процесса
        /// </summary>
        /// <param name="processId">Идентификатор</param>
        /// <returns>Файл, реузльтат ваполнения Процесса</returns>
        /// <exception cref="ProcessNotFinishedException">Процесс не завершён</exception>
        /// <exception cref="ProcessNotFoundException">Процесс не найден</exception>
        public string GetPdfByProcessId(string processId)
        {
            var processItem = _processStorage.GetProcessById(processId);
            if (string.IsNullOrEmpty(processItem.pdfFilePath))
            {
                throw new ProcessNotFinishedException(processId);
            }

            return processItem.pdfFilePath;
        }

        /// <summary>
        /// Получить информацию о завершении процесса
        /// </summary>
        /// <param name="processId">Идентификатор процесса</param>
        /// <returns>true, false</returns>
        /// <exception cref="ProcessNotFoundException">Процесс не найден</exception>
        public bool IsConvertFinished(string processId)
        {
            var processItem = _processStorage.GetProcessById(processId);
            return !string.IsNullOrEmpty(processItem.pdfFilePath);
        }

        /// <summary>
        /// Обработчик завершения выполнения процесса
        /// </summary>
        /// <param name="processId">Идентификатор Процесса</param>
        /// <param name="pathToPdf">Результат выполнения Процесса</param>
        private void OnProcessFinished(string processId, string pathToPdf)
        {
            ProcessItem processItem;
            try
            {
                processItem = _processStorage.GetProcessById(processId);
            }
            catch (ProcessNotFoundException)
            {
                return;
            }

            processItem.pdfFilePath = pathToPdf;
            processItem.convertFinishRecived = true;
            processItem.convertFinishRecivedTime = DateTime.Now;

            _processStorage.SaveProcess(processItem);
            // TODO: Сообщить хабу, что сообщение обработано
        }
    }

    public class ProcessNotFinishedException: Exception
    {
        public ProcessNotFinishedException(string processId)
            : base($"Процесс с идентификатором \"{processId}\" не завершён")
        { }
    }
}
