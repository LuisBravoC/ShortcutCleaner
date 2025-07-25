using System;
using System.IO;

namespace CopiarIconos.Services
{
    public class FileValidationService
    {
        private readonly ILogger<FileValidationService> _logger;
        public FileValidationService(ILogger<FileValidationService> logger)
        {
            _logger = logger;
        }

        public bool IsValidIconFile(string filePath, long maxFileSizeBytes)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            try
            {
                //var extension = Path.GetExtension(filePath).ToLowerInvariant();
                //if (!_config.AllowedExtensions.Contains(extension)) return false;
                
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > maxFileSizeBytes)
                {
                    _logger.LogWarning("Archivo {FileName} excede el tamaño máximo ({FileSize} bytes)", fileInfo.Name, fileInfo.Length);
                    return false;
                }
                var fileName = Path.GetFileName(filePath);
                bool valid = !string.IsNullOrWhiteSpace(fileName) &&
                             !fileName.Contains("..") &&
                             fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
                if (!valid)
                    _logger.LogWarning("Archivo {FileName} no es un archivo válido", fileName);
                return valid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando archivo {FilePath}", filePath);
                return false;
            }
        }
    }
}
