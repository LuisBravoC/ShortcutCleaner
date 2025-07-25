using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CopiarIconos.Services
{
    public class CleanupService
    {
        public int DeleteAllFilesFromDesktop(string desktopPath)
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
                        //_logger.LogWarning("No se pudo eliminar {FileName} de {Desktop}: {Error}", file, desktopPath, ex.Message);
                    }
                }
            }
            catch (Exception ex) 
            {
                //_logger.LogError(ex, "Error eliminando archivos del escritorio {Desktop}", desktopPath);
            }
            return deleted;
        }
    }
}
