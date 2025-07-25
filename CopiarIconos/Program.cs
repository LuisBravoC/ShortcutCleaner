using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CopiarIconos
{
    public class IconMonitorService : BackgroundService
    {
        private readonly ILogger<IconMonitorService> _logger;
        private readonly IconMonitorConfig _config;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public IconMonitorService(ILogger<IconMonitorService> logger, IconMonitorConfig config)
        {
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de Monitor de Iconos iniciado");

            _logger.LogInformation("HOSTNAME 1: {Hostname1}", _config.hostname);

            string hostnameType = HostnameHelper.GetTypeName(_config.hostname);
            _logger.LogInformation("Tipo de HOSTNAME : {TipoHostname}", hostnameType);

            string currentUser = SessionHelper.GetActiveSessionUser();
            _logger.LogInformation("Usuario interactivo activo: {User}", currentUser);

            try
            {
                ValidateConfiguration();
                await ExecuteMonitoringAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en el servicio");
                throw;
            }
        }

        private void ValidateConfiguration()
        {
            if (_config.CheckIntervalMinutes < 1 || _config.CheckIntervalMinutes > 1440)
                throw new ArgumentOutOfRangeException("El intervalo debe estar entre 1 y 1440 minutos");

            _logger.LogInformation("Configuración validada correctamente");
        }

        private async Task ExecuteMonitoringAsync(CancellationToken stoppingToken)
        {
            var desktopPaths = GetDesktopPaths();

            _logger.LogInformation("Servicio iniciado. Verificando cada {Interval} minuto(s)", _config.CheckIntervalMinutes);
            _logger.LogInformation("Origen: {Source}", _config.SourcePath);
            _logger.LogInformation("Escritorios detectados ({Count}):", desktopPaths.Count);
            foreach (var (desktop, index) in desktopPaths.Select((d, i) => (d, i + 1)))
                _logger.LogInformation("  {Index}. {Desktop}", index, desktop);
            _logger.LogInformation("Limpieza automática: {Cleanup}", _config.EnableCleanup ? "ACTIVADA" : "DESACTIVADA");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ProcessIcons();
                    await Task.Delay(TimeSpan.FromMinutes(_config.CheckIntervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Servicio detenido por solicitud");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante el procesamiento");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            
            _logger.LogInformation("Servicio detenido");
        }

        private List<string> GetDesktopPaths()
        {
            string usuarioActivo = SessionHelper.GetActiveSessionUser();
            if (string.IsNullOrWhiteSpace(usuarioActivo)) return new List<string>();
            string userName = usuarioActivo.Contains("\\") ? usuarioActivo.Split('\\')[1] : usuarioActivo;
            var paths = new List<string>();
            try
            {
                var userDir = new DirectoryInfo(@"C:\Users\" + userName);
                //_logger.LogInformation("Detectando escritorios para el usuario: {User}", userDir.FullName);
                if (userDir.Exists)
                {
                    var desktopPath = Path.Combine(userDir.FullName, "Desktop");
                    if (Directory.Exists(desktopPath)) paths.Add(desktopPath);

                    foreach (var oneDrive in userDir.GetDirectories("OneDrive*"))
                    {
                        foreach (var desktop in new[] { "Desktop", "Escritorio" })
                        {
                            var oneDriveDesktop = Path.Combine(oneDrive.FullName, desktop);
                            if (Directory.Exists(oneDriveDesktop)) paths.Add(oneDriveDesktop);
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error detectando escritorios"); }
            return paths.Where(Directory.Exists).Distinct().ToList();
        }

        private void ProcessIcons()
        {
            // Copiar archivos SOLO a los escritorios de usuario
            if (Directory.Exists(_config.SourcePath))
            {
                try
                {
                    var validFiles = Directory.GetFiles(_config.SourcePath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(IsValidIconFile)
                        .ToArray();

                    if (validFiles.Length > 0)
                    {
                        var desktopPaths = GetDesktopPaths(); // Escritorios usuario (NO público)
                        int totalCopied = 0, totalExisting = 0, totalDeleted = 0;

                        foreach (var desktopPath in desktopPaths)
                        {
                            // Eliminar todos los archivos del escritorio antes de copiar
                            int deleted = DeleteAllFilesFromDesktop(desktopPath);
                            totalDeleted += deleted;

                            var (copied, existing) = CopyFiles(validFiles, desktopPath);
                            totalCopied += copied; totalExisting += existing;
                        }

                        if (totalCopied > 0 || totalDeleted > 0)
                        {
                            _logger.LogInformation("Completado (Usuario): {Copied} copiados, {Deleted} eliminados", 
                                totalCopied, totalDeleted);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No se encontraron archivos válidos en {SourcePath}", _config.SourcePath);
                    }
                }
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "Error procesando iconos desde {SourcePath}", _config.SourcePath); 
                }
            }

            // Copiar archivos SOLO a escritorio público desde carpeta Public
            var publicDesktop = @"C:\Users\Public\Desktop";
            if (Directory.Exists(_config.publicSource) && Directory.Exists(publicDesktop))
            {
                try
                {
                    var publicFiles = Directory.GetFiles(_config.publicSource, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(IsValidIconFile)
                        .ToArray();

                    if (publicFiles.Length > 0)
                    {
                        int deleted = DeleteAllFilesFromDesktop(publicDesktop);
                        var (copied, existing) = CopyFiles(publicFiles, publicDesktop);
                        _logger.LogInformation("Completado (Public): {Copied} copiados, {Deleted} eliminados", copied, deleted);
                    }
                    else
                    {
                        _logger.LogDebug("No se encontraron archivos válidos en {SourcePath}", _config.publicSource);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando iconos públicos desde {SourcePath}", _config.publicSource);
                }
            }

            try { SHChangeNotify(0x8000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
        }

        // Elimina todos los archivos del escritorio de destino
        private int DeleteAllFilesFromDesktop(string desktopPath)
        {
            if (string.IsNullOrWhiteSpace(desktopPath) || !Directory.Exists(desktopPath)) return 0;
            int deleted = 0;
            try
            {
                var files = Directory.GetFiles(desktopPath, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Exists)
                        {
                            fileInfo.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden);
                            File.Delete(file);
                            //_logger.LogInformation("Eliminado: {FileName} de {Desktop}", file, desktopPath);
                            deleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo eliminar {FileName} de {Desktop}: {Error}", file, desktopPath, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando archivos del escritorio {Desktop}", desktopPath);
            }
            return deleted;
        }

        private bool IsValidIconFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                //if (!_config.AllowedExtensions.Contains(extension)) return false;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > _config.MaxFileSizeBytes)
                {
                    _logger.LogWarning("{FileName} excede tamaño máximo ({FileSize} bytes)", fileInfo.Name, fileInfo.Length);
                    return false;
                }

                var fileName = Path.GetFileName(filePath);
                return !string.IsNullOrWhiteSpace(fileName) && 
                       !fileName.Contains("..") && 
                       fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            }
            catch { return false; }
        }

        private (int copied, int existing) CopyFiles(string[] sourceFiles, string desktopPath)
        {
            if (sourceFiles == null || string.IsNullOrWhiteSpace(desktopPath)) return (0, 0);

            int copied = 0, existing = 0;
            foreach (var sourceFile in sourceFiles.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                var fileName = Path.GetFileName(sourceFile);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                // Verifica si el archivo está permitido para el hostname actual
                if (!HostnameHelper.IsFileAllowedForHostname(fileName, _config.hostname))
                {
                    _logger.LogWarning("Archivo {FileName} no permitido para hostname {Hostname}", fileName, _config.hostname);
                    continue;
                }

                var destinationFile = Path.Combine(desktopPath, fileName);
                if (File.Exists(destinationFile)) { existing++; continue; }

                try
                {
                    if (File.Exists(sourceFile))
                    {
                        File.Copy(sourceFile, destinationFile, false);
                        _logger.LogInformation("Copiado: {FileName} a {Desktop}", fileName, desktopPath);
                        copied++;
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Error copiando {FileName}", fileName); }
            }
            return (copied, existing);
        }

    }

    public class IconMonitorConfig
    {
        public string SourcePath { get; set; } = @"C:\Windows\Setup\Files\Iconos";
        public string publicSource { get; set; } = @"C:\Windows\Setup\Files\Public";
        public string hostname { get; set; } = Environment.MachineName; 
        public long MaxFileSizeBytes { get; set; } = 10485760; // 10MB
        public bool EnableCleanup { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 1;
        public string[] AllowedExtensions { get; set; } = [".lnk", ".ico", ".png", ".jpg", ".jpeg", ".bmp"];
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            int interval = 1;
            if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed >= 1 && parsed <= 1440)
                interval = parsed;

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton(new IconMonitorConfig
            {
                CheckIntervalMinutes = interval
            });
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
