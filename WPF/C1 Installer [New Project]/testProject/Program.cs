
using C1Installer.Core;
using C1Installer.Core.Models;
using C1Installer.Core.Utility;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace TestEditionCatalog
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Locale: {LocaleInfo.Key}");
            Console.WriteLine(new string('-', 40));

            Console.OutputEncoding = Encoding.UTF8;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };


            #region ReleaseVersionJsonSync()
            var sync = new ReleaseVersionJsonSync(@"C:\Program Files (x86)\MESCIUS\ComponentOne\version_data", "US", http);
            var updated = sync.SyncAllJsonFilesAsync().GetAwaiter().GetResult();
            Console.WriteLine(updated);
            #endregion

            #region ReadProductVersions()

            var combinedProduct = ProductVersionCatalogue.ReadProductVersions(
                @"C:\Program Files (x86)\MESCIUS\ComponentOne\version_data",
                "US"
            );

            foreach (var product in combinedProduct)
            {
                Console.WriteLine($"Product: {product.Name}");
                foreach (var v in product.Versions)
                {
                    Console.WriteLine("   -----------------------------");
                    Console.WriteLine($"   Version:              {v.Version}");
                    Console.WriteLine($"   DisplayVersion:       {v.DisplayVersion}");
                    Console.WriteLine($"   ToolBoxVersion:       {v.ToolBoxVersion}");
                    Console.WriteLine($"   C1LiveVersion:        {v.C1LiveVersion}");
                    Console.WriteLine($"   FrameWorkVersions:    {v.FrameWorkVersions}");
                    Console.WriteLine($"   DefaultCheckFrameworks: {v.DefaultCheckFrameWorks}");
                    Console.WriteLine($"   Source:               {v.Source}");
                }
            }

            #endregion


        }



    }
}
