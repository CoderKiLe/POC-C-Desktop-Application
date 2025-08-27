using C1Installer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C1Installer.Core
{
    public interface IProductVersionProvider
    {
        IReadOnlyList<Product> GetProductVersion(ProductVersionQuery query);
    }

    public sealed class ProductVersionQuery
    {
        public string? RegionKey { get; init; }             // For Old reader
        public string? LocalRoot { get; init; } // For New reader
    }


}
