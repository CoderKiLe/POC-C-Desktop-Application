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

namespace POC_Events
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // let me declare a simple event 
        public static event Action<string> OnSomethingHappened;
        public MainWindow()
        {
            InitializeComponent();

            // create a subscriber to that event
            PageSwitchNotifier.PageSwitchChanged += PageSwitchNotifier_PageSwitchChanged; ;
        }

        private void PageSwitchNotifier_PageSwitchChanged(object? sender, PageTransitionEventArgs e)
        {
            PageLoadingBrother.Navigate(e.PageToBeLoaded);
        }
    }
}