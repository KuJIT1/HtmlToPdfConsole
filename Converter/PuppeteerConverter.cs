namespace Converter
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using PuppeteerSharp;

    public class PuppeteerConverter: IDisposable, IHtmlToPdfConverter
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

        public PuppeteerConverter()
        {
            using var browserFetcher = new BrowserFetcher();
            IBrowser browser;
            try
            {
                browserFetcher.DownloadAsync().Wait();
                browser = Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                throw;
            }

            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
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

    public interface IHtmlToPdfConverter
    {
        Task<string> ConvertAsync(string htmlFilePath);
    }
}
