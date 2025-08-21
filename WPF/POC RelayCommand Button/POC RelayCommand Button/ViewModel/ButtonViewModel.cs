using POC_RelayCommand_Button.Commands;
using POC_RelayCommand_Button.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace POC_RelayCommand_Button.ViewModel
{
    public class ButtonViewModel
    {
        public ObservableCollection<ButtonModel> Buttons { get; }
        // using the command in the viewmodel
        public ICommand ClickCommand1 { get; }
        public ICommand ClickCommand2 { get; }

        public ButtonViewModel()
        {
            Buttons = new ObservableCollection<ButtonModel>()
            {
                new ButtonModel
                {
                    Label = "Save",
                    Command = new RelayCommand<object>
                    {

                    },
                    Parameter = "Button1"
                },
            }
        }
        public void OnExecute()
        {
            MessageBox.Show("I am Clicked 1!");
        }

        public void OnExecuteButton2()
        {
            MessageBox.Show("This is button2");
        }
        
    }
}
