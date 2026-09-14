using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using ModernKey.Core;
using ModernKey.Hook;

namespace ModernKey
{
    public partial class TextTransformWindow : Window
    {
        private string _originalText;

        public TextTransformWindow(string selectedText)
        {
            InitializeComponent();
            _originalText = selectedText ?? string.Empty;
            TxtPreview.Text = _originalText;
            TxtInfo.Text = $" ({_originalText.Length} ký tự)";

            Deactivated += (s, e) =>
            {
                try { Close(); } catch { }
            };

            // Đặt vị trí gần con trỏ chuột
            var mouse = System.Windows.Forms.Cursor.Position;
            Left = Math.Max(10, Math.Min(mouse.X - Width / 2, SystemParameters.PrimaryScreenWidth - Width - 20));
            Top = Math.Max(10, Math.Min(mouse.Y + 20, SystemParameters.PrimaryScreenHeight - Height - 40));
        }

        private void ApplyAndPaste(string newText)
        {
            if (string.IsNullOrEmpty(newText))
            {
                Close();
                return;
            }

            try
            {
                Clipboard.SetText(newText);
                KeySender.SendViaClipboardPaste(newText);
            }
            catch { }

            Close();
        }

        private void BtnUpper_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndPaste(_originalText.ToUpper());
        }

        private void BtnLower_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndPaste(_originalText.ToLower());
        }

        private void BtnTitle_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndPaste(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_originalText.ToLower()));
        }

        private void BtnRemoveDiacritics_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndPaste(CharsetConverter.RemoveDiacritics(_originalText));
        }

        private void BtnSlug_Click(object sender, RoutedEventArgs e)
        {
            string noDia = CharsetConverter.RemoveDiacritics(_originalText).ToLower();
            string slug = Regex.Replace(noDia, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
            ApplyAndPaste(slug);
        }

        private void BtnBase64_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(_originalText);
                ApplyAndPaste(Convert.ToBase64String(bytes));
            }
            catch
            {
                Close();
            }
        }

        private void BtnUrlEnc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyAndPaste(Uri.EscapeDataString(_originalText));
            }
            catch
            {
                Close();
            }
        }

        private void BtnWordCount_Click(object sender, RoutedEventArgs e)
        {
            int chars = _originalText.Length;
            int words = _originalText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int lines = _originalText.Split('\n').Length;
            TxtInfo.Text = $" // {words} từ, {chars} ký tự, {lines} dòng";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
