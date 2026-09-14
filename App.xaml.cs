using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using ModernKey.Config;
using ModernKey.Core;
using ModernKey.Hook;
using ModernKey.Models;
using ModernKey.Tray;

namespace ModernKey
{
    public partial class App : Application
    {
        private static Mutex _appMutex;
        private KeyboardHook _keyboardHook;
        private SystemTrayManager _trayManager;
        private VietnameseEngine _engine;
        private MacroManager _macroManager;
        private AppSettings _settings;
        private MainWindow _mainWindow;
        private StatusOsdWindow _statusOsdWindow;
        private ClipboardHistoryManager _clipboardHistory;
        private ClipboardListener _clipboardListener;
        private ClipboardWindow _clipboardWindow;
        private CaretIndicatorWindow _caretIndicatorWindow;

        public ClipboardHistoryManager ClipboardHistory => _clipboardHistory;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
                AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            }
            catch { }

            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs != null && Array.IndexOf(cmdArgs, "--test") >= 0)
            {
                AttachConsole(-1);
                bool passed = EngineTester.RunAllTests(out string report);
                Console.WriteLine(report);
                try
                {
                    string outPath = System.IO.Path.Combine(SettingsManager.GetConfigDirectory(), "test_results.txt");
                    System.IO.File.WriteAllText(outPath, report, Encoding.UTF8);
                }
                catch { }

                Environment.Exit(passed ? 0 : 1);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try
                {
                    string err = ev.ExceptionObject?.ToString();
                    string logPath = System.IO.Path.Combine(SettingsManager.GetConfigDirectory(), "crash_log.txt");
                    System.IO.File.WriteAllText(logPath, err ?? "Unknown unhandled exception", Encoding.UTF8);
                }
                catch { }
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                try
                {
                    string err = ev.Exception?.ToString();
                    string logPath = System.IO.Path.Combine(SettingsManager.GetConfigDirectory(), "crash_log.txt");
                    System.IO.File.WriteAllText(logPath, err ?? "Unknown dispatcher exception", Encoding.UTF8);
                }
                catch { }
                ev.Handled = true;
            };

            const string mutexName = "ModernKeyGMTPC_SingleInstance_Mutex";
            _appMutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("ModernKey GMTPC đang chạy trên hệ thống!", "Thông báo",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 1. Tải cấu hình và macro
            _settings = SettingsManager.LoadSettings();
            _macroManager = new MacroManager();
            _clipboardHistory = new ClipboardHistoryManager(_settings);

            // 2. Khởi tạo Engine và Hook
            _engine = new VietnameseEngine(_settings, _macroManager);
            _keyboardHook = new KeyboardHook(_engine, _settings);
            _keyboardHook.Start();

            // 3. Khởi tạo Tray Icon, OSD & Clipboard Listener
            _trayManager = new SystemTrayManager(_settings, ShowMainWindow, ExitApplication, ShowClipboardWindow);
            _statusOsdWindow = new StatusOsdWindow();
            _clipboardListener = new ClipboardListener(_clipboardHistory, _settings);

            // Đồng bộ trạng thái khi phím tắt chuyển đổi chế độ V/E bất đồng bộ (Non-blocking Hook Thread)
            _keyboardHook.LanguageChanged += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    _trayManager.UpdateTrayIcon();
                    _trayManager.BuildContextMenu();
                    if (_mainWindow != null && _mainWindow.IsLoaded && _mainWindow.IsVisible)
                    {
                        _mainWindow.RefreshState();
                    }
                    ShowStatusOsd(_settings.IsVietnamese);
                }));
            };

            // Phím tắt đổi Bảng mã (F3: Unicode, F4: Tùy chọn)
            _keyboardHook.CharsetChanged += (cs) =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    SettingsManager.SaveSettings(_settings);
                    _trayManager.BuildContextMenu();
                    if (_mainWindow != null && _mainWindow.IsLoaded && _mainWindow.IsVisible)
                    {
                        _mainWindow.RefreshState();
                    }
                }));
            };

            // Phím tắt mở Bảng cài đặt (F5)
            _keyboardHook.OpenSettingsRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    ShowMainWindow();
                }));
            };

            // Phím tắt mở Bảng gõ tắt (F8)
            _keyboardHook.OpenMacroTableRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    ShowMainWindow();
                    _mainWindow?.SelectMacroTab();
                }));
            };

            // Phím tắt Bật/Tắt gõ tắt (F9)
            _keyboardHook.ToggleMacroRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    SettingsManager.SaveSettings(_settings);
                    if (_mainWindow != null && _mainWindow.IsLoaded && _mainWindow.IsVisible)
                    {
                        _mainWindow.RefreshState();
                    }
                }));
            };

            // Phím tắt Reset engine / hook (F12)
            _keyboardHook.ResetHookRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    _engine.Reset();
                    _keyboardHook.Stop();
                    _keyboardHook.Start();
                    _trayManager.UpdateTrayIcon();
                }));
            };

            // Phím tắt Toggle Bảng Lịch sử Clipboard (Win+Ins / Ctrl+Alt+V)
            _keyboardHook.OpenClipboardRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    ToggleClipboardHistory();
                }));
            };

            // Phím tắt Toggle Bảng Yêu thích Clipboard (Alt+Ins chuẩn Comfort Keys Pro)
            _keyboardHook.OpenClipboardFavoriteRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    ToggleClipboardFavorites();
                }));
            };

            // Phím tắt dán nhanh mục Clipboard cá nhân hóa (Chuẩn Comfort Keys Pro)
            _keyboardHook.CheckClipboardShortcutRequested = (mod, vk) =>
            {
                return TryPasteClipboardItemByShortcut(mod, vk);
            };

            // Phím tắt Quick Text Transform (Win+Alt+T / Ctrl+Shift+T)
            _keyboardHook.OpenTextTransformRequested += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    ShowTextTransformWindow();
                }));
            };

            // Game Mode Zero-Latency Toggle (Ctrl+Shift+F11)
            KeyboardHook.GameModeChanged += (enabled) =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    _statusOsdWindow?.ShowMessage(enabled ? "🎮 GAME MODE: BẬT (Zero-Latency 0ms)" : "⌨️ GAME MODE: TẮT (Gõ tiếng Việt)", enabled ? "#00FF66" : "#FF007F");
                }));
            };

            // Lắng nghe sự kiện đổi kiểu gõ từ phím F6
            KeyboardHook.InputMethodChanged += (im) =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    string imName = im == InputMethod.TuBinhTran ? "Tư Bình Trần" : (im == InputMethod.Telex ? "Telex" : (im == InputMethod.Vni ? "VNI" : im.ToString()));
                    _statusOsdWindow?.ShowMessage($"Kiểu gõ: {imName}", "#00F0FF");
                    _trayManager?.UpdateTrayIcon();
                }));
            };

            // Floating Caret Indicator bám theo con trỏ soạn thảo
            KeyboardHook.RequestShowCaretIndicator += () =>
            {
                if (_settings != null && _settings.EnableCaretIndicator)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    {
                        if (_caretIndicatorWindow == null)
                        {
                            _caretIndicatorWindow = new CaretIndicatorWindow();
                        }
                        _caretIndicatorWindow.ShowIndicator(_settings.IsVietnamese);
                    }));
                }
            };

            // Lắng nghe đồng bộ cấu hình từ bên ngoài (P2P / Local Folder Sync)
            SettingsManager.OnExternalSyncUpdate += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    _settings = SettingsManager.LoadSettings();
                    _macroManager?.Load();
                    if (_mainWindow != null && _mainWindow.IsLoaded && _mainWindow.IsVisible)
                    {
                        _mainWindow.RefreshState();
                    }
                    _statusOsdWindow?.ShowMessage("P2P CONFIG SYNCED", "#00F0FF");
                }));
            };

            if (!string.IsNullOrEmpty(_settings.SyncFolderPath))
            {
                SettingsManager.SetupSyncWatcher(_settings.SyncFolderPath);
            }

            _trayManager.StateChanged += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    if (_mainWindow != null && _mainWindow.IsLoaded && _mainWindow.IsVisible)
                    {
                        _mainWindow.RefreshState();
                    }
                }));
            };

            // 4. Khởi tạo MainWindow theo nhu cầu (Lazy loading giúp app siêu nhẹ khi khởi động khay hệ thống)
            if (_settings.OpenDialogOnStartup)
            {
                ShowMainWindow();
            }
            else
            {
                // Trì hoãn dọn dẹp bộ nhớ sau 4 giây khi engine và hook đã ổn định,
                // tuyệt đối không dọn ngay lập tức lúc 0s để tránh làm nghẽn I/O trên đĩa HDD!
                RequestMemoryCleanup(4);
            }
        }

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private static System.Windows.Threading.DispatcherTimer _trimTimer = null;

        public static void RequestMemoryCleanup(int delaySeconds = 2)
        {
            try
            {
                if (Application.Current?.Dispatcher == null) return;

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_trimTimer == null)
                    {
                        _trimTimer = new System.Windows.Threading.DispatcherTimer();
                        _trimTimer.Tick += (s, ev) =>
                        {
                            _trimTimer.Stop();
                            TrimWorkingSet();
                        };
                    }
                    _trimTimer.Stop();
                    _trimTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, delaySeconds));
                    _trimTimer.Start();
                }));
            }
            catch
            {
                TrimWorkingSet();
            }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, false);
                // Giải phóng các trang nhớ chưa chạm tới của working set trả về cho hệ điều hành
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_settings, _macroManager, _trayManager);
            }

            _mainWindow.RefreshState();

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        public void ShowClipboardWindow()
        {
            ShowClipboardWindow(null);
        }

        public void ShowClipboardWindow(string mode)
        {
            try
            {
                if (_clipboardWindow == null)
                {
                    _clipboardWindow = new ClipboardWindow(_clipboardHistory, _settings);
                }
                _clipboardWindow.ShowHud(IntPtr.Zero, mode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ShowClipboardWindow error: " + ex.Message);
                try
                {
                    _clipboardWindow = new ClipboardWindow(_clipboardHistory, _settings);
                    _clipboardWindow.ShowHud(IntPtr.Zero, mode);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine("ShowClipboardWindow retry error: " + ex2.Message);
                }
            }
        }

        public void ToggleClipboardHistory()
        {
            try
            {
                if (_clipboardWindow != null && _clipboardWindow.IsVisible && _clipboardWindow.WindowState != WindowState.Minimized && _clipboardWindow.CurrentMode == "HISTORY")
                {
                    _clipboardWindow.Hide();
                    return;
                }

                if (_clipboardWindow == null)
                {
                    _clipboardWindow = new ClipboardWindow(_clipboardHistory, _settings);
                }
                _clipboardWindow.ShowHud(IntPtr.Zero, "HISTORY");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ToggleClipboardHistory error: " + ex.Message);
            }
        }

        public void ToggleClipboardFavorites()
        {
            try
            {
                if (_clipboardWindow != null && _clipboardWindow.IsVisible && _clipboardWindow.WindowState != WindowState.Minimized && _clipboardWindow.CurrentMode == "FAVORITES")
                {
                    _clipboardWindow.Hide();
                    return;
                }

                if (_clipboardWindow == null)
                {
                    _clipboardWindow = new ClipboardWindow(_clipboardHistory, _settings);
                }
                _clipboardWindow.ShowHud(IntPtr.Zero, "FAVORITES");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ToggleClipboardFavorites error: " + ex.Message);
            }
        }

        public void ShowStatusOsd(bool isVietnamese)
        {
            if (_settings != null && _settings.EnableStatusOsd)
            {
                _statusOsdWindow?.ShowStatus(isVietnamese);
            }
        }

        private bool TryPasteClipboardItemByShortcut(int mod, uint vk)
        {
            if (_clipboardHistory == null) return false;

            ClipboardItem match = null;
            lock (_clipboardHistory)
            {
                // 1. Ưu tiên tìm trong danh sách Yêu thích
                match = _clipboardHistory.FavoriteItems.FirstOrDefault(x => x.ShortcutModifiers == mod && x.ShortcutVk == vk);
                // 2. Nếu không có trong Yêu thích, tìm trong Lịch sử
                if (match == null)
                {
                    match = _clipboardHistory.Items.FirstOrDefault(x => x.ShortcutModifiers == mod && x.ShortcutVk == vk);
                }
            }

            if (match != null)
            {
                PasteItemDirectly(match);
                return true;
            }

            return false;
        }

        public void PasteItemDirectly(ClipboardItem item)
        {
            if (item == null) return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(30);

                if (item.ContentType == ClipboardContentType.Text)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            KeySender.SuppressClipboardMonitoring = true;
                            Clipboard.SetText(item.TextContent ?? string.Empty);
                        }
                        catch { }
                    });

                    Thread.Sleep(40);
                    KeySender.SendCtrlVPaste();

                    ThreadPool.QueueUserWorkItem(__ =>
                    {
                        Thread.Sleep(500);
                        KeySender.SuppressClipboardMonitoring = false;
                    });
                    return;
                }

                if (item.ContentType == ClipboardContentType.Image)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            KeySender.SuppressClipboardMonitoring = true;
                            string path = ClipboardItem.ResolvePath(item.ImagePath);
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            {
                                using (var img = System.Drawing.Image.FromFile(path))
                                {
                                    System.Windows.Forms.Clipboard.SetImage(img);
                                }
                            }
                        }
                        catch { }
                    });

                    Thread.Sleep(50);
                    KeySender.SendCtrlVPaste();

                    ThreadPool.QueueUserWorkItem(__ =>
                    {
                        Thread.Sleep(500);
                        KeySender.SuppressClipboardMonitoring = false;
                    });
                    return;
                }

                if (item.ContentType == ClipboardContentType.Files)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            KeySender.SuppressClipboardMonitoring = true;
                            var files = item.FilePaths;
                            if (files.Count > 0)
                            {
                                var coll = new System.Collections.Specialized.StringCollection();
                                coll.AddRange(files.ToArray());
                                Clipboard.SetFileDropList(coll);
                            }
                        }
                        catch { }
                    });

                    Thread.Sleep(50);
                    KeySender.SendCtrlVPaste();

                    ThreadPool.QueueUserWorkItem(__ =>
                    {
                        Thread.Sleep(500);
                        KeySender.SuppressClipboardMonitoring = false;
                    });
                    return;
                }
            });
        }

        public void ShowTextTransformWindow()
        {
            try
            {
                string text = "";
                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }

                var wnd = new TextTransformWindow(text);
                wnd.Show();
                wnd.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ShowTextTransformWindow error: " + ex.Message);
            }
        }

        public void ExitApplication()
        {
            SettingsManager.StopSyncWatcher();
            SoundManager.Cleanup();
            _caretIndicatorWindow?.Close();
            _keyboardHook?.Dispose();
            _clipboardListener?.Dispose();
            _clipboardHistory?.SaveHistoryNow();
            _trayManager?.Dispose();
            _statusOsdWindow?.Close();
            _clipboardWindow?.Close();
            _appMutex?.ReleaseMutex();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SettingsManager.StopSyncWatcher();
            SoundManager.Cleanup();
            _caretIndicatorWindow?.Close();
            _keyboardHook?.Dispose();
            _clipboardListener?.Dispose();
            _clipboardHistory?.SaveHistoryNow();
            _trayManager?.Dispose();
            base.OnExit(e);
        }
    }
}
