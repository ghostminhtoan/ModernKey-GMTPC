using System;
using System.Collections.ObjectModel;
using System.Windows;
using ModernKey.Models;

namespace ModernKey
{
    public partial class EditMacroKeystrokesDialog : Window
    {
        public ObservableCollection<MacroKeyEvent> Events { get; } = new ObservableCollection<MacroKeyEvent>();

        public EditMacroKeystrokesDialog(ObservableCollection<MacroKeyEvent> initialEvents = null)
        {
            InitializeComponent();

            if (initialEvents != null)
            {
                foreach (var ev in initialEvents)
                {
                    Events.Add(new MacroKeyEvent
                    {
                        Delay = ev.Delay,
                        Event = ev.Event,
                        Key = ev.Key,
                        KeyCode = ev.KeyCode,
                        Extended = ev.Extended
                    });
                }
            }

            DgMacroEvents.ItemsSource = Events;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new KeyCombinationDialog();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                string combo = dlg.ResultCombination;
                // Thêm cặp Key Down và Key Up
                Events.Add(new MacroKeyEvent
                {
                    Delay = 50,
                    Event = "Key Down",
                    Key = combo,
                    KeyCode = 0x41, // Default or parsed
                    Extended = combo.Contains("Win")
                });

                Events.Add(new MacroKeyEvent
                {
                    Delay = 50,
                    Event = "Key Up",
                    Key = combo,
                    KeyCode = 0x41,
                    Extended = combo.Contains("Win")
                });
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = DgMacroEvents.SelectedItem as MacroKeyEvent;
            if (selected != null)
            {
                Events.Remove(selected);
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgMacroEvents.SelectedIndex;
            if (idx > 0)
            {
                var item = Events[idx];
                Events.RemoveAt(idx);
                Events.Insert(idx - 1, item);
                DgMacroEvents.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgMacroEvents.SelectedIndex;
            if (idx >= 0 && idx < Events.Count - 1)
            {
                var item = Events[idx];
                Events.RemoveAt(idx);
                Events.Insert(idx + 1, item);
                DgMacroEvents.SelectedIndex = idx + 1;
            }
        }

        private void BtnMoreOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var itemDelay = new System.Windows.Controls.MenuItem { Header = "Đặt độ trễ đồng loạt thành 50ms" };
            itemDelay.Click += (s, ev) =>
            {
                foreach (var k in Events) k.Delay = 50;
            };
            menu.Items.Add(itemDelay);

            var itemDelayZero = new System.Windows.Controls.MenuItem { Header = "Xóa toàn bộ độ trễ (0ms - Siêu tốc)" };
            itemDelayZero.Click += (s, ev) =>
            {
                foreach (var k in Events) k.Delay = 0;
            };
            menu.Items.Add(itemDelayZero);

            var itemClear = new System.Windows.Controls.MenuItem { Header = "Xóa sạch danh sách sự kiện" };
            itemClear.Click += (s, ev) => Events.Clear();
            menu.Items.Add(itemClear);

            menu.IsOpen = true;
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Cửa sổ Edit Macro cho phép điều chỉnh độ trễ mili-giây (Delay ms), loại sự kiện nhấn/nhả (Key Down/Key Up) và mã phím chi tiết.", "Trợ giúp Macro", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
