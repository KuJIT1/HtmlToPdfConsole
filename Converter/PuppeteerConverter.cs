namespace Converter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    using PuppeteerSharp;

    public class PuppeteerConverter: IDisposable
    {
        private readonly IBrowser _browser;
        private bool _disposedValue;

        async public static Task<PuppeteerConverter> CreateAsync()
        {
            using var browserFetcher = new BrowserFetcher();
            IBrowser browser;
            try
            {
                await browserFetcher.DownloadAsync();
                browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            }
            catch(Exception)
            {
                throw;
            }

            return new PuppeteerConverter(browser);
        }

        private PuppeteerConverter(IBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        }

        public async Task<string> ConvertAsync(string htmlFilePath)
        {
            if (!File.Exists(htmlFilePath))
            {
                throw new ArgumentException($"Файл не существует: {htmlFilePath}", nameof(htmlFilePath));
            }

            await using var page = await _browser.NewPageAsync();

            var uri = new Uri(htmlFilePath);
            await page.GoToAsync(uri.AbsoluteUri);

            var tempFileName = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Guid.NewGuid().ToString(), ".pdf"));
            await page.PdfAsync(tempFileName);

            return tempFileName;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _browser.Dispose();
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
