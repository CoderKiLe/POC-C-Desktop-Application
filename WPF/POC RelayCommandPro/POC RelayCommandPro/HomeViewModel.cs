using System;
using System.Windows;
using System.Windows.Input;

namespace POC_RelayCommandPro
{
    public class HomeViewModel
    {
        public ICommand ShowMessageCommand { get; }

        public HomeViewModel()
        {
            ShowMessageCommand = new RelayCommand(
                execute: param => OnExecuteButtonClick(param),
                canExecute: param => CanShow(param)
            );
        }

        private bool CanShow(object param)
        {
            return true; // Or add parameter-based logic if needed
        }

        private void OnExecuteButtonClick(object param)
        {
            MessageBox.Show("I am clicked, dumbass"); // Still got bite 😄
        }
    }
}