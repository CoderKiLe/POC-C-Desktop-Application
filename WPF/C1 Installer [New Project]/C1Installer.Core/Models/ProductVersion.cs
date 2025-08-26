using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C1Installer.Core.Models
{
    /// <summary>
    /// Represents a version entry for a product.
    /// </summary>
    public class ProductVersion
    {
        public string Version { get; set; }            // e.g. "20251.1.710"
        public string DisplayVersion { get; set; }     // e.g. "2025v1(710)"
        public string ToolBoxVersion { get; set; }     // e.g. "20251.710"
        public string C1LiveVersion { get; set; }      // e.g. "20231.672"
        public string FrameWorkVersions { get; set; }  // e.g. "v68.0,v4.8,v4.6.2"
        public string DefaultCheckFrameWorks { get; set; } // e.g. "v68.0"
    }
}
