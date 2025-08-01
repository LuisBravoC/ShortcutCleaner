using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CopiarIconos.Services
{
    public class CleanupService
    {
        private readonly ILogger<CleanupService> _logger;
        public CleanupService(ILogger<CleanupService> logger)
        {
            _logger = logger;
        }

        public int DeleteAllFilesFromDesktop(string desktopPath)
        {
            if (string.IsNullOrWhiteSpace(desktopPath) || !Directory.Exists(desktopPath)) return 0;
            int deleted = 0;
            try
            {
                var files = Directory.GetFiles(desktopPath, "*.*", SearchOption.TopDirectoryOnly);

                // Eliminar archivos
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Exists)
                        {
                            fileInfo.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden);
                            File.Delete(file);
                            _logger.LogDebug("Eliminado: {FileName} de {Desktop}", file, desktopPath);
                            deleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo eliminar {FileName} de {Desktop}: {Error}", file, desktopPath, ex.Message);
                    }
                }
                
                var folders = Directory.GetDirectories(desktopPath, "*.*", SearchOption.TopDirectoryOnly);
                // Eliminar carpetas
                foreach (var folder in folders)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(folder);
                        if (dirInfo.Exists)
                        {
                            dirInfo.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden);
                            Directory.Delete(folder, true); // true = recursivo
                            _logger.LogDebug("Eliminada carpeta: {FolderName} de {Desktop}", folder, desktopPath);
                            deleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo eliminar carpeta {FolderName} de {Desktop}: {Error}", folder, desktopPath, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando archivos y carpetas del escritorio {Desktop}", desktopPath);
            }
            return deleted;
        }
    }
}
