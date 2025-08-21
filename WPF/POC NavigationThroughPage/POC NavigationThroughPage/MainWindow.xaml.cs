using POC_NavigationThroughPage.Pages;
using POC_NavigationThroughPage.ViewModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace POC_NavigationThroughPage
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            MainFrame.Content = _viewModel.CurrentPage;
            
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if(e.NewValue is NavigationItem navItem)
            {
                _viewModel.SelectedItem = navItem;
                MainFrame.Content = navItem.PageInstance;
            }
        }
    }
}