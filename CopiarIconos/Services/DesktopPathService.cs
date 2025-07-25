using System;
using System.Collections.Generic;
using System.IO;

namespace CopiarIconos.Services
{
    public class DesktopPathService
    {
        public List<string> GetDesktopPaths(string userName)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(userName)) return paths;
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
            catch { }
            return paths;
        }
    }
}
