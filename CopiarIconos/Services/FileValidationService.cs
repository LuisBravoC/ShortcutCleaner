using System;
using System.IO;

namespace CopiarIconos.Services
{
    public class FileValidationService
    {
        public bool IsValidIconFile(string filePath, long maxFileSizeBytes)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            try
            {
                //var extension = Path.GetExtension(filePath).ToLowerInvariant();
                //if (!_config.AllowedExtensions.Contains(extension)) return false;
                
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > maxFileSizeBytes)
                    return false;
                var fileName = Path.GetFileName(filePath);
                return !string.IsNullOrWhiteSpace(fileName) &&
                       !fileName.Contains("..") &&
                       fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            }
            catch { return false; }
        }
    }
}
