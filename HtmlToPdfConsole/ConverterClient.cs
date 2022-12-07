namespace HtmlToPdfConsole
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class ConverterClient:IDisposable
    {
        // Список незавершённых процессов
        private Dictionary<string, string> _activeProcess = new Dictionary<string, string>();

        // Имитация обращения к серверу по сети.
        private ConverterServer _server = new ConverterServer();

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _disposedValue;

        private int _checkDeelay = 3000;

        public ConverterClient()
        {
            _server.ProcessFinished += OnConvertFinished;
        }

        /// <summary>
        /// Отправляет на сервер запрос на Процесс
        /// </summary>
        /// <param name="htmlFilePath">файл</param>
        public void AddConvertFile(string htmlFilePath)
        {
            var processId = _server.StartConvert(htmlFilePath);
            _activeProcess[processId] = htmlFilePath;
            _ = CheckIfConvertFinished(processId, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Обрабаботчик окончания Процесса
        /// </summary>
        /// <param name="processId">Идентификатор Процесса</param>
        /// <exception cref="ProcessNotFinishedException">Процесс не завершён</exception>
        /// <exception cref="ProcessNotFoundException">Процесс не найден</exception>
        private void OnConvertFinished(string processId)
        {
            if (!_activeProcess.ContainsKey(processId))
            {
                return;
            }

            var pdfFilePath = _server.GetPdfByProcessId(processId);
            ShowFinished(pdfFilePath, processId);
            _activeProcess.Remove(processId);
        }

        /// <summary>
        /// Обработчик результата выполнения Процесса
        /// </summary>
        /// <param name="pdfFilePath">Результат выполнения процесса</param>
        /// <param name="processId">Идентификатор процесса</param>
        private void ShowFinished(string pdfFilePath, string processId)
        {
            Console.WriteLine($"processId: {processId}; pdfFilePath: {pdfFilePath};");
        }

        /// <summary>
        /// Периодически опрашивает сервер о завершении процесса,
        /// обрабаотывает результат, когда процесс завершён
        /// </summary>
        /// <param name="processId">Идентификатор Процесса</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>Таск</returns>
        /// <exception cref="ProcessNotFoundException">Процесс не найден</exception>
        async private Task CheckIfConvertFinished(string processId, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_server.IsConvertFinished(processId))
                {
                    OnConvertFinished(processId);
                    return;
                }

                await Task.Delay(_checkDeelay, cancellationToken);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
