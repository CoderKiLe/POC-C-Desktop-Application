using C1Installer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace C1Installer.Core
{

    /// <summary>
    /// Reads "new structure" JSON files from disk based on (localRoot, regionKey).
    /// 
    /// Expected directory layout:
    ///   {localRoot}\{regionKey}\ReleaseVersion.json
    ///   {localRoot}\{regionKey}\{releaseId}\{releaseId}.json
    ///
    /// Where ReleaseVersion.json is an array like:
    /// [
    ///   { "Id": "2025v2", "Sha256": "...", "NewsURL": "", "EULA": "" },
    ///   { "Id": "2026v1", "Sha256": "...", "NewsURL": "", "EULA": "" }
    /// ]
    ///
    /// And each {releaseId}\{releaseId}.json is an array like:
    /// [
    ///   { "id":"WinFormControls", "name": "WinForms Controls",
    ///     "versions": { "version":"2026v1(111)", "displayVersion":"...", "frameworkVersions":["v11.0"] } },
    ///   ...
    /// ]
    ///
    /// All produced ProductVersion entries are tagged as Source = ProductVersionSource.NewJson.
    /// </summary>
    /// 
    public sealed class NewJsonReader : IProductVersionProvider
    {
        /// <summary>
        /// Adapter entry for the common provider interface.
        /// Expects <see cref="ProductVersionQuery.LocalRoot"/> and <see cref="ProductVersionQuery.RegionKey"/> to be set.
        /// </summary>
        public IReadOnlyList<Product> GetProductVersion(ProductVersionQuery query)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));
            if (string.IsNullOrWhiteSpace(query.LocalRoot)) throw new ArgumentException("LocalRoot is required.", nameof(query));
            if (string.IsNullOrWhiteSpace(query.RegionKey)) throw new ArgumentException("RegionKey is required.", nameof(query));

            return ReadNewProductVersions(query.LocalRoot, query.RegionKey);
        }

        /// <summary>
        /// Reads the new JSON structure from the given local root + region key, merging all
        /// releases listed in ReleaseVersion.json into a single catalog of Products with Versions.
        /// </summary>

        public static List<Product> ReadNewProductVersions(string localRoot, string regionKey)
        {
            if (string.IsNullOrWhiteSpace(localRoot)) throw new ArgumentException("localRoot cannot be null/empty.", nameof(localRoot));
            if (string.IsNullOrWhiteSpace(regionKey)) throw new ArgumentException("regionKey cannot be null/empty.", nameof(regionKey));

            var baseDir = Path.Combine(TrimEndSlashes(localRoot), regionKey);
            var releaseListPath = Path.Combine(baseDir, "ReleaseVersion.json");

            if (!File.Exists(releaseListPath))
                throw new FileNotFoundException($"ReleaseVersion.json not found at: {releaseListPath}");

            using var fs = File.OpenRead(releaseListPath);
            using var doc = JsonDocument.Parse(fs);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("ReleaseVersion.json must be a JSON array.");

            var releaseIds = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // Accept "Id" or "id"
                if (TryGetPropertyCaseInsensitive(el, "Id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrWhiteSpace(id)) releaseIds.Add(id!);
                }
                else if (TryGetPropertyCaseInsensitive(el, "id", out idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrWhiteSpace(id)) releaseIds.Add(id!);
                }
            }

            // Merge all releases into a single product catalog (grouped by product Id)
            var byProductId = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

            foreach (var relId in releaseIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var relFolder = Path.Combine(baseDir, relId);
                var relFile = Path.Combine(relFolder, $"{relId}.json");
                if (!File.Exists(relFile))
                {
                    // Skip missing releases silently (or collect warnings in a higher layer)
                    continue;
                }

                using var rfs = File.OpenRead(relFile);
                using var rdoc = JsonDocument.Parse(rfs);
                var root = rdoc.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    // Unexpected, skip this release
                    continue;
                }

                foreach (var prodEl in root.EnumerateArray())
                {
                    var prodId = GetStringCI(prodEl, "id") ?? GetStringCI(prodEl, "Id");
                    var prodName = GetStringCI(prodEl, "name") ?? GetStringCI(prodEl, "Name");

                    if (string.IsNullOrWhiteSpace(prodId) && string.IsNullOrWhiteSpace(prodName))
                        continue; // Not a valid product entry

                    var normalizedId = Slug(prodId ?? prodName!);
                    if (!byProductId.TryGetValue(normalizedId, out var product))
                    {
                        product = new Product
                        {
                            Id = normalizedId,
                            Name = !string.IsNullOrWhiteSpace(prodName) ? prodName! : (prodId ?? "Unnamed Product"),
                            Description = null // not present in this schema
                        };
                        byProductId[normalizedId] = product;
                    }
                    else
                    {
                        // Prefer first discovered non-empty name
                        if (string.IsNullOrWhiteSpace(product.Name) && !string.IsNullOrWhiteSpace(prodName))
                            product.Name = prodName!;
                    }

                    // Read versions (supports both object and array for "versions")
                    if (TryGetPropertyCaseInsensitive(prodEl, "versions", out var versionsEl))
                    {
                        if (versionsEl.ValueKind == JsonValueKind.Object)
                        {
                            var pv = ReadVersionObjectFromNewSchema(versionsEl);
                            if (pv != null) product.Versions.Add(pv);
                        }
                        else if (versionsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in versionsEl.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.Object)
                                {
                                    var pv = ReadVersionObjectFromNewSchema(item);
                                    if (pv != null) product.Versions.Add(pv);
                                }
                            }
                        }
                    }
                }
            }

            // Optional: sort versions within each product (newest-looking first)
            foreach (var p in byProductId.Values)
            {
                p.Versions = p.Versions
                    .OrderByDescending(v => v.Version, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Return catalog ordered by product name
            return byProductId.Values
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ----------------------------
        // Helpers
        // ----------------------------

        private static ProductVersion? ReadVersionObjectFromNewSchema(JsonElement v)
        {
            // The "new" schema's versions block (singular or array item) looks like:
            // {
            //   "version":"2026v1(111)",
            //   "displayVersion":"dummy testing should not be updated locally",
            //   "frameworkVersions":["v11.0"]
            // }
            var version = GetStringCI(v, "version");
            var displayVersion = GetStringCI(v, "displayVersion");
            // frameworkVersions may be array or string (be permissive)
            var fw = ReadFrameworkVersionsAsCsv(v);

            if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(displayVersion) && string.IsNullOrWhiteSpace(fw))
                return null;

            // If only one is present, mirror it to keep both fields useful to UI logic.
            var effectiveVersion = string.IsNullOrWhiteSpace(version) ? displayVersion : version;
            var effectiveDisplay = !string.IsNullOrWhiteSpace(displayVersion) ? displayVersion : version ?? string.Empty;

            return new ProductVersion
            {
                Version = effectiveVersion,
                DisplayVersion = effectiveDisplay,
                ToolBoxVersion = null,
                C1LiveVersion = null,
                FrameWorkVersions = fw,
                DefaultCheckFrameWorks = FirstFramework(fw),
                Source = ProductVersionSource.NewJson
            };
        }

        private static string? ReadFrameworkVersionsAsCsv(JsonElement v)
        {
            if (TryGetPropertyCaseInsensitive(v, "frameworkVersions", out var fwEl))
            {
                if (fwEl.ValueKind == JsonValueKind.String)
                {
                    return fwEl.GetString();
                }
                if (fwEl.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in fwEl.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) list.Add(s!);
                        }
                    }
                    return list.Count > 0 ? string.Join(",", list) : null;
                }
            }
            return null;
        }

        private static string? FirstFramework(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return null;
            var first = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
        {
            value = default;
            if (obj.ValueKind != JsonValueKind.Object) return false;

            if (obj.TryGetProperty(name, out value))
                return true;

            foreach (var p in obj.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
            return false;
        }

        private static string? GetStringCI(JsonElement obj, string name)
        {
            return TryGetPropertyCaseInsensitive(obj, name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }

        private static string TrimEndSlashes(string s) => s.TrimEnd('\\', '/');

        private static string Slug(string raw)
        {
            var s = raw?.Trim() ?? string.Empty;
            s = Regex.Replace(s, @"[^\p{L}\p{Nd}]+", "-");
            s = Regex.Replace(s, @"-+", "-").Trim('-');
            return s.Length == 0 ? "product" : s.ToLowerInvariant();
        }




    }
}
