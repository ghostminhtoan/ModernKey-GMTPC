using System;
using System.Windows;
using System.Windows.Input;

namespace ModernKey
{
    public partial class QuickInputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public QuickInputDialog(string prompt, string defaultVal = "")
        {
            InitializeComponent();
            TxtPrompt.Text = string.IsNullOrWhiteSpace(prompt) ? "Nhập giá trị thay thế:" : prompt;
            TxtInput.Text = defaultVal ?? string.Empty;
            Loaded += (s, e) =>
            {
                TxtInput.Focus();
                TxtInput.SelectAll();
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            InputText = TxtInput.Text;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnOk_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                BtnCancel_Click(sender, e);
            }
        }

        public static string ShowInput(string prompt, string defaultVal = "")
        {
            string result = null;
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dlg = new QuickInputDialog(prompt, defaultVal);
                    if (dlg.ShowDialog() == true)
                    {
                        result = dlg.InputText;
                    }
                });
            }
            else
            {
                var dlg = new QuickInputDialog(prompt, defaultVal);
                if (dlg.ShowDialog() == true)
                {
                    result = dlg.InputText;
                }
            }
            return result ?? defaultVal;
        }
    }
}
