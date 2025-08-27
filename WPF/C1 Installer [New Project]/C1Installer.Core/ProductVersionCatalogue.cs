using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using C1Installer.Core.Models;

namespace C1Installer.Core
{
    /// <summary>
    /// Static API for the GUI to fetch a unified product+version catalog.
    /// - Reads OLD (embedded) and NEW (disk) JSON.
    /// - Merges by Product.Name (canonical identity).
    /// - Dedupes versions by Version string; NewJson values take precedence.
    /// </summary>
    public static class ProductVersionCatalogue
    {
        /// <summary>
        /// Reads OLD + NEW product versions and returns a unified, merged list grouped by Product.Name.
        /// Both parameters are mandatory.
        /// </summary>
        /// <param name="localRoot">
        /// Base path for NEW JSON data (e.g., C:\Program Files (x86)\MESCIUS\ComponentOne\version_data).
        /// </param>
        /// <param name="regionKey">
        /// Region key used to pick the NEW JSON subfolder (e.g., "US", "JP", "KR").
        /// Also used as locale key for OLD JSON (embedded).
        /// </param>
        public static List<Product> ReadProductVersions(string localRoot, string regionKey)
        {
            if (string.IsNullOrWhiteSpace(localRoot))
                throw new ArgumentException("LocalRoot must be provided.", nameof(localRoot));
            if (string.IsNullOrWhiteSpace(regionKey))
                throw new ArgumentException("RegionKey must be provided.", nameof(regionKey));

            // 1) Get products from both sources
            var oldProducts = OldJsonReader.ReadOldProductsVersion(regionKey);
            var newProducts = NewJsonReader.ReadNewProductVersions(localRoot, regionKey);

            // 2) Merge by Product.Name (canonical identity)
            var all = oldProducts.Concat(newProducts);

            var merged = all
                .GroupBy(p => CanonicalName(p.Name), StringComparer.OrdinalIgnoreCase)
                .Select(g => MergeGroupByName(g.Key, g.ToList()))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return merged;
        }

        // --------------------------
        // Merge helpers
        // --------------------------

        private static Product MergeGroupByName(string canonicalName, List<Product> sameNameProducts)
        {
            var main = sameNameProducts
                .OrderByDescending(p => p.Versions.Any(v => v.Source == ProductVersionSource.NewJson))
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .First();

            var finalName = FirstNonEmpty(
                sameNameProducts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Name))?.Name,
                canonicalName
            )!;

            var idFromNew = sameNameProducts
                .FirstOrDefault(p => p.Versions.Any(v => v.Source == ProductVersionSource.NewJson))?.Id;
            var anyId = sameNameProducts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Id))?.Id;
            var finalId = FirstNonEmpty(idFromNew, anyId, Slug(finalName))!;

            var result = new Product
            {
                Id = finalId,
                Name = finalName,
                Description = main.Description
            };

            // Group versions by Version string
            var allVersions = sameNameProducts.SelectMany(p => p.Versions);
            var byVersion = new Dictionary<string, ProductVersion>(StringComparer.OrdinalIgnoreCase);

            foreach (var v in allVersions)
            {
                var verKey = CanonicalVersion(v.Version);
                if (string.IsNullOrEmpty(verKey)) continue;

                if (byVersion.TryGetValue(verKey, out var existing))
                {
                    // Merge — but do not backfill missing values
                    var merged = new ProductVersion
                    {
                        Version = FirstNonEmpty(v.Version, existing.Version),
                        DisplayVersion = PreferNewElseKeep(existing, v, pv => pv.DisplayVersion),
                        ToolBoxVersion = PreferNewElseKeep(existing, v, pv => pv.ToolBoxVersion),
                        C1LiveVersion = PreferNewElseKeep(existing, v, pv => pv.C1LiveVersion),
                        FrameWorkVersions = PreferNewElseKeep(existing, v, pv => pv.FrameWorkVersions),
                        DefaultCheckFrameWorks = PreferNewElseKeep(existing, v, pv => pv.DefaultCheckFrameWorks),
                        Source = (v.Source == ProductVersionSource.NewJson ||
                                                  existing.Source == ProductVersionSource.NewJson)
                                                 ? ProductVersionSource.NewJson
                                                 : existing.Source
                    };
                    byVersion[verKey] = merged;
                }
                else
                {
                    byVersion[verKey] = Clone(v);
                }
            }

            result.Versions = byVersion.Values
                .OrderByDescending(v => v.Source == ProductVersionSource.NewJson)
                .ThenByDescending(v => v.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result;
        }

        // -----------------
        // Helpers
        // -----------------
        private static string PreferNewElseKeep(ProductVersion existing, ProductVersion candidate, Func<ProductVersion, string?> selector)
        {
            // If candidate is NewJson and has a non-empty value, take it.
            if (candidate.Source == ProductVersionSource.NewJson && !string.IsNullOrWhiteSpace(selector(candidate)))
                return selector(candidate)!;

            // Else keep what we already had (old value), even if NewJson is empty
            return selector(existing) ?? string.Empty;
        }

        // --------------------------
        // Utility helpers
        // --------------------------
        private static string CanonicalName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var s = name.Trim();
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        private static string CanonicalVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) return string.Empty;
            return version.Trim();
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        private static string PreferNewThenOther(ProductVersion candidate, ProductVersion other, Func<ProductVersion, string?> selector)
        {
            var candVal = selector(candidate);
            var otherVal = selector(other);

            if (candidate.Source == ProductVersionSource.NewJson && !string.IsNullOrWhiteSpace(candVal))
                return candVal!;
            return FirstNonEmpty(otherVal, candVal) ?? string.Empty;
        }

        private static ProductVersion Clone(ProductVersion v) => new ProductVersion
        {
            Version = v.Version,
            DisplayVersion = v.DisplayVersion,
            ToolBoxVersion = v.ToolBoxVersion,
            C1LiveVersion = v.C1LiveVersion,
            FrameWorkVersions = v.FrameWorkVersions,
            DefaultCheckFrameWorks = v.DefaultCheckFrameWorks,
            Source = v.Source
        };

        private static string Slug(string raw)
        {
            var s = raw.Trim();
            s = Regex.Replace(s, @"[^\p{L}\p{Nd}]+", "-");
            s = Regex.Replace(s, @"-+", "-").Trim('-');
            return s.Length == 0 ? "product" : s.ToLowerInvariant();
        }
    }
}
