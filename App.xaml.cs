using System;
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
                    string outPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_results.txt");
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
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                    System.IO.File.WriteAllText(logPath, err ?? "Unknown unhandled exception", Encoding.UTF8);
                }
                catch { }
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                try
                {
                    string err = ev.Exception?.ToString();
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
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

            // 2. Khởi tạo Engine và Hook
            _engine = new VietnameseEngine(_settings, _macroManager);
            _keyboardHook = new KeyboardHook(_engine, _settings);
            _keyboardHook.Start();

            // 3. Khởi tạo Tray Icon
            _trayManager = new SystemTrayManager(_settings, ShowMainWindow, ExitApplication);

            // Đồng bộ trạng thái khi phím tắt chuyển đổi chế độ V/E
            _keyboardHook.LanguageChanged += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    _trayManager.UpdateTrayIcon();
                    _trayManager.BuildContextMenu();
                    _mainWindow?.RefreshState();
                });
            };

            // Phím tắt đổi Bảng mã (F3: Unicode, F4: Tùy chọn)
            _keyboardHook.CharsetChanged += (cs) =>
            {
                Dispatcher.Invoke(() =>
                {
                    SettingsManager.SaveSettings(_settings);
                    _trayManager.BuildContextMenu();
                    _mainWindow?.RefreshState();
                });
            };

            // Phím tắt mở Bảng cài đặt (F5)
            _keyboardHook.OpenSettingsRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    ShowMainWindow();
                });
            };

            // Phím tắt mở Bảng gõ tắt (F8)
            _keyboardHook.OpenMacroTableRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    ShowMainWindow();
                    _mainWindow?.SelectMacroTab();
                });
            };

            // Phím tắt Bật/Tắt gõ tắt (F9)
            _keyboardHook.ToggleMacroRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    SettingsManager.SaveSettings(_settings);
                    _mainWindow?.RefreshState();
                });
            };

            // Phím tắt Reset engine / hook (F12)
            _keyboardHook.ResetHookRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    _engine.Reset();
                    _keyboardHook.Stop();
                    _keyboardHook.Start();
                    _trayManager.UpdateTrayIcon();
                });
            };

            _trayManager.StateChanged += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    _mainWindow?.RefreshState();
                });
            };

            // 4. Khởi tạo MainWindow
            _mainWindow = new MainWindow(_settings, _macroManager, _trayManager);

            if (_settings.OpenDialogOnStartup)
            {
                _mainWindow.Show();
            }
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_settings, _macroManager, _trayManager);
            }

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        public void ExitApplication()
        {
            _keyboardHook?.Dispose();
            _trayManager?.Dispose();
            _appMutex?.ReleaseMutex();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _keyboardHook?.Dispose();
            _trayManager?.Dispose();
            base.OnExit(e);
        }
    }
}
