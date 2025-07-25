using System;
using System.Collections.Generic;
using System.IO;
using CopiarIconos.Helpers;
using Microsoft.Extensions.Logging;

namespace CopiarIconos.Services
{
    public class FileCopyService
    {
        private readonly ILogger<FileCopyService> _logger;

        public FileCopyService(ILogger<FileCopyService> logger)
        {
            _logger = logger;
        }

        public (int copied, int existing) CopyFiles(string[] sourceFiles, string desktopPath, string hostname)
        {
            if (sourceFiles == null || string.IsNullOrWhiteSpace(desktopPath)) return (0, 0);
            int copied = 0, existing = 0;
            foreach (var sourceFile in sourceFiles)
            {
                if (string.IsNullOrWhiteSpace(sourceFile)) continue;
                var fileName = Path.GetFileName(sourceFile);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                if (!HostnameHelper.IsFileAllowedForHostname(fileName, hostname))
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
}
