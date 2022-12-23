namespace ConverterWorker
{
    using ConverterWorker.Extensions;

    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
                {
                    services.AddHostedService<Worker>(sp => 
                    {
                        var logger = sp.GetRequiredService<ILogger<Worker>>();
                        return new Worker(logger);
                    });
                })
            .AddEventBus()
            .AddHtmlToPdfConverter()
            .Build();

            host.Run();
        }
    }
}