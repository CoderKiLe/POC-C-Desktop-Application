namespace C1Installer.Core.Models
{
    /// <summary>
    /// Represents a product (e.g., WinForms, WPF, Blazor) with its metadata and available versions.
    /// </summary>
    public class Product
    {
        public string Id { get; set; }             // e.g. "winFormsControls"
        public string Name { get; set; }           // e.g. "WinForms 控件"
        public string Description { get; set; }    // e.g. "适用于传统桌面应用程序的高级 UI 控件"
        public List<ProductVersion> Versions { get; set; } = new();
    }

    

}