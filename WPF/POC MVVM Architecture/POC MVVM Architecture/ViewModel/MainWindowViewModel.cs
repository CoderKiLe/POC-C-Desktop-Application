using POC_MVVM_Architecture.Model;
using POC_MVVM_Architecture.MVVM;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using POC_MVVM_Architecture.RelayCommand;

namespace POC_MVVM_Architecture.ViewModel
{
    internal class MainWindowViewModel : ViewModelBase
    {
        public ButtonRelayCommand AddCommand => new ButtonRelayCommand(execute => AddItem());
        public ObservableCollection<Item> Items {get; set;}
        public MainWindowViewModel()
        {
            Items = new ObservableCollection<Item>();
        }

        private void AddItem()
        {
            Items.Add(new Item 
            {
                Name = "Random item",
                SerialNumber = "Random serial", 
                Quantity = 1
            });
        }

        private Item selectedItem;

        public Item SelectedItem
        {
            get { return selectedItem; }
            set 
            { 
                selectedItem = value;
                OnPropertyChanged();
            }
        }
    }
}
