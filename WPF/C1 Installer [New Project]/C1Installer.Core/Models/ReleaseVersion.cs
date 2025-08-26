using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C1Installer.Core.Models
{
    public class ReleaseVersionEntry
    {
        public string Id { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string? NewsURL { get; set; }
        public string? EULA { get; set; }
    }
}
