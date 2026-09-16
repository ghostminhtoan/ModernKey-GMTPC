using System;
using System.Windows;
using System.Windows.Input;

namespace ModernKey
{
    public partial class EditMacroDialog : Window
    {
        public string Shortcut => TxtShortcut?.Text?.Trim() ?? string.Empty;
        public string Replacement => TxtReplacement?.Text ?? string.Empty;

        public EditMacroDialog(string initialShortcut = null, string initialReplacement = null, bool isEdit = false)
        {
            InitializeComponent();

            if (isEdit)
            {
                Title = "Edit Macro";
                TxtHeaderTitle.Text = "EDIT MACRO // CHỈNH SỬA TỪ GÕ TẮT";
            }
            else
            {
                Title = "Add Macro";
                TxtHeaderTitle.Text = "ADD MACRO // THÊM TỪ GÕ TẮT MỚI";
            }

            if (!string.IsNullOrEmpty(initialShortcut))
            {
                TxtShortcut.Text = initialShortcut;
            }

            if (!string.IsNullOrEmpty(initialReplacement))
            {
                TxtReplacement.Text = initialReplacement;
            }

            Loaded += (s, e) =>
            {
                if (string.IsNullOrEmpty(initialShortcut))
                {
                    TxtShortcut.Focus();
                }
                else
                {
                    TxtReplacement.Focus();
                    TxtReplacement.SelectAll();
                }
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtShortcut.Text))
            {
                MessageBox.Show("Vui lòng nhập từ viết tắt (Shortcut)!", "ModernKey", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtShortcut.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                BtnOk_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
