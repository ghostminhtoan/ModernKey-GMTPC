using System;
using System.Diagnostics;
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

        public ClipboardHistoryManager ClipboardHistory => _clipboardHistory;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        protected override void OnStartup(StartupEventArgs e)
        {
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

            // 4. Khởi tạo MainWindow theo nhu cầu (Lazy loading giúp app siêu nhẹ ~8MB khi khởi động khay hệ thống)
            if (_settings.OpenDialogOnStartup)
            {
                ShowMainWindow();
            }
            else
            {
                TrimWorkingSet();
            }
        }

        public static void TrimWorkingSet()
        {
            // TUYỆT ĐỐI KHÔNG DÙNG SetProcessWorkingSetSize(-1, -1) vì nó ép bộ nhớ ra Pagefile trên HDD/SSD,
            // gây hàng loạt Hard Page Fault khiến bàn phím và chuột bị đóng băng trong 10 giây khi chơi game!
            try
            {
                GC.Collect(1, GCCollectionMode.Optimized);
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

        public void ExitApplication()
        {
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
            _keyboardHook?.Dispose();
            _clipboardListener?.Dispose();
            _clipboardHistory?.SaveHistoryNow();
            _trayManager?.Dispose();
            base.OnExit(e);
        }
    }
}
