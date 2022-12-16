namespace HtmlToPdfConsole
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class ConverterServer
    {
        /// <summary>
        /// Хранилище файлов
        /// </summary>
        private IFileStorage _fileStorage = new FileStorage();
        private ProcessStorage _processStorage = new ProcessStorage();
        private MessageHab _messageHab = new MessageHab();
        private ProcessRunner _processRunner;


        /// <summary>
        /// Событие завершения процесса. Аргумент - идентификатор процесса
        /// </summary>
        public event Action<string> ProcessFinished;

        public ConverterServer()
        {
            _processRunner = new ProcessRunner(_processStorage, _messageHab);
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
            _processRunner.RunProcess(processItem);
            
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

            ProcessFinished(processId);
        }
    }

    public class ProcessNotFinishedException: Exception
    {
        public ProcessNotFinishedException(string processId)
            : base($"Процесс с идентификатором \"{processId}\" не завершён")
        { }
    }

    public class ProcessRunner: IDisposable
    {
        private ProcessStorage _processStorage;
        private MessageHab _messageHab;

        private BufferBlock<ProcessItem> _processItems;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool disposedValue;

        public ProcessRunner(ProcessStorage processStorage, MessageHab messageHab)
        {
            _processStorage = processStorage;
            _messageHab = messageHab;

            _ = StartSending();
        }

        public void RunProcess(ProcessItem item)
        {
            _processItems.Post(item);
        }

        private async Task StartSending()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var processItem = await _processItems.ReceiveAsync(_cancellationTokenSource.Token);
                if (processItem.convertStartSended)
                {
                    return;
                }

                await _messageHab.StartProcessAsync(processItem.htmlFilePath, processItem.processId);

                processItem.convertStartSended = true;
                processItem.convertStartSendedTime = DateTime.Now;

                await _processStorage.SaveProcessAsync(processItem);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
