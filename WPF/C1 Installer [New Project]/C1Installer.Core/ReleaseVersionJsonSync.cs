using C1Installer.Core.Constant;
using C1Installer.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace C1Installer.Core
{
    /// <summary>
    /// Syncs per-region metadata by comparing local vs remote SHA file over HTTP.
    /// If SHA differs or local missing, downloads both the SHA file and <see cref="JsonFileConstant.ReleaseVersionFileName"/>.
    /// If SHA matches but <see cref="JsonFileConstant.ReleaseVersionFileName"/> is missing, downloads the JSON file.
    /// </summary>
    public class ReleaseVersionJsonSync
    {
        private readonly string _localRoot;
        private readonly string _regionKey;
        private readonly HttpClient _http;

        public ReleaseVersionJsonSync(string localRoot, string regionKey, HttpClient httpClient)
        {
            _localRoot = (localRoot ?? throw new ArgumentNullException(nameof(localRoot)))
                         .TrimEnd('\\', '/');
            _regionKey = string.IsNullOrWhiteSpace(regionKey)
                ? throw new ArgumentException("Region key must be provided.", nameof(regionKey))
                : regionKey;
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Ensures local SHA matches the remote one for the region (HTTP).
        /// If different/missing, also downloads the JSON from HTTP → local.
        /// Returns true if any local file was created or updated.
        /// </summary>
        public async Task<bool> SyncReleaseVersionShaAsync()
        {
            var remoteRoot = CombineUri(JsonFileConstant.RemoteRootBase, _regionKey);
            var remoteShaUrl = CombineUri(remoteRoot, JsonFileConstant.Sha256FileNameRelease);
            var remoteJsonUrl = CombineUri(remoteRoot, JsonFileConstant.ReleaseVersionFileName);

            string localRegionDir = _localRoot;
            string localShaPath = Path.Combine(localRegionDir,_regionKey, JsonFileConstant.Sha256FileNameRelease);
            string localJsonPath = Path.Combine(localRegionDir, _regionKey, JsonFileConstant.ReleaseVersionFileName);
            Directory.CreateDirectory(localRegionDir);

            // Get remote SHA (throws FileNotFoundException on 404)
            string remoteSha = await GetRemoteStringTrimAsync(remoteShaUrl, JsonFileConstant.Sha256FileNameRelease);

            // Read local SHA if present
            string? localSha = File.Exists(localShaPath) ? ReadTrim(localShaPath) : null;

            // If equal and JSON exists locally, nothing to do
            if (!string.IsNullOrEmpty(localSha) &&
                string.Equals(localSha, remoteSha, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(localJsonPath))
            {
                return false;
            }

            // SHA differs or JSON missing → download JSON first (ensures file + content in sync), then write SHA
            await DownloadAtomicFileAsync(remoteJsonUrl, localJsonPath, JsonFileConstant.ReleaseVersionFileName);
            await AtomicWriteAllTextAsync(localShaPath, remoteSha);
            return true;
        }

        public async Task<bool> SyncAllJsonFilesAsync()
        {
            // Step 1: region-level SHA + ReleaseVersion.json
            bool regionUpdated = await SyncReleaseVersionShaAsync();

            // Step 2: validate each Release Configuration entry
            string localJsonPath = Path.Combine(_localRoot,_regionKey, JsonFileConstant.ReleaseVersionFileName);
            if (!File.Exists(localJsonPath))
                throw new FileNotFoundException($"Local {JsonFileConstant.ReleaseVersionFileName} not found after sync.", localJsonPath);

            var entries = JsonSerializer.Deserialize<List<ReleaseVersionEntry>>(File.ReadAllText(localJsonPath))
                          ?? new List<ReleaseVersionEntry>();

            bool anyUpdated = regionUpdated;
            foreach (var entry in entries)
            {
                if (await ReleaseConfigurationAsync(entry))
                    anyUpdated = true;
            }

            return anyUpdated;
        }

        private async Task<bool> ReleaseConfigurationAsync(ReleaseVersionEntry entry)
        {
            string entryDir = Path.Combine(_localRoot,_regionKey, entry.Id);
            Directory.CreateDirectory(entryDir);

            string localShaPath = Path.Combine(entryDir, JsonFileConstant.Sha256FileNameReleaseConfiguration);
            string localJsonPath = Path.Combine(entryDir, entry.Id + ".json");

            var remoteBase = CombineUri(JsonFileConstant.RemoteRootBase, _regionKey, entry.Id);
            var remoteShaUrl = CombineUri(remoteBase, JsonFileConstant.Sha256FileNameReleaseConfiguration);
            var remoteJsonUrl = CombineUri(remoteBase, entry.Id + ".json");

            // Get remote SHA over HTTP
            string remoteSha = await GetRemoteStringTrimAsync(remoteShaUrl, JsonFileConstant.Sha256FileNameReleaseConfiguration);

            string? localSha = File.Exists(localShaPath) ? ReadTrim(localShaPath) : null;

            // Mismatch or missing → download JSON + update SHA
            if (string.IsNullOrEmpty(localSha) ||
                !string.Equals(localSha, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                await DownloadAtomicFileAsync(remoteJsonUrl, localJsonPath, entry.Id + ".json");
                await AtomicWriteAllTextAsync(localShaPath, entry.Sha256);
                return true;
            }

            return false;
        }

        // ---------- Helpers ----------

        private static string ReadTrim(string path) => File.ReadAllText(path).Trim();

        private async Task<string> GetRemoteStringTrimAsync(Uri url, string logicalNameForErrors)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                throw new FileNotFoundException($"Remote {logicalNameForErrors} not found at {url}", url.ToString());
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
        }

        private async Task DownloadAtomicFileAsync(Uri url, string targetPath, string logicalNameForErrors)
        {
            var dir = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, Path.GetRandomFileName());

            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                throw new FileNotFoundException($"Remote {logicalNameForErrors} not found at {url}", url.ToString());
            resp.EnsureSuccessStatusCode();

            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await resp.Content.CopyToAsync(fs).ConfigureAwait(false);
            }

            if (File.Exists(targetPath))
            {
                var backup = targetPath + ".bak";
                File.Replace(tmp, targetPath, backup, ignoreMetadataErrors: true);
                SafeDelete(backup);
            }
            else
            {
                File.Move(tmp, targetPath);
            }
        }

        private static async Task AtomicWriteAllTextAsync(string targetPath, string content)
        {
            var dir = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, Path.GetRandomFileName());
            await File.WriteAllTextAsync(tmp, content).ConfigureAwait(false);

            if (File.Exists(targetPath))
            {
                var backup = targetPath + ".bak";
                File.Replace(tmp, targetPath, backup, ignoreMetadataErrors: true);
                SafeDelete(backup);
            }
            else
            {
                File.Move(tmp, targetPath);
            }
        }

        private static void SafeDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
        }

        private static Uri CombineUri(string baseUri, params string[] segments)
            => CombineUri(new Uri(EnsureTrailingSlash(baseUri), UriKind.Absolute), segments);

        private static Uri CombineUri(Uri baseUri, params string[] segments)
        {
            var u = baseUri;
            foreach (var s in segments)
            {
                var part = Uri.EscapeDataString(s.Trim('/'));
                u = new Uri(u, part + "/");
            }
            // Last segment might be a file (no trailing slash)
            var last = segments.Length > 0 ? segments[^1] : "";
            if (last.Contains('.')) // heuristic: looks like a file
            {
                // Rebuild without trailing slash for last segment
                var prefix = segments[..^1];
                var leaf = Uri.EscapeDataString(last.Trim('/'));
                var root = baseUri;
                foreach (var p in prefix)
                    root = new Uri(root, Uri.EscapeDataString(p.Trim('/')) + "/");
                return new Uri(root, leaf);
            }
            return u;
        }

        private static string EnsureTrailingSlash(string s) => s.EndsWith("/") ? s : s + "/";
    }
}
