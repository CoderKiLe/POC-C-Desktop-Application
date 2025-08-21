using CupertinoForms.UI.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace POC_DemoAppCupertinoTreevie
{
    internal class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CupertinoWindow window = new();
            window.Title = "C1 Engine Installer";
            window.ShowDialog();
        }
    }
}
