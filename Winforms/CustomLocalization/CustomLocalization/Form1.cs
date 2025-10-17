using C1.Win.Command;
using C1.Win.Input;
using C1.Win.Schedule;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CustomLocalization
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeCulture();
            InitializeComponent();
            InitializeComboBox();
        }

        private void InitializeCulture()
        {
            string cultureKey = GetCurrentCultureKey();
            var culture = new CultureInfo(cultureKey);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
        }   

        private void InitializeComboBox()
        {
            // later gets this data form the custom folder wheree i wanna add teh culture
            string[] items = { "en-US", "ko-KR", "ja-JP", "dz-BT", "hi-IN" };

            selectCultureComboBox.Items.AddRange(items);

            int indexOfCulture = FindCultureIndex(selectCultureComboBox, GetCurrentCultureKey());
            selectCultureComboBox.SelectedIndex = indexOfCulture;
            selectCultureComboBox.SelectedItemChanged += SelectCultureComboBox_SelectedItemChanged; ;   
        }

        private void SelectCultureComboBox_SelectedItemChanged(object? sender, EventArgs e)
        {
            if (selectCultureComboBox.SelectedItem != null)
            {
                string culture = selectCultureComboBox.SelectedItem.DisplayText.ToString() ?? "en-US";
                ChangeRunTimeCulture(culture);
            }   
        }

        private int FindCultureIndex(C1ComboBox comboBox, string cultureKey)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item && item.DisplayText == cultureKey)
                    return i;
            }
            return -1;
        }

        private string GetCurrentCultureKey()
        {
            string cultureKey = Properties.Settings.Default.CurrentLocalizationKey;
            return cultureKey;
        }

        private void ChangeRunTimeCulture(string culture)
        {
            // First set the culture in the settings to persist it
            Properties.Settings.Default.CurrentLocalizationKey = culture; 
            Properties.Settings.Default.Save(); 

            // Then restart the application to apply the new culture
            Application.Restart();
            Environment.Exit(0);
        }
    }
}
