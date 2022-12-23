namespace ConverterWorker.Events
{
    using EventBus.Events;

    public record StartConvertHtmlFileIntegrationEvent: IntegrationEvent
    {
        public string HtmlFilePath { get; }

        public StartConvertHtmlFileIntegrationEvent(string htmlFilePath)
        {
            this.HtmlFilePath = htmlFilePath;
        }
    }
}