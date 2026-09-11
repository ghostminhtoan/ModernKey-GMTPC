using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModernKey.Config;
using ModernKey.Models;

namespace ModernKey
{
    /// <summary>
    /// Interaction logic for CustomInputMethodWindow.xaml
    /// </summary>
    public partial class CustomInputMethodWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly ObservableCollection<CustomInputRule> _rules = new ObservableCollection<CustomInputRule>();

        public CustomInputMethodWindow(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeComponent();
            InitControls();
        }

        private void InitControls()
        {
            // Nạp danh sách Presets
            CmbPresets.ItemsSource = CustomInputRule.PresetNames;
            CmbPresets.SelectedIndex = 0;

            // Nạp danh sách Actions
            CmbAction.ItemsSource = CustomInputRule.ActionDescriptions;
            CmbAction.SelectedIndex = 0;

            // Nạp danh sách Rules hiện tại từ Settings (sao chép độc lập để không ảnh hưởng nếu hủy)
            if (_settings.CustomRules != null && _settings.CustomRules.Count > 0)
            {
                foreach (var rule in _settings.CustomRules)
                {
                    _rules.Add(new CustomInputRule(rule.Key, rule.Action));
                }
            }
            else
            {
                // Mặc định nạp Telex nếu chưa có quy tắc nào
                var defaultPreset = CustomInputRule.GetPreset(0);
                foreach (var rule in defaultPreset)
                {
                    _rules.Add(new CustomInputRule(rule.Key, rule.Action));
                }
            }

            DgRules.ItemsSource = _rules;
        }

        private void BtnLoadPreset_Click(object sender, RoutedEventArgs e)
        {
            int sel = CmbPresets.SelectedIndex;
            if (sel >= 0)
            {
                var preset = CustomInputRule.GetPreset(sel);
                _rules.Clear();
                foreach (var rule in preset)
                {
                    _rules.Add(new CustomInputRule(rule.Key, rule.Action));
                }
                TxtKey.Text = string.Empty;
                if (CmbAction.Items.Count > 0) CmbAction.SelectedIndex = 0;
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string keyText = TxtKey.Text?.Trim();
            if (string.IsNullOrEmpty(keyText))
            {
                MessageBox.Show("Hãy nhập phím!", "ModernKey", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtKey.Focus();
                return;
            }

            char key = keyText[0];
            if (_rules.Any(r => r.Key == key))
            {
                MessageBox.Show("Phím đã tồn tại trong quy tắc!", "ModernKey", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int action = CmbAction.SelectedIndex >= 0 ? CmbAction.SelectedIndex : 0;
            var newRule = new CustomInputRule(key, action);
            _rules.Add(newRule);
            DgRules.SelectedItem = newRule;
            DgRules.ScrollIntoView(newRule);
        }

        private void BtnReplace_Click(object sender, RoutedEventArgs e)
        {
            string keyText = TxtKey.Text?.Trim();
            if (string.IsNullOrEmpty(keyText))
            {
                MessageBox.Show("Hãy nhập phím!", "ModernKey", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtKey.Focus();
                return;
            }

            char key = keyText[0];
            var existing = _rules.FirstOrDefault(r => r.Key == key);
            if (existing == null)
            {
                MessageBox.Show("Không tìm thấy phím để thay thế!", "ModernKey", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int action = CmbAction.SelectedIndex >= 0 ? CmbAction.SelectedIndex : 0;
            existing.Action = action;
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DgRules.SelectedItem is CustomInputRule selected)
            {
                _rules.Remove(selected);
                TxtKey.Text = string.Empty;
            }
            else if (!string.IsNullOrEmpty(TxtKey.Text))
            {
                char key = TxtKey.Text[0];
                var rule = _rules.FirstOrDefault(r => r.Key == key);
                if (rule != null)
                {
                    _rules.Remove(rule);
                    TxtKey.Text = string.Empty;
                }
            }
        }

        private void DgRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRules.SelectedItem is CustomInputRule selected)
            {
                TxtKey.Text = selected.Key.ToString();
                if (selected.Action >= 0 && selected.Action < CmbAction.Items.Count)
                {
                    CmbAction.SelectedIndex = selected.Action;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.CustomRules = new List<CustomInputRule>(_rules);
            SettingsManager.SaveSettings(_settings);
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
