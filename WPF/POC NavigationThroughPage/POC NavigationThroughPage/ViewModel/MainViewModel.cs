using POC_NavigationThroughPage.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace POC_NavigationThroughPage.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<NavigationItem> NavigationItems { get; }

        private NavigationItem _selectedItem;
        public NavigationItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnPropertyChanged();
                    CurrentPage = _selectedItem?.PageInstance;
                }
            }
        }

        private Page _currentPage;
        public Page CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            NavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem { Header = "Home", PageInstance = new Page1() },
            new NavigationItem { Header = "Page", PageInstance = new Page2() },
            new NavigationItem { Header = "File", PageInstance = new Page3() },
            new NavigationItem { Header = "Settings", PageInstance = new Page4() }
        };

            SelectedItem = NavigationItems.First(); // Optional default
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NavigationItem
    {
        public string Header { get; set; }
        public Page PageInstance { get; set; }
    }

}
