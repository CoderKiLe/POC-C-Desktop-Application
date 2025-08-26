
using C1Installer.Core;
using C1Installer.Core.Utility;
using System.Reflection;
using System.Text;
using System.Net.Http;

namespace TestEditionCatalog
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("hello World");

            Console.WriteLine($"Locale: {LocaleInfo.Key}");
            Console.WriteLine(new string('-', 40));


            var products = OldProductVersionReaders.ReadOldProductsVersion("US");
            foreach (var product in products)
            {
                Console.WriteLine(product.Versions);
                foreach (var version in product.Versions)
                {
                    Console.WriteLine(version.DisplayVersion);
                }
            }

            // basically i ahv eto all this api with whatever

            //var newProducts = LegacyEditionVersionReader.GetLegacyProductVersions();
            //foreach(var p in newProducts)
            //{
            //    Console.WriteLine(p.Value);
            //    foreach(var pop in p.Value)
            //    {
            //        Console.WriteLine(pop);
            //    }
            //}

            Console.OutputEncoding = Encoding.UTF8;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };


            #region ReleaseVersionJsonSync()
            var sync = new ReleaseVersionJsonSync(@"C:\Program Files (x86)\MESCIUS\ComponentOne\version_data", "US", http);
            var updated = sync.SyncAllJsonFilesAsync().GetAwaiter().GetResult();
            Console.WriteLine(updated);
            #endregion

        }


        
    }
}
