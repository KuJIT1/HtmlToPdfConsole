namespace Converter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;


    /*
        есть постоянное хранилище, где хранятся задачи. Например таблица в БД: taskId, htmlFilePath, pdfFilePath.
        Пришла задача:
            1. Записываем в хранилище taskId, htmlFilePath
                1.1. Если запись с таким taskId существует, то 
                    1.1.1. Если pdfFilePath для этой записи есть, сигнализируем о завершении задачи
            2. Запускаем задачу, ожидаем завершения
            3. Сигнализируем о завершении задачи
            4. Отмечаем в хранилище pdfFilePath (отметка о сигнализации?)


           При запуске.
     */


    public class ConvertTaskWatcher: IDisposable
    {
        private readonly PuppeteerConverter _converter;
        private bool _disposedValue;

        async public static Task<ConvertTaskWatcher> CreateAsync()
        {
            var converter = await PuppeteerConverter.CreateAsync();
            return new ConvertTaskWatcher(converter);
        }

        private ConvertTaskWatcher(PuppeteerConverter converter)
        {
            _converter = converter;
        }

        async public Task StartConvertTaskAsync(string htmlFilePath, string taskId)
        {
            var pdfFilePath = await _converter.ConvertAsync(htmlFilePath);
            this.FinishConvertTask(taskId, pdfFilePath);
        }

        public void FinishConvertTask(string taskId, string pdfFilePath)
        {

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _converter.Dispose();
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
