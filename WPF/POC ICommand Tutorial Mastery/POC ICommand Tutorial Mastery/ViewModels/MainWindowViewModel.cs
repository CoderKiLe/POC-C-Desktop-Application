using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace POC_ICommand_Tutorial_Mastery.ViewModels
{
    // my view model class
    public class MainWindowViewModel
    {
        public ObservableCollection<NavigationModel> NavItems { get; set; }
        public ICommand SaveCommand { get; }  // command for the save button 
        public ICommand ItemSelectedCommand { get; } // command for the treeview items

        public MainWindowViewModel()
        {
            SaveCommand = new RelayCommand(
                execute: _ => Save(),
                canExecute: _ => CanSave()
            );

            ItemSelectedCommand = new RelayCommand(OnItemSelected);


            // initilizing some set of dat from the model that we all know

            NavItems = new ObservableCollection<NavigationModel>
            {
            new NavigationModel
            {
                Name = "Home",
                Children = new ObservableCollection<NavigationModel>
                {
                    new NavigationModel { Name = "Dashboard" },
                    new NavigationModel { Name = "Activity" }
                }
            },
            new NavigationModel
            {
                Name = "File",
                Children = new ObservableCollection<NavigationModel>
                {
                    new NavigationModel { Name = "Open" },
                    new NavigationModel { Name = "Save" },
                    new NavigationModel { Name = "Close" }
                }
            },
            new NavigationModel { Name = "System" }
        };
        }

        private void OnItemSelected(object parameter)
        {
            if(parameter is NavigationModel items)
                MessageBox.Show($"Selected Item: {items.Name}");
        }

        private void Save()
        {
            MessageBox.Show("Saving command running");
        }

        private bool CanSave()
        {
            bool isTime = DateTime.Now.Second % 2 == 0;
            return isTime; // button only works on the even seconds
        }
    }

    // Basic implementation of the relay command
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // this is reised when the canexecute changes
        // this time let's bind it to a button
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }

    // Model for my code
    public class NavigationModel
    {
        public string Name { get; set; }
        public ObservableCollection<NavigationModel> Children { get; set; }
    }



}
