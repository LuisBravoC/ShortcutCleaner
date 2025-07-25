using System;
using System.Collections.Generic;
using System.IO;

namespace CopiarIconos.Helpers
{
    public static class HostnameHelper
    {
        public static readonly Dictionary<char, string> TypeByLetter = new()
        {
            { 'C', "Caja" },
            { 'M', "Muebles" },
            // Agrega más tipos si es necesario
        };

        public static readonly Dictionary<char, HashSet<string>> AllowedFilesByLetter = new()
        {
            { 'C', new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Punto de Venta.lnk" } },
            { 'M', new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Muebles.lnk" } }
        };

        public static char GetTypeLetter(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname) || hostname.Length < 5)
                return '\0';
            return hostname[4];
        }

        public static string GetTypeName(string hostname)
        {
            char letter = GetTypeLetter(hostname);
            return TypeByLetter.TryGetValue(letter, out var type) ? type : "Desconocido";
        }

        public static bool IsFileAllowedForHostname(string fileName, string hostname)
        {
            var allowedLetters = new List<char>();
            foreach (var typeFiles in AllowedFilesByLetter)
            {
                if (typeFiles.Value.Contains(fileName))
                    allowedLetters.Add(typeFiles.Key);
            }
            if (allowedLetters.Count > 0)
            {
                char letter = GetTypeLetter(hostname);
                return allowedLetters.Contains(letter);
            }
            return true;
        }
    }
}
