using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CopiarIconos
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            int interval = 1;
            if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed >= 1 && parsed <= 1440)
                interval = parsed;

            var builder = Host.CreateApplicationBuilder(args);

            var hostnameConfigSection = builder.Configuration.GetSection("HostnameConfig");
            var hostnameConfig = hostnameConfigSection.Get<Models.HostnameConfigModel>();
            if (hostnameConfig != null)
                Helpers.HostnameHelper.InitFromConfig(hostnameConfig);
            else
                Console.Error.WriteLine("ERROR: No se encontró la sección HostnameConfig en appsettings.json.");

            builder.Services.AddSingleton(new IconMonitorConfig
            {
                CheckIntervalMinutes = interval
            });
            builder.Services.AddSingleton<Services.DesktopPathService>();
            builder.Services.AddSingleton<Services.FileValidationService>();
            builder.Services.AddSingleton<Services.CleanupService>();
            builder.Services.AddSingleton<Services.FileCopyService>();

            builder.Services.AddHostedService<IconMonitorService>();
            builder.Services.AddWindowsService(options => options.ServiceName = "IconMonitorService");
            builder.Services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddEventLog(eventLogSettings =>
                {
                    eventLogSettings.SourceName = "IconMonitorService";
                    eventLogSettings.LogName = "Application";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            });

            await builder.Build().RunAsync();
        }
    }
}
