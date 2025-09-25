using POC_Events.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace POC_Events.Controls
{
    /// <summary>
    /// Interaction logic for SideNavigationPanel.xaml
    /// </summary>
    public partial class SideNavigationPanel : UserControl
    {
        public SideNavigationPanel()
        {
            InitializeComponent();
        }


        private void Page1_Click(object sender, RoutedEventArgs e)
        {
            PageSwitchNotifier.RaisePageSwitchChanged(new Page1());
        }

        private void Page2_Click(object sender, RoutedEventArgs e)
        {
            PageSwitchNotifier.RaisePageSwitchChanged(new Page2());

        }

        private void Page3_Click(object sender, RoutedEventArgs e)
        {
            PageSwitchNotifier.RaisePageSwitchChanged(new Page3());

        }

        private void Page4_Click(object sender, RoutedEventArgs e)
        {
            PageSwitchNotifier.RaisePageSwitchChanged(new Page1());

        }
    }
}
