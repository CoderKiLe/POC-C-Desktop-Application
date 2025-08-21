using System.Windows;
using System.Windows.Controls;

namespace POC_TreeViewNavigationPanle
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            NavigationTree.SelectedItemChanged += NavigationTree_SelectedItemChanged;
        }

        private void NavigationTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (NavigationTree.SelectedItem is TreeViewItem selectedItem)
            {
                // Handle selection change
                MessageBox.Show($"Selected: {selectedItem.Header}");
            }
        }
    }
}