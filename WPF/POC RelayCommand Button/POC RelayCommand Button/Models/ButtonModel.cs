using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace POC_RelayCommand_Button.Models
{
    public class ButtonModel
    {
        public string Label { get; set; }
        public ICommand Command { get; set; }
        public object Parameter { get; set; }
    }
}
