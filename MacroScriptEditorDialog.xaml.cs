using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModernKey.Core;
using ModernKey.Models;

namespace ModernKey
{
    public partial class MacroScriptEditorDialog : Window
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public class MacroStepModel
        {
            public int StepNumber { get; set; }
            public string ActionDisplay { get; set; }
            public string DetailsDisplay { get; set; }
            public string DelayDisplay { get; set; }
            public string RawScriptLine { get; set; }
        }

        public string ResultScript { get; private set; }
        private readonly ObservableCollection<MacroStepModel> _steps = new ObservableCollection<MacroStepModel>();

        public MacroScriptEditorDialog(string initialScript)
        {
            InitializeComponent();
            ListSteps.ItemsSource = _steps;

            TxtRawScript.Text = initialScript ?? string.Empty;
            ParseScriptToSteps(TxtRawScript.Text);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSaveAndApply_Click(object sender, RoutedEventArgs e)
        {
            // Nếu người dùng đang đứng ở tab Raw Script thì lấy text từ RawScript, nếu không thì build từ steps
            if (TabWorkspace.SelectedIndex == 1)
            {
                ResultScript = TxtRawScript.Text;
            }
            else
            {
                ResultScript = BuildScriptFromSteps();
            }

            DialogResult = true;
            Close();
        }

        private void ParseScriptToSteps(string script)
        {
            _steps.Clear();
            if (string.IsNullOrWhiteSpace(script)) return;

            string[] lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int stepNo = 1;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var item = new MacroStepModel
                {
                    StepNumber = stepNo++,
                    RawScriptLine = line
                };

                if (line.StartsWith("#") || line.StartsWith("//"))
                {
                    item.ActionDisplay = "💬 Chú thích";
                    item.DetailsDisplay = line;
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("DELAY :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⏱️ Nghỉ (Delay)";
                    string ms = line.Substring(7).Trim();
                    item.DetailsDisplay = $"Chờ {ms} mili-giây";
                    item.DelayDisplay = $"{ms}ms";
                }
                else if (line.StartsWith("MOUSE_CLICK :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "🖱️ Click Chuột";
                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string btn = parts.Length > 1 ? parts[1].Trim() : "LEFT";
                    string x = parts.Length > 2 ? parts[2].Trim() : "0";
                    string y = parts.Length > 3 ? parts[3].Trim() : "0";
                    item.DetailsDisplay = $"Nút {btn} tại tọa độ X={x}, Y={y}";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("MOUSE_DBLCLICK :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "🖱️ Double Click";
                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string btn = parts.Length > 1 ? parts[1].Trim() : "LEFT";
                    string x = parts.Length > 2 ? parts[2].Trim() : "0";
                    string y = parts.Length > 3 ? parts[3].Trim() : "0";
                    item.DetailsDisplay = $"Nhấp đúp {btn} tại tọa độ X={x}, Y={y}";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("MOUSE_DOWN :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⬇️ Giữ Chuột (Drag)";
                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string btn = parts.Length > 1 ? parts[1].Trim() : "LEFT";
                    string x = parts.Length > 2 ? parts[2].Trim() : "0";
                    string y = parts.Length > 3 ? parts[3].Trim() : "0";
                    item.DetailsDisplay = $"Nhấn giữ {btn} tại tọa độ X={x}, Y={y}";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("MOUSE_UP :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⬆️ Thả Chuột (Drop)";
                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string btn = parts.Length > 1 ? parts[1].Trim() : "LEFT";
                    string x = parts.Length > 2 ? parts[2].Trim() : "0";
                    string y = parts.Length > 3 ? parts[3].Trim() : "0";
                    item.DetailsDisplay = $"Thả nút {btn} tại tọa độ X={x}, Y={y}";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("MOUSE_MOVE :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "🎯 Di Chuột (Move)";
                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string x = parts.Length > 1 ? parts[1].Trim() : "0";
                    string y = parts.Length > 2 ? parts[2].Trim() : "0";
                    item.DetailsDisplay = $"Di chuyển chuột tới X={x}, Y={y}";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("MOUSE_WHEEL :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "📜 Cuộn Chuột";
                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    string delta = parts.Length > 1 ? parts[1].Trim() : "120";
                    string x = parts.Length > 2 ? parts[2].Trim() : "0";
                    string y = parts.Length > 3 ? parts[3].Trim() : "0";
                    item.DetailsDisplay = $"Cuộn delta={delta} tại X={x}, Y={y}";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("KEY_COMBINATION :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⌨️⚡ Tổ Hợp Phím";
                    string combo = line.Substring(17).Trim();
                    item.DetailsDisplay = $"Bấm tổ hợp [{combo}]";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("KEY_PRESS :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⌨️ Nhấn Phím";
                    string key = line.Substring(11).Trim();
                    item.DetailsDisplay = $"Gõ phím [{key}]";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("KEY_DOWN :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⬇️ Giữ Phím (Down)";
                    string key = line.Substring(10).Trim();
                    item.DetailsDisplay = $"Nhấn giữ [{key}]";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("KEY_UP :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "⬆️ Nhả Phím (Up)";
                    string key = line.Substring(8).Trim();
                    item.DetailsDisplay = $"Thả phím [{key}]";
                    item.DelayDisplay = "-";
                }
                else if (line.StartsWith("TEXT :", StringComparison.OrdinalIgnoreCase))
                {
                    item.ActionDisplay = "🔤 Gõ Văn Bản";
                    string txt = line.Substring(6).Trim();
                    item.DetailsDisplay = $"Gõ chuỗi: \"{txt}\"";
                    item.DelayDisplay = "-";
                }
                else
                {
                    item.ActionDisplay = "⚡ Lệnh Tùy Chỉnh";
                    item.DetailsDisplay = line;
                    item.DelayDisplay = "-";
                }

                _steps.Add(item);
            }

            TxtStatus.Text = $"Đã nạp {_steps.Count} bước kịch bản.";
        }

        private string BuildScriptFromSteps()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var step in _steps)
            {
                if (!string.IsNullOrWhiteSpace(step.RawScriptLine))
                {
                    sb.AppendLine(step.RawScriptLine.Trim());
                }
            }
            return sb.ToString().TrimEnd();
        }

        private void ReindexSteps()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].StepNumber = i + 1;
            }
            ListSteps.Items.Refresh();
        }

        #region Toolbar Actions

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            TxtStatus.Text = "🔴 Đang ghi macro... Nhấn [F11] hoặc [Esc] để dừng.";

            Action<string> finishedHandler = null;
            finishedHandler = (fullScript) =>
            {
                MacroRecorder.Instance.OnRecordingFinished -= finishedHandler;

                Dispatcher.Invoke(() =>
                {
                    WindowState = WindowState.Normal;
                    Activate();

                    if (!string.IsNullOrWhiteSpace(fullScript))
                    {
                        TxtRawScript.Text = fullScript;
                        ParseScriptToSteps(fullScript);
                        TxtStatus.Text = $"Đã hoàn tất ghi kịch bản ({_steps.Count} bước).";
                    }
                    else
                    {
                        TxtStatus.Text = "Đã dừng ghi (không có thao tác nào).";
                    }
                });
            };

            MacroRecorder.Instance.OnRecordingFinished += finishedHandler;
            MacroRecorder.Instance.StartRecording();
        }

        private void BtnTestRun_Click(object sender, RoutedEventArgs e)
        {
            string scriptToRun = TabWorkspace.SelectedIndex == 1 ? TxtRawScript.Text : BuildScriptFromSteps();
            if (string.IsNullOrWhiteSpace(scriptToRun))
            {
                MessageBox.Show("Kịch bản đang trống, vui lòng thêm bước hoặc ghi thao tác trước!", "ModernKey Macro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            WindowState = WindowState.Minimized;
            TxtStatus.Text = "Đang chạy thử macro sau 1.5 giây...";

            Task.Run(() =>
            {
                Thread.Sleep(1500);

                var tempItem = new ComfortShortcutItem
                {
                    ActionType = ShortcutActionType.KeystrokeMacro,
                    MacroKeystrokes = scriptToRun
                };

                ComfortShortcutManager.Instance.ExecuteShortcutAction(tempItem);

                Dispatcher.Invoke(() =>
                {
                    Thread.Sleep(500);
                    WindowState = WindowState.Normal;
                    Activate();
                    TxtStatus.Text = "Chạy thử kịch bản thành công!";
                });
            });
        }

        private void BtnAddStep_Click(object sender, RoutedEventArgs e)
        {
            BtnAddStepToEnd_Click(sender, e);
        }

        private void BtnEditStep_Click(object sender, RoutedEventArgs e)
        {
            if (ListSteps.SelectedItem is MacroStepModel step)
            {
                PopulateStepToEditPanel(step);
            }
            else
            {
                TxtStatus.Text = "Vui lòng chọn một bước để sửa.";
            }
        }

        private void BtnDeleteStep_Click(object sender, RoutedEventArgs e)
        {
            if (ListSteps.SelectedItem is MacroStepModel step)
            {
                _steps.Remove(step);
                ReindexSteps();
                TxtStatus.Text = "Đã xóa bước kịch bản.";
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = ListSteps.SelectedIndex;
            if (idx > 0)
            {
                var item = _steps[idx];
                _steps.RemoveAt(idx);
                _steps.Insert(idx - 1, item);
                ReindexSteps();
                ListSteps.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = ListSteps.SelectedIndex;
            if (idx >= 0 && idx < _steps.Count - 1)
            {
                var item = _steps[idx];
                _steps.RemoveAt(idx);
                _steps.Insert(idx + 1, item);
                ReindexSteps();
                ListSteps.SelectedIndex = idx + 1;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa sạch toàn bộ kịch bản này không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _steps.Clear();
                TxtRawScript.Clear();
                TxtStatus.Text = "Đã xóa sạch kịch bản.";
            }
        }

        #endregion

        #region Quick Step Editor Panel

        private void CmbActionKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateEditorInputVisibility();
        }

        private void UpdateEditorInputVisibility()
        {
            if (CmbActionKind.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                bool isMouseCoords = tag == "MOUSE_CLICK" || tag == "MOUSE_DBLCLICK" || tag == "MOUSE_DOWN" || tag == "MOUSE_UP" || tag == "MOUSE_MOVE";
                PanelMouseParams.Visibility = isMouseCoords ? Visibility.Visible : Visibility.Collapsed;
                TxtSingleParam.Visibility = isMouseCoords ? Visibility.Collapsed : Visibility.Visible;
                CmbSubKind.Visibility = (tag == "MOUSE_CLICK" || tag == "MOUSE_DBLCLICK" || tag == "MOUSE_DOWN" || tag == "MOUSE_UP") ? Visibility.Visible : Visibility.Collapsed;

                switch (tag)
                {
                    case "KEY_COMBINATION":
                        TxtSingleParam.ToolTip = "Tổ hợp phím, ví dụ CONTROL+C hoặc ALT+TAB hoặc CONTROL+SHIFT+A";
                        if (string.IsNullOrEmpty(TxtSingleParam.Text) || !TxtSingleParam.Text.Contains("+"))
                            TxtSingleParam.Text = "CONTROL+C";
                        break;
                    case "KEY_PRESS":
                    case "KEY_DOWN":
                    case "KEY_UP":
                        TxtSingleParam.ToolTip = "Tên phím: ENTER, TAB, SPACE, ESC, F5, C, V, CONTROL...";
                        if (string.IsNullOrEmpty(TxtSingleParam.Text) || int.TryParse(TxtSingleParam.Text, out _))
                            TxtSingleParam.Text = "ENTER";
                        break;
                    case "TEXT":
                        TxtSingleParam.ToolTip = "Nội dung văn bản cần gõ tự động";
                        if (string.IsNullOrEmpty(TxtSingleParam.Text))
                            TxtSingleParam.Text = "Xin chào các bạn";
                        break;
                    case "DELAY":
                        TxtSingleParam.ToolTip = "Thời gian nghỉ (mili-giây, ví dụ 200)";
                        TxtSingleParam.Text = "200";
                        break;
                }
            }
        }

        private void BtnPickMousePos_Click(object sender, RoutedEventArgs e)
        {
            if (GetCursorPos(out POINT pt))
            {
                TxtCoordX.Text = pt.X.ToString();
                TxtCoordY.Text = pt.Y.ToString();
                TxtStatus.Text = $"Đã lấy tọa độ chuột: X={pt.X}, Y={pt.Y}";
            }
        }

        private string GenerateCurrentStepScript()
        {
            if (CmbActionKind.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                int.TryParse(TxtCoordX.Text, out int mx);
                int.TryParse(TxtCoordY.Text, out int my);
                string btn = (CmbSubKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "LEFT";

                switch (tag)
                {
                    case "MOUSE_CLICK":
                    case "MOUSE_DBLCLICK":
                    case "MOUSE_DOWN":
                    case "MOUSE_UP":
                        return $"{tag} : {btn} : {mx} : {my}";

                    case "MOUSE_MOVE":
                        return $"MOUSE_MOVE : {mx} : {my}";

                    case "MOUSE_WHEEL":
                        int.TryParse(TxtCoordX.Text, out int wx);
                        int.TryParse(TxtCoordY.Text, out int wy);
                        return $"MOUSE_WHEEL : 120 : {wx} : {wy}";

                    case "KEY_COMBINATION":
                        string combo = (TxtSingleParam.Text ?? "CONTROL+C").Trim().ToUpperInvariant();
                        return $"KEY_COMBINATION : {combo}";

                    case "KEY_PRESS":
                    case "KEY_DOWN":
                    case "KEY_UP":
                        string key = (TxtSingleParam.Text ?? "ENTER").Trim().ToUpperInvariant();
                        return $"{tag} : {key}";

                    case "TEXT":
                        return $"TEXT : {TxtSingleParam.Text}";

                    case "DELAY":
                        int.TryParse(TxtSingleParam.Text, out int delayMs);
                        if (delayMs <= 0) delayMs = 100;
                        return $"DELAY : {delayMs}";
                }
            }
            return "DELAY : 100";
        }

        private void BtnAddStepToEnd_Click(object sender, RoutedEventArgs e)
        {
            string scriptLine = GenerateCurrentStepScript();
            ParseScriptToSteps(BuildScriptFromSteps() + Environment.NewLine + scriptLine);
            ListSteps.SelectedIndex = _steps.Count - 1;
            ListSteps.ScrollIntoView(ListSteps.SelectedItem);
            TxtStatus.Text = "Đã thêm bước mới vào kịch bản.";
        }

        private void BtnSaveStepEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ListSteps.SelectedItem is MacroStepModel step)
            {
                string scriptLine = GenerateCurrentStepScript();
                step.RawScriptLine = scriptLine;
                // Re-parse
                string allScript = BuildScriptFromSteps();
                int curIdx = ListSteps.SelectedIndex;
                ParseScriptToSteps(allScript);
                if (curIdx >= 0 && curIdx < _steps.Count)
                {
                    ListSteps.SelectedIndex = curIdx;
                }
                TxtStatus.Text = "Đã cập nhật bước thành công.";
            }
            else
            {
                BtnAddStepToEnd_Click(sender, e);
            }
        }

        private void PopulateStepToEditPanel(MacroStepModel step)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.RawScriptLine)) return;
            string line = step.RawScriptLine.Trim();

            if (line.StartsWith("MOUSE_CLICK :", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("MOUSE_DBLCLICK :", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("MOUSE_DOWN :", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("MOUSE_UP :", StringComparison.OrdinalIgnoreCase))
            {
                string cmd = line.Substring(0, line.IndexOf(':')).Trim();
                SetCmbAction(cmd.ToUpperInvariant());

                string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1) SetCmbSub(parts[1].Trim());
                if (parts.Length > 2) TxtCoordX.Text = parts[2].Trim();
                if (parts.Length > 3) TxtCoordY.Text = parts[3].Trim();
            }
            else if (line.StartsWith("MOUSE_MOVE :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("MOUSE_MOVE");
                string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1) TxtCoordX.Text = parts[1].Trim();
                if (parts.Length > 2) TxtCoordY.Text = parts[2].Trim();
            }
            else if (line.StartsWith("MOUSE_WHEEL :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("MOUSE_WHEEL");
                string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2) TxtCoordX.Text = parts[2].Trim();
                if (parts.Length > 3) TxtCoordY.Text = parts[3].Trim();
            }
            else if (line.StartsWith("KEY_COMBINATION :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("KEY_COMBINATION");
                TxtSingleParam.Text = line.Substring(17).Trim();
            }
            else if (line.StartsWith("KEY_PRESS :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("KEY_PRESS");
                TxtSingleParam.Text = line.Substring(11).Trim();
            }
            else if (line.StartsWith("KEY_DOWN :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("KEY_DOWN");
                TxtSingleParam.Text = line.Substring(10).Trim();
            }
            else if (line.StartsWith("KEY_UP :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("KEY_UP");
                TxtSingleParam.Text = line.Substring(8).Trim();
            }
            else if (line.StartsWith("TEXT :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("TEXT");
                TxtSingleParam.Text = line.Substring(6).Trim();
            }
            else if (line.StartsWith("DELAY :", StringComparison.OrdinalIgnoreCase))
            {
                SetCmbAction("DELAY");
                TxtSingleParam.Text = line.Substring(7).Trim();
            }

            UpdateEditorInputVisibility();
        }

        private void SetCmbAction(string tag)
        {
            foreach (ComboBoxItem cbi in CmbActionKind.Items)
            {
                if (cbi.Tag?.ToString() == tag)
                {
                    CmbActionKind.SelectedItem = cbi;
                    break;
                }
            }
        }

        private void SetCmbSub(string tag)
        {
            foreach (ComboBoxItem cbi in CmbSubKind.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    CmbSubKind.SelectedItem = cbi;
                    break;
                }
            }
        }

        private void ListSteps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListSteps.SelectedItem is MacroStepModel step)
            {
                PopulateStepToEditPanel(step);
            }
        }

        private void ListSteps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListSteps.SelectedItem is MacroStepModel step)
            {
                PopulateStepToEditPanel(step);
            }
        }

        #endregion

        #region Sync Tabs

        private void BtnSyncToTable_Click(object sender, RoutedEventArgs e)
        {
            ParseScriptToSteps(TxtRawScript.Text);
            TabWorkspace.SelectedIndex = 0;
            TxtStatus.Text = "Đã đồng bộ mã script vào bảng kịch bản.";
        }

        private void BtnSyncFromTable_Click(object sender, RoutedEventArgs e)
        {
            TxtRawScript.Text = BuildScriptFromSteps();
            TxtStatus.Text = "Đã cập nhật kịch bản từ bảng vào trình soạn thảo mã.";
        }

        #endregion
    }
}
