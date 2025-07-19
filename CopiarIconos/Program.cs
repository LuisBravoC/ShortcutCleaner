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
            var paths = new List<string>();
            try
            {
                var usersDir = new DirectoryInfo(@"C:\Users");
                var excludedDirs = new[] { "Public", "Default", "All Users", "Default User" };
                
                foreach (var userDir in usersDir.GetDirectories()
                    .Where(d => !excludedDirs.Contains(d.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    try
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
                    catch { }
                }
                paths.Add(@"C:\Users\Public\Desktop");
            }
            catch (Exception ex) { _logger.LogError(ex, "Error detectando escritorios"); }
            
            return paths.Where(Directory.Exists).Distinct().ToList();
        }

        private void ProcessIcons()
        {
            if (!Directory.Exists(_config.SourcePath))
            {
                _logger.LogWarning("Carpeta origen no existe: {SourcePath}", _config.SourcePath);
                return;
            }

            try
            {
                var validFiles = Directory.GetFiles(_config.SourcePath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsValidIconFile)
                    .ToArray();

                if (validFiles.Length == 0)
                {
                    _logger.LogDebug("No se encontraron archivos válidos");
                    return;
                }

                var desktopPaths = GetDesktopPaths(); // Varios escritorios
                int totalCopied = 0, totalExisting = 0, totalDeleted = 0;

                foreach (var desktopPath in desktopPaths)
                {
                    var (copied, existing) = CopyFiles(validFiles, desktopPath);
                    var deleted = _config.EnableCleanup ? CleanupExtraFiles(validFiles, desktopPath) : 0;
                    totalCopied += copied; totalExisting += existing; totalDeleted += deleted;
                }
                
                if (totalCopied > 0 || totalDeleted > 0)
                {
                    try { SHChangeNotify(0x8000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
                    _logger.LogInformation("Completado: {Copied} copiados, {Existing} ya existían, {Deleted} eliminados", 
                        totalCopied, totalExisting, totalDeleted);
                }
                else if (totalExisting > 0)
                {
                    _logger.LogDebug("Completado: {Existing} archivos ya existen", totalExisting);
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Error procesando iconos"); 
            }
        }

        private bool IsValidIconFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (!_config.AllowedExtensions.Contains(extension)) return false;

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

        private int CleanupExtraFiles(string[] sourceFiles, string desktopPath)
        {
            if (sourceFiles == null || string.IsNullOrWhiteSpace(desktopPath)) return 0;
            
            var sourceFileNames = sourceFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int deleted = 0;
            
            try
            {
                var desktopFiles = Directory.GetFiles(desktopPath, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var desktopFile in desktopFiles)
                {
                    var fileName = Path.GetFileName(desktopFile);
                    if (string.IsNullOrEmpty(fileName) || sourceFileNames.Contains(fileName)) continue;
                    
                    try
                    {
                        var fileInfo = new FileInfo(desktopFile);
                        if (fileInfo.Exists)
                        {
                            // Remover atributos problemáticos de una vez
                            fileInfo.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden);
                            File.Delete(desktopFile);
                            _logger.LogInformation("Eliminado: {FileName} de {Desktop}", fileName, desktopPath);
                            deleted++;
                        }
                    }
                    catch (UnauthorizedAccessException ex) { _logger.LogWarning("Sin permisos para eliminar {FileName}: {Error}", fileName, ex.Message); }
                    catch (IOException ex) { _logger.LogWarning("Archivo en uso, no se puede eliminar {FileName}: {Error}", fileName, ex.Message); }
                    catch (Exception ex) { _logger.LogError(ex, "Error eliminando {FileName}", fileName); }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error durante limpieza en {Desktop}: {Error}", desktopPath, ex.Message); }
            
            return deleted;
        }
    }

    public class IconMonitorConfig
    {
        public string SourcePath { get; set; } = @"C:\Windows\Setup\Files\iconos";
        public long MaxFileSizeBytes { get; set; } = 10485760; // 10MB
        public bool EnableCleanup { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 1;
        public string[] AllowedExtensions { get; set; } = [".lnk", ".ico", ".png", ".jpg", ".jpeg", ".bmp"];
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            
            builder.Services.AddSingleton(new IconMonitorConfig());
            builder.Services.AddHostedService<IconMonitorService>();
            builder.Services.AddWindowsService(options => options.ServiceName = "IconMonitorService");
            builder.Services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddEventLog();
            });

            await builder.Build().RunAsync();
        }
    }
}
