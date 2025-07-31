using System.Collections.Generic;

namespace CopiarIconos.Models
{
    public class HostnameConfigModel
    {
        public Dictionary<string, string> TypeByLetter { get; set; } = new();
        public Dictionary<string, List<string>> AllowedFilesByLetter { get; set; } = new();
    }
}
