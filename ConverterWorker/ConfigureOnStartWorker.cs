namespace ConverterWorker
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class ConfigureOnStartWorker : BackgroundService
    {
        IServiceProvider serviceProvider;

        public ConfigureOnStartWorker(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }

    public class OnstartConfigurator
    {

    }
}
