using POC_RelayCommand_Button.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace POC_RelayCommand_Button.Commands
{
    public class ButtonCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public ButtonViewModel _button1ViewModel;

        public ButtonCommand(ButtonViewModel bvm)
        {
            _button1ViewModel = bvm;
        }
        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            _button1ViewModel.OnExecute();
        }
    }
}
