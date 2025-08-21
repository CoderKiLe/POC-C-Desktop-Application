using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace POC_JsonDataParsingAndBinding
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MyTree> MyItems { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "featureTreeEN.json");
            string json = File.ReadAllText(jsonPath);
            MyItems = JsonTreeParser.Parse(json);

            DataContext = this;
        }
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is MyTree selectedItem && !string.IsNullOrEmpty(selectedItem.LaunchExe))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedItem.LaunchExe,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch: {ex.Message}");
                }
            }
        }
    }

    public static class JsonTreeParser
    {
        public static ObservableCollection<MyTree> Parse(string json)
        {
            var items = JsonSerializer.Deserialize<List<MyTree>>(json);
            return new ObservableCollection<MyTree>(items);
        }
    }

    public class MyTree
    {
        public string Name { get; set; }
        public string ActionName { get; set; }
        public int Level { get; set; }
        public string LaunchExe { get; set; }

        [JsonPropertyName("SubItems")]
        public ObservableCollection<MyTree> ChildItems { get; set; } = new();
    }
}