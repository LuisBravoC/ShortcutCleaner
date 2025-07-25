using System;
using System.Collections.Generic;
using System.IO;

namespace CopiarIconos.Services
{
    public class DesktopPathService
    {
        private readonly ILogger<DesktopPathService> _logger;
        public DesktopPathService(ILogger<DesktopPathService> logger)
        {
            _logger = logger;
        }

        public List<string> GetDesktopPaths(string userName)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(userName)) return paths;
            try
            {
                var userDir = new DirectoryInfo(@"C:\Users\" + userName);
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
                //_logger.LogInformation("Detectando {Count} escritorios para el usuario: {User}", paths.Count, userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting desktops for user {User}", userName);
            }
            return paths;
        }
    }
}
