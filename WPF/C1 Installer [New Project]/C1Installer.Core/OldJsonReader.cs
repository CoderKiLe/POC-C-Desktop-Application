using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using C1Installer.Core.Models;

namespace C1Installer.Core
{
    /// <summary>
    /// Reads legacy (old-structure) control panel JSON embedded as resources (US/JP/KR)
    /// and maps them into Product/ProductVersion. All versions produced here are tagged
    /// Source = ProductVersionSource.OldJson.
    /// </summary>
    public sealed class OldJsonReader: IProductVersionProvider
    {

        public IReadOnlyList<Product> GetProductVersion(ProductVersionQuery query)
        {
            if (string.IsNullOrWhiteSpace(query.RegionKey))
                throw new ArgumentException("LocaleKey is required for old reader.");
            return OldJsonReader.ReadOldProductsVersion(query.RegionKey);
        }


        /// <summary>
        /// Reads embedded control panel JSON by locale ("US", "JP", or "KR") and
        /// returns products with versions mapped into the provided Product/ProductVersion model.
        /// </summary>
        public static List<Product> ReadOldProductsVersion(string regionKey)
        {
            if (string.IsNullOrWhiteSpace(regionKey))
                throw new ArgumentException("localeKey must be US/JP/KR", nameof(regionKey));

            var resourceAssembly = typeof(OldJsonReader).Assembly;

            // Map locale to embedded file name
            var targetSuffix = regionKey.Trim().ToUpperInvariant() switch
            {
                "US" => "c1ControlPanelEN.json",
                "KR" => "c1ControlPanelKR.json", // KR uses the US format but its own file
                "JP" => "c1ControlPanelJP.json",
                _ => throw new NotSupportedException("Supported localeKey values: US, JP, KR.")
            };

            // Find embedded resource by suffix (avoids hardcoding namespace)
            var resourceName = resourceAssembly
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
                throw new FileNotFoundException($"Embedded resource not found: {targetSuffix}");

            using var stream = resourceAssembly.GetManifestResourceStream(resourceName)
                               ?? throw new IOException($"Unable to open embedded resource stream: {resourceName}");

            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            // EN & KR share schema; JP differs.
            return regionKey.Equals("JP", StringComparison.OrdinalIgnoreCase)
                ? ParseProducts_JP(root)
                : ParseProducts_EN_KR(root);
        }

        // ----------------------------
        // EN/KR PARSER (shared schema)
        // ----------------------------
        private static List<Product> ParseProducts_EN_KR(JsonElement root)
        {
            var products = new List<Product>();

            // Typical EN/KR legacy structure exposes a top-level "Editions" array with
            // Name, Description, LatestVersion, etc. Some builds may also include a "Versions" array per edition.
            if (TryGetPropertyCaseInsensitive(root, "Editions", out var editions) && editions.ValueKind == JsonValueKind.Array)
            {
                foreach (var ed in editions.EnumerateArray())
                {
                    var prod = new Product
                    {
                        Id = Slug(GetStringCI(ed, "Id") ?? GetStringCI(ed, "Name") ?? "product"),
                        Name = GetStringCI(ed, "Name") ?? GetStringCI(ed, "Title") ?? prodNameFallback(ed),
                        Description = GetStringCI(ed, "Description")
                    };

                    // Preferred: explicit versions list if present
                    if (TryGetPropertyCaseInsensitive(ed, "Versions", out var versionsArr) && versionsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in versionsArr.EnumerateArray())
                            prod.Versions.Add(ReadVersionObject(v)); // Source set inside
                    }
                    else
                    {
                        // Fallback: synthesize a single ProductVersion from "LatestVersion" (and siblings if present)
                        var latest = GetStringCI(ed, "LatestVersion");
                        if (!string.IsNullOrWhiteSpace(latest))
                        {
                            prod.Versions.Add(new ProductVersion
                            {
                                Version = latest,
                                DisplayVersion = GetStringCI(ed, "DisplayVersion") ?? ToDisplayVersion(latest),
                                ToolBoxVersion = GetStringCI(ed, "ToolBoxVersion"),
                                C1LiveVersion = GetStringCI(ed, "C1LiveVersion"),
                                FrameWorkVersions = GetStringCI(ed, "FrameWorkVersions"),
                                DefaultCheckFrameWorks = GetStringCI(ed, "DefaultCheckFrameWorks"),
                                Source = ProductVersionSource.OldJson
                            });
                        }
                    }

                    products.Add(prod);
                }

                return products;
            }

            // Secondary fallback: try a generic "Products" array if "Editions" is absent
            if (TryGetPropertyCaseInsensitive(root, "Products", out var prods) && prods.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in prods.EnumerateArray())
                {
                    var prod = new Product
                    {
                        Id = Slug(GetStringCI(p, "Id") ?? GetStringCI(p, "Name") ?? "product"),
                        Name = GetStringCI(p, "Name") ?? prodNameFallback(p),
                        Description = GetStringCI(p, "Description")
                    };

                    if (TryGetPropertyCaseInsensitive(p, "Versions", out var versions) && versions.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in versions.EnumerateArray())
                            prod.Versions.Add(ReadVersionObject(v)); // Source set inside
                    }

                    // If no versions, try "LatestVersion"
                    if (prod.Versions.Count == 0)
                    {
                        var latest = GetStringCI(p, "LatestVersion");
                        if (!string.IsNullOrWhiteSpace(latest))
                            prod.Versions.Add(new ProductVersion
                            {
                                Version = latest,
                                DisplayVersion = ToDisplayVersion(latest),
                                Source = ProductVersionSource.OldJson
                            });
                    }

                    products.Add(prod);
                }

                return products;
            }

            // Final fallback: scan any object children that look like an "edition/product"
            foreach (var prodLike in ScanForProductLikeObjects(root))
                products.Add(prodLike);

            return products;

            static string prodNameFallback(JsonElement el) =>
                GetStringCI(el, "SKU") ?? GetStringCI(el, "Key") ?? "Unnamed Product";
        }

        // ----------------------------
        // JP PARSER (different schema)
        // ----------------------------
        private static List<Product> ParseProducts_JP(JsonElement root)
        {
            var products = new List<Product>();

            // JP often nests product blocks keyed by product name/section, with "versions" inside.
            // Strategy:
            //  1) Look for an array/object under well-known keys (e.g., "Products", "Editions", "製品", etc.)
            //  2) Otherwise, do a recursive scan: any object that contains a property "versions" (any case)
            //     with an array of version-like objects will be treated as a product container.
            //  3) Product name is resolved from the nearest "name"/"title" (any case) or the object key,
            //     with id = slug(name).

            // Heuristic 1: explicit container candidates
            foreach (var candidateKey in new[] { "Products", "Editions", "製品", "プロダクト", "項目" })
            {
                if (TryGetPropertyCaseInsensitive(root, candidateKey, out var container))
                {
                    if (container.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in container.EnumerateArray())
                            TryAddProductFromJPNode(item, ref products);
                        return products;
                    }
                    if (container.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in container.EnumerateObject())
                            TryAddProductFromJPNode(prop.Value, ref products, preferredName: prop.Name);
                        return products;
                    }
                }
            }

            // Heuristic 2: fully generic recursive scan
            RecursiveScanForJPProducts(root, ref products);
            return products;
        }

        /// <summary>
        /// Fallback scanner: looks through a JsonElement for objects that resemble a product,
        /// meaning they have at least a Name/Id and maybe Versions/LatestVersion.
        /// </summary>
        private static IEnumerable<Product> ScanForProductLikeObjects(JsonElement root)
        {
            var results = new List<Product>();

            if (root.ValueKind == JsonValueKind.Object)
            {
                // If object has Name or Id, consider it a product-like node
                if (TryGetPropertyCaseInsensitive(root, "Name", out var _) ||
                    TryGetPropertyCaseInsensitive(root, "Id", out var _))
                {
                    var prod = new Product
                    {
                        Id = Slug(GetStringCI(root, "Id") ?? GetStringCI(root, "Name") ?? "product"),
                        Name = GetStringCI(root, "Name") ?? "Unnamed Product",
                        Description = GetStringCI(root, "Description")
                    };

                    if (TryGetPropertyCaseInsensitive(root, "Versions", out var versions) && versions.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in versions.EnumerateArray())
                            prod.Versions.Add(ReadVersionObject(v)); // Source set inside
                    }
                    else
                    {
                        var latest = GetStringCI(root, "LatestVersion");
                        if (!string.IsNullOrWhiteSpace(latest))
                            prod.Versions.Add(new ProductVersion
                            {
                                Version = latest,
                                DisplayVersion = ToDisplayVersion(latest),
                                Source = ProductVersionSource.OldJson
                            });
                    }

                    results.Add(prod);
                }

                // recurse deeper
                foreach (var prop in root.EnumerateObject())
                    results.AddRange(ScanForProductLikeObjects(prop.Value));
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                    results.AddRange(ScanForProductLikeObjects(item));
            }

            return results;
        }

        private static void TryAddProductFromJPNode(JsonElement node, ref List<Product> products, string? preferredName = null)
        {
            // Need a versions array on this node (or inside it)
            if (!TryGetPropertyCaseInsensitive(node, "Versions", out var vers) &&
                !TryGetPropertyCaseInsensitive(node, "versions", out vers))
            {
                // Not directly on this node; attempt to find nested
                if (!TryFindNestedVersions(node, out var nestedNode, out vers))
                    return;

                node = nestedNode; // use the nested node as product root
            }

            if (vers.ValueKind != JsonValueKind.Array)
                return;

            var displayName = preferredName
                              ?? GetStringCI(node, "Name")
                              ?? GetStringCI(node, "Title")
                              ?? GetStringCI(node, "製品名")
                              ?? "Unnamed Product";

            var product = new Product
            {
                Id = Slug(GetStringCI(node, "Id") ?? displayName),
                Name = displayName,
                Description = GetStringCI(node, "Description") ?? GetStringCI(node, "説明")
            };

            foreach (var v in vers.EnumerateArray())
                product.Versions.Add(ReadVersionObject(v)); // Source set inside

            products.Add(product);
        }

        private static void RecursiveScanForJPProducts(JsonElement el, ref List<Product> products, string? objectKey = null)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                // If the current object has versions, treat it as a product root
                if (TryGetPropertyCaseInsensitive(el, "Versions", out var vers) ||
                    TryGetPropertyCaseInsensitive(el, "versions", out vers))
                {
                    if (vers.ValueKind == JsonValueKind.Array)
                    {
                        var displayName = GetStringCI(el, "Name")
                                          ?? GetStringCI(el, "Title")
                                          ?? objectKey
                                          ?? "Unnamed Product";

                        var product = new Product
                        {
                            Id = Slug(GetStringCI(el, "Id") ?? displayName),
                            Name = displayName,
                            Description = GetStringCI(el, "Description") ?? GetStringCI(el, "説明")
                        };
                        foreach (var v in vers.EnumerateArray())
                            product.Versions.Add(ReadVersionObject(v)); // Source set inside

                        products.Add(product);
                        // continue scanning in case there are more
                    }
                }

                foreach (var p in el.EnumerateObject())
                    RecursiveScanForJPProducts(p.Value, ref products, p.Name);
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                    RecursiveScanForJPProducts(item, ref products, objectKey);
            }
        }

        // ----------------------------
        // Helpers
        // ----------------------------
        private static ProductVersion ReadVersionObject(JsonElement v)
        {
            // Accept both PascalCase and camel/case-insensitive keys
            return new ProductVersion
            {
                Version = GetStringCI(v, "Version") ?? GetStringCI(v, "version"),
                DisplayVersion = GetStringCI(v, "DisplayVersion") ?? GetStringCI(v, "displayversion"),
                ToolBoxVersion = GetStringCI(v, "ToolBoxVersion") ?? GetStringCI(v, "toolboxversion"),
                C1LiveVersion = GetStringCI(v, "C1LiveVersion") ?? GetStringCI(v, "c1liveversion"),
                FrameWorkVersions = GetStringCI(v, "FrameWorkVersions") ?? GetStringCI(v, "frameworkversions"),
                DefaultCheckFrameWorks = GetStringCI(v, "DefaultCheckFrameWorks") ?? GetStringCI(v, "defaultcheckframeworks"),
                Source = ProductVersionSource.OldJson
            };
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            // Fast path: exact
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
            value = default;
            return false;
        }

        private static string? GetStringCI(JsonElement obj, string name)
        {
            return TryGetPropertyCaseInsensitive(obj, name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }

        private static bool TryFindNestedVersions(JsonElement node, out JsonElement productNode, out JsonElement versionsArray)
        {
            // Depth-first search for a property called "versions"/"Versions" that is an array.
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyCaseInsensitive(node, "Versions", out versionsArray) ||
                    TryGetPropertyCaseInsensitive(node, "versions", out versionsArray))
                {
                    if (versionsArray.ValueKind == JsonValueKind.Array)
                    {
                        productNode = node;
                        return true;
                    }
                }

                foreach (var p in node.EnumerateObject())
                {
                    if (TryFindNestedVersions(p.Value, out productNode, out versionsArray))
                        return true;
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                {
                    if (TryFindNestedVersions(item, out productNode, out versionsArray))
                        return true;
                }
            }

            productNode = default;
            versionsArray = default;
            return false;
        }

        private static string Slug(string raw)
        {
            // Create id-like slug: keep letters/digits, convert spaces to '-', lowercase.
            var s = raw.Trim();
            s = Regex.Replace(s, @"[^\p{L}\p{Nd}]+", "-");
            s = Regex.Replace(s, @"-+", "-").Trim('-');
            return s.Length == 0 ? "product" : s.ToLowerInvariant();
        }

        private static string ToDisplayVersion(string version)
        {
            // Best-effort: "20251.1.710" -> "2025v1(710)"
            // Pattern: YYYYQ.build or YYYYQ.X.build
            // Extract year(4), quarter(1), build(last block)
            try
            {
                var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[0].Length >= 5)
                {
                    var year = parts[0].Substring(0, 4);
                    var q = parts[0][4];
                    var build = parts[^1];
                    return $"{year}v{q}({build})";
                }
            }
            catch { /* ignore */ }
            return version;
        }
    }
}
