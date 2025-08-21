using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace POC_WPF_D_Reference
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }
    }

    public class TaskItem
    {
        public string Title { get; set; }
        public bool IsCompleted { get; set; }
        public string Priority { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class MainWindowViewModel
    {
        public ObservableCollection<TaskItem> Tasks { get; set; } = new ObservableCollection<TaskItem>();

        public MainWindowViewModel()
        {
            // Runtime data initialization
            //Tasks.Add(new TaskItem { Title = "Real Task 1", Priority = "Medium" });
            //Tasks.Add(new TaskItem { Title = "Real Task 2", DueDate = DateTime.Now.AddDays(3) });
        }
    }


}