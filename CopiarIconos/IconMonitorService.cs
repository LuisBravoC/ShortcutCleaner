using CopiarIconos.Helpers;
using CopiarIconos.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace CopiarIconos
{
    public class IconMonitorService : BackgroundService
    {
        private readonly ILogger<IconMonitorService> _logger;
        private readonly IconMonitorConfig _config;
        private readonly Services.DesktopPathService _desktopPathService;
        private readonly Services.FileValidationService _fileValidationService;
        private readonly Services.CleanupService _cleanupService;
        private readonly Services.FileCopyService _fileCopyService;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public IconMonitorService(
            ILogger<IconMonitorService> logger,
            IconMonitorConfig config,
            Services.DesktopPathService desktopPathService,
            Services.FileValidationService fileValidationService,
            Services.CleanupService cleanupService,
            Services.FileCopyService fileCopyService)
        {
            _logger = logger;
            _config = config;
            _desktopPathService = desktopPathService;
            _fileValidationService = fileValidationService;
            _cleanupService = cleanupService;
            _fileCopyService = fileCopyService;
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
            var paths = _desktopPathService.GetDesktopPaths(userName);
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
                        .Where(f => _fileValidationService.IsValidIconFile(f, _config.MaxFileSizeBytes))
                        .ToArray();

                    if (validFiles.Length > 0)
                    {
                        var desktopPaths = GetDesktopPaths(); // Escritorios usuario (NO público)
                        int totalCopied = 0, totalExisting = 0, totalDeleted = 0;

                        foreach (var desktopPath in desktopPaths)
                        {
                            // Eliminar todos los archivos del escritorio antes de copiar
                            int deleted = _cleanupService.DeleteAllFilesFromDesktop(desktopPath);
                            totalDeleted += deleted;

                            var (copied, existing) = _fileCopyService.CopyFiles(validFiles, desktopPath, _config.hostname);
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
                        .Where(f => _fileValidationService.IsValidIconFile(f, _config.MaxFileSizeBytes))
                        .ToArray();

                    if (publicFiles.Length > 0)
                    {
                        int deleted = _cleanupService.DeleteAllFilesFromDesktop(publicDesktop);
                        var (copied, existing) = _fileCopyService.CopyFiles(publicFiles, publicDesktop, _config.hostname);
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
}