using System;
using System.Collections.Generic;
using System.IO;
using CopiarIconos.Models;

namespace CopiarIconos.Helpers
{

    public static class HostnameHelper
    {
        public static Dictionary<char, string> TypeByLetter { get; private set; } = new();
        public static Dictionary<char, HashSet<string>> AllowedFilesByLetter { get; private set; } = new();

        public static void InitFromConfig(HostnameConfigModel config)
        {
            var typeDict = new Dictionary<char, string>();
            foreach (var typeEntry in config.TypeByLetter)
            {
                if (!string.IsNullOrEmpty(typeEntry.Key) && typeEntry.Key.Length == 1)
                    typeDict[typeEntry.Key[0]] = typeEntry.Value;
            }
            TypeByLetter = typeDict;

            var allowedDict = new Dictionary<char, HashSet<string>>();
            if (config.AllowedFilesByLetter != null)
            {
                foreach (var allowedEntry in config.AllowedFilesByLetter)
                {
                    if (!string.IsNullOrEmpty(allowedEntry.Key) && allowedEntry.Key.Length == 1)
                        allowedDict[allowedEntry.Key[0]] = new HashSet<string>(allowedEntry.Value, StringComparer.OrdinalIgnoreCase);
                }
            }
            AllowedFilesByLetter = allowedDict;
        }

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
