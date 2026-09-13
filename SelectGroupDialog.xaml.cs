using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModernKey.Models;

namespace ModernKey
{
    public partial class SelectGroupDialog : Window
    {
        public class GroupItemViewModel
        {
            public string RawName { get; set; }
            public string DisplayName { get; set; }
            public int Count { get; set; }
            public string CountText => $"({Count})";
        }

        public string SelectedGroupName { get; private set; } = string.Empty;

        public SelectGroupDialog(IEnumerable<ClipboardItem> favoriteItems, string defaultGroup = null)
        {
            InitializeComponent();

            var favList = favoriteItems != null ? favoriteItems.ToList() : new List<ClipboardItem>();
            int totalFavorites = favList.Count;

            var groupCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in favList)
            {
                string g = (item.GroupName ?? "").Trim();
                if (!string.IsNullOrEmpty(g))
                {
                    if (groupCounts.ContainsKey(g)) groupCounts[g]++;
                    else groupCounts[g] = 1;
                }
            }

            var list = new List<GroupItemViewModel>();
            // 1. Mục mặc định: -All-
            list.Add(new GroupItemViewModel
            {
                RawName = string.Empty,
                DisplayName = "-All-",
                Count = totalFavorites
            });

            // 2. Các mục Group hiện có sắp xếp theo Alphabet A-Z
            var sortedGroups = groupCounts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in sortedGroups)
            {
                list.Add(new GroupItemViewModel
                {
                    RawName = kvp.Key,
                    DisplayName = kvp.Key,
                    Count = kvp.Value
                });
            }

            LstGroups.ItemsSource = list;

            // Xác định mục chọn ban đầu
            GroupItemViewModel initialSelection = null;
            if (!string.IsNullOrWhiteSpace(defaultGroup))
            {
                initialSelection = list.FirstOrDefault(x => string.Equals(x.RawName, defaultGroup.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (initialSelection == null)
            {
                initialSelection = list.FirstOrDefault();
            }

            if (initialSelection != null)
            {
                LstGroups.SelectedItem = initialSelection;
                TxtGroupName.Text = string.IsNullOrEmpty(initialSelection.RawName) ? "-All-" : initialSelection.RawName;
            }

            Loaded += (s, ev) =>
            {
                TxtGroupName.Focus();
                TxtGroupName.SelectAll();
            };
        }

        private void LstGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstGroups.SelectedItem is GroupItemViewModel selected)
            {
                TxtGroupName.Text = string.IsNullOrEmpty(selected.RawName) ? "-All-" : selected.RawName;
            }
        }

        private void LstGroups_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void TxtGroupName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmSelection()
        {
            string text = (TxtGroupName.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text) ||
                string.Equals(text, "-All-", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "All", StringComparison.OrdinalIgnoreCase))
            {
                SelectedGroupName = string.Empty;
            }
            else
            {
                SelectedGroupName = text;
            }

            DialogResult = true;
            Close();
        }
    }
}
