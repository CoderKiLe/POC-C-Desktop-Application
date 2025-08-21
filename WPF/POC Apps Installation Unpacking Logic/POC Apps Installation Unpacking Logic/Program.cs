using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization;

namespace AppUnpackingLogic
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine(Environment.OSVersion); // Detect OS


            //var dirResolver = new DirectoryResolver();
            //dirResolver.GetDirectory("pgmsdata", "MESCIUS");

            string jsonpath = "Resources/Apps.json";
            var jsonFileHandler = new JsonFileHandler(jsonpath);

            var apps = await jsonFileHandler.ReadJsonFileAsync();


            var urls = apps.Apps
                .SelectMany(a => a.Directories)
                .SelectMany(d => d.Extracts)
                .Select(e => e.Url);

            foreach (var url in urls)
            {
                Console.WriteLine(url);
            }


            // Example inputs
            string archiveName = "C1FlexDesignerv48.7z";
            string extractPath = Path.Combine(@"C:\Artifacts", "C1FlexDesigner", "v4.8");
            string filepath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string downloadUrl = "https://gcpkg.blob.core.windows.net/c1installer/webinstaller/2025v1/Product/WinForms/v4.8/C1FlexDesignerv48.7z";

            //var apps = new ApplicationPackage(archiveName, extractPath, downloadUrl);

            //apps.CheckInstallDirectory();
            // Uncomment when needed
            // await apps.DownloadAppAsync();
            // apps.UnpackApp();
        }
    }

    public class JsonFileHandler
    {
        string _resourcePath;
        public JsonFileHandler(string resourcePath)
        {
            _resourcePath = resourcePath;
        }
        public async Task<AppsCollection> ReadJsonFileAsync()
        {
            string projectRoot = AppDomain.CurrentDomain.BaseDirectory;
            string appsJsonFullPath = Path.Combine(projectRoot, _resourcePath);

            if (File.Exists(appsJsonFullPath))
            {
                string jsonContent = await File.ReadAllTextAsync(appsJsonFullPath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<AppsCollection>(jsonContent, options);
            }
            else
            {
                throw new FileNotFoundException("Apps.json not found.", appsJsonFullPath);
            }
        }
    }

    /// <summary>
    /// returns the combined path taken from the json 
    /// pgmfls, 
    /// pgmdata and comfls whatever the naming convention is they use that rite
    /// </summary>
    public class DirectoryResolver
    {
        public string GetDirectory(string maindir, string subDir)
        {
            // just for my tutorial sake
            string testdir = "pgmdata";
            string testsubDir = "MESCIUS/ComponentOne/Apps/v4.8";
            string store;
            switch (testdir)
            {
                case "pgmdata":
                    store = Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), testsubDir);
                    Console.WriteLine(store);
                    break;

                default:
                    Console.WriteLine("[info] directory not found");
                    break;
            }
            return default;
        }

    }

    public class AppsCollection
    {
        [JsonPropertyName("Apps")]
        public List<AppInfo> Apps { get; set; }
    }

    public class AppInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string HelpLink { get; set; }
        public string SHA { get; set; }
        public string Image { get; set; }
        public string Icon { get; set; }
        public FetchLocation FetchLocation { get; set; }
        public List<AppDirectory> Directories { get; set; }
    }

    public class FetchLocation
    {
        public string Base { get; set; }
        public string Path { get; set; }
        public string Exe { get; set; }
    }

    public class AppDirectory
    {
        public string Base { get; set; }
        public string Path { get; set; }
        public bool? DetermineSha { get; set; } // optional
        public List<ExtractItem> Extracts { get; set; }
    }

    public class ExtractItem
    {
        public string Url { get; set; }
        public string ExtractVal { get; set; }
    }


    public class ApplicationPackage
    {
        private readonly string _archiveName;
        private readonly string _extractRoot;
        private readonly string _downloadUrl;

        public ApplicationPackage(string archiveName, string extractRoot, string downloadUrl)
        {
            _archiveName = archiveName ?? throw new ArgumentNullException(nameof(archiveName));
            _extractRoot = extractRoot ?? throw new ArgumentNullException(nameof(extractRoot));
            _downloadUrl = downloadUrl ?? throw new ArgumentNullException(nameof(downloadUrl));
        }

        public void CheckInstallDirectory()
        {
            if (!Directory.Exists(_extractRoot))
            {
                Console.WriteLine("[WARN] Directory not found.");
                return;
            }

            var exeFiles = Directory.GetFiles(_extractRoot, "*.exe", SearchOption.TopDirectoryOnly);

            if (exeFiles.Length == 0)
            {
                Console.WriteLine("[INFO] No executables found. Need to download and extract.");
                return;
            }

            Console.WriteLine("[INFO] Found executables:");
            foreach (var file in exeFiles)
                Console.WriteLine($" - {Path.GetFileName(file)}");

            LaunchApp(exeFiles.First());
        }

        private void LaunchApp(string exePath)
        {
            Console.WriteLine($"[INFO] Launching: {exePath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
            Console.WriteLine("[SUCCESS] Application launched.");
        }

        /// <summary>
        /// Download the app if it is not found in the dir location
        /// </summary>
        /// <returns>Task</returns>
        public async Task DownloadAppAsync()
        {
            Console.WriteLine("[INFO] Starting download...");

            string archivePath = GetArchivePath();
            EnsureTempDirectoryExists();

            try
            {
                using var response = await GetHttpResponseAsync();
                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                bool showProgress = totalBytes > 0;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);

                await DownloadToFileAsync(contentStream, fileStream, totalBytes, showProgress);

                Console.WriteLine("\n[SUCCESS] Download complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] Download failed: {ex.Message}");
            }
        }

        private string GetArchivePath() =>
            Path.Combine(Path.GetTempPath(), _archiveName);

        private void EnsureTempDirectoryExists()
        {
            Directory.CreateDirectory(Path.GetTempPath());
        }

        private async Task<HttpResponseMessage> GetHttpResponseAsync()
        {
            var client = new HttpClient();
            var response = await client.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return response;
        }

        private async Task DownloadToFileAsync(Stream contentStream, FileStream fileStream, long totalBytes, bool showProgress)
        {
            byte[] buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (showProgress)
                    ReportDownloadProgress(totalRead, totalBytes);
            }
        }

        private void ReportDownloadProgress(long totalRead, long totalBytes)
        {
            double percent = (totalRead * 100d) / totalBytes;
            Console.Write($"\r[DOWNLOAD] {percent:F2}%");
        }

        public void UnpackApp()
        {
            string archivePath = Path.Combine(Path.GetTempPath(), _archiveName);

            Console.WriteLine($"[INFO] Archive: {archivePath}");
            Console.WriteLine($"[INFO] Destination: {_extractRoot}");

            if (!File.Exists(archivePath))
            {
                Console.WriteLine($"[ERROR] Archive not found: {archivePath}");
                return;
            }

            Directory.CreateDirectory(_extractRoot);

            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            int total = entries.Count;
            int processed = 0;

            foreach (var entry in entries)
            {
                entry.WriteToDirectory(_extractRoot, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                processed++;
                ReportExtractionProgress(processed, total);
            }

            Console.WriteLine("[SUCCESS] Extraction completed.");
        }

        private void ReportExtractionProgress(int processed, int total)
        {
            int percent = (int)((double)processed / total * 100);
            Console.WriteLine($"[PROGRESS] {percent}% ({processed}/{total})");
        }

    }
}
