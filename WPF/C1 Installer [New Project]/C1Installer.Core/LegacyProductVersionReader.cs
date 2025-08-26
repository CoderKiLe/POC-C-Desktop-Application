using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using C1Installer.Core.Constant;
using C1Installer.Core.Utility;

namespace C1Installer.Core
{
    public static class LegacyEditionVersionReader
    {
        /// <summary>
        /// Reads the legacy control panel JSON (EN/JP) from embedded resources
        /// and returns a dictionary: Edition Name -> List of displayversion values.
        /// </summary>
        public static Dictionary<string, List<string>> GetLegacyProductVersions()
        {
            var fileName = JsonFileConstant.GetLegacyFileNameForLocale(LocaleInfo.Key);
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"C1Installer.Core.Data.Legacy.{fileName}")
                ?? throw new FileNotFoundException($"Embedded resource not found: {fileName}");

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return LocaleInfo.Key == "JP"
                ? ParseLegacyJP(json)
                : ParseLegacyUSorKR(json);
        }

        private static Dictionary<string, List<string>> ParseLegacyJP(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, List<string>>();

            var editions = doc.RootElement
                .GetProperty("Root")[0]
                .GetProperty("Products")[0]
                .GetProperty("Editions");

            foreach (var edition in editions.EnumerateArray())
            {
                var name = edition.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;

                // Skip "Common"
                if (string.Equals(name, "C1Common", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var versions = edition.TryGetProperty("Versions", out var vProp)
                    ? vProp.EnumerateArray()
                           .Select(v => v.TryGetProperty("DisplayVersion", out var dv) ? dv.GetString() : null)
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .ToList()
                    : new List<string>();

                dict[name] = versions;
            }
            return dict;
        }

        private static Dictionary<string, List<string>> ParseLegacyUSorKR(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, List<string>>();

            var editions = doc.RootElement.GetProperty("Editions");

            foreach (var edition in editions.EnumerateArray())
            {
                var name = edition.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;

                // Skip "Common"
                if (string.Equals(name, "Common", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var versions = edition.TryGetProperty("Versions", out var vProp)
                    ? vProp.EnumerateArray()
                           .Select(v => v.TryGetProperty("DisplayVersion", out var dv) ? dv.GetString() : null)
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .ToList()
                    : new List<string>();

                dict[name] = versions;
            }
            return dict;
        }

    }
}
