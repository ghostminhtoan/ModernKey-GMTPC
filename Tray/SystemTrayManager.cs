using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ModernKey.Config;
using ModernKey.Core;
using ModernKey.Models;

namespace ModernKey.Tray
{
    public class SystemTrayManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly AppSettings _settings;
        private readonly Action<int> _showMainWindowAction;
        private readonly Action _showClipboardAction;
        private readonly Action _exitAction;
        private readonly Action _quickConvertAction;
        private readonly Action _showComfortShortcutsAction;

        private Icon _iconViet;
        private Icon _iconEng;

        public event Action StateChanged;

        public SystemTrayManager(AppSettings settings, Action<int> showMainWindowAction, Action exitAction, Action showClipboardAction = null, Action quickConvertAction = null, Action showComfortShortcutsAction = null)
        {
            _settings = settings;
            _showMainWindowAction = showMainWindowAction;
            _exitAction = exitAction;
            _showClipboardAction = showClipboardAction;
            _quickConvertAction = quickConvertAction;
            _showComfortShortcutsAction = showComfortShortcutsAction;

            _notifyIcon = new NotifyIcon();
            LoadIcons();

            _notifyIcon.Visible = true;
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.DoubleClick += (s, e) => _showMainWindowAction?.Invoke(-1);

            UpdateTrayIcon();
            BuildContextMenu();
        }

        public SystemTrayManager(AppSettings settings, Action showMainWindowAction, Action exitAction, Action showClipboardAction = null)
            : this(settings, _ => showMainWindowAction?.Invoke(), exitAction, showClipboardAction, null)
        {
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 2500)
        {
            try
            {
                if (_notifyIcon != null && _notifyIcon.Visible)
                {
                    _notifyIcon.ShowBalloonTip(timeoutMs, title, message, icon);
                }
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static Icon CreateCyberTrayIcon(string text, Color neonTextColor, Color neonBorderColor, Color bgColor)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // Nền bo góc 5px vừa vặn chiếm trọn diện tích icon tray
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    float r = 5f;
                    RectangleF rect = new RectangleF(0, 0, 31, 31);
                    path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                    path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                    path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                    path.CloseFigure();

                    using (var brushBg = new SolidBrush(bgColor))
                    {
                        g.FillPath(brushBg, path);
                    }

                    // Viền phát sáng mảnh tinh xảo 1.2px
                    using (var penBorder = new Pen(neonBorderColor, 1.2f))
                    {
                        g.DrawPath(penBorder, path);
                    }
                }

                // Font chữ to bản, đậm nét, dễ nhìn rõ từ xa trên Taskbar Windows
                Font font = null;
                try
                {
                    font = new Font("Segoe UI Black", 18.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                }
                catch
                {
                    font = new Font("Arial", 19f, FontStyle.Bold, GraphicsUnit.Pixel);
                }

                using (font)
                using (var brushText = new SolidBrush(neonTextColor))
                using (var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    var textRect = new RectangleF(-0.5f, 0.5f, 33f, 31f);
                    g.DrawString(text, font, brushText, textRect, sf);
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);
                Icon result = (Icon)icon.Clone();
                DestroyIcon(hIcon);
                return result;
            }
        }

        private void LoadIcons()
        {
            try
            {
                // [VI] Chữ to rõ ràng, màu Tím Sáng rực rỡ (Bright Neon Purple / Violet), nền đen ánh tím
                _iconViet = CreateCyberTrayIcon(
                    "VI",
                    Color.FromArgb(235, 125, 255),    // Bright Neon Purple (Tím sáng rực rỡ)
                    Color.FromArgb(195, 75, 255),     // Neon Violet Border
                    Color.FromArgb(20, 6, 28)         // Cyber Dark Purple BG
                );

                // [EN] Chữ to rõ ràng, màu Xanh Cyan sáng rực rỡ (Bright Neon Cyan), nền đen ánh xanh
                _iconEng = CreateCyberTrayIcon(
                    "EN",
                    Color.FromArgb(0, 240, 255),      // Bright Neon Cyan
                    Color.FromArgb(0, 175, 255),      // Electric Blue Border
                    Color.FromArgb(6, 18, 30)         // Cyber Dark Blue BG
                );
            }
            catch
            {
                _iconViet = SystemIcons.Application;
                _iconEng = SystemIcons.Information;
            }
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Click trái để chuyển đổi nhanh VI <-> EN
                _settings.IsVietnamese = !_settings.IsVietnamese;
                SettingsManager.SaveSettings(_settings);
                UpdateTrayIcon();
                BuildContextMenu();
                StateChanged?.Invoke();
            }
        }

        public void UpdateTrayIcon()
        {
            _notifyIcon.Icon = _settings.IsVietnamese ? _iconViet : _iconEng;
            string langStr = _settings.IsVietnamese ? "Tiếng Việt [VI]" : "English [EN]";
            string modeStr = _settings.CurrentInputMethod == InputMethod.TuBinhTran ? "Tư Bình Trần" : _settings.CurrentInputMethod.ToString();
            string portTag = SettingsManager.IsPortableMode() ? " (Portable)" : "";
            _notifyIcon.Text = $"ModernKey GMTPC [{langStr} - {modeStr}]{portTag}";
        }

        public void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            // 1. Nhóm Bật/Tắt tính năng nhanh (Chuẩn OpenKey C++ - Ảnh 2)
            var itemVietnamese = new ToolStripMenuItem("Bật Tiếng Việt", null, (s, e) =>
            {
                _settings.IsVietnamese = !_settings.IsVietnamese;
                SettingsManager.SaveSettings(_settings);
                UpdateTrayIcon();
                BuildContextMenu();
                StateChanged?.Invoke();
            })
            {
                Checked = _settings.IsVietnamese
            };
            menu.Items.Add(itemVietnamese);

            var itemSpelling = new ToolStripMenuItem("Bật kiểm tra chính tả", null, (s, e) =>
            {
                _settings.CheckSpelling = !_settings.CheckSpelling;
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
                StateChanged?.Invoke();
            })
            {
                Checked = _settings.CheckSpelling
            };
            menu.Items.Add(itemSpelling);

            var itemSmartExclusion = new ToolStripMenuItem("Bật loại trừ ứng dụng thông minh", null, (s, e) =>
            {
                _settings.SmartCodePassthrough = !_settings.SmartCodePassthrough;
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
                StateChanged?.Invoke();
            })
            {
                Checked = _settings.SmartCodePassthrough
            };
            menu.Items.Add(itemSmartExclusion);

            var itemMacro = new ToolStripMenuItem("Bật gõ tắt", null, (s, e) =>
            {
                _settings.UseMacro = !_settings.UseMacro;
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
                StateChanged?.Invoke();
            })
            {
                Checked = _settings.UseMacro
            };
            menu.Items.Add(itemMacro);

            menu.Items.Add(new ToolStripSeparator());

            // 2. Nhóm Công cụ (Chuẩn OpenKey C++ - Ảnh 2)
            var itemConfigMacro = new ToolStripMenuItem("Cấu hình gõ tắt...", null, (s, e) =>
            {
                _showMainWindowAction?.Invoke(2); // Mở Tab Gõ tắt (Tab index 2)
            });
            menu.Items.Add(itemConfigMacro);

            var itemConvertTool = new ToolStripMenuItem("Công cụ chuyển mã...", null, (s, e) =>
            {
                _showMainWindowAction?.Invoke(3); // Mở Tab Chuyển mã (Tab index 3)
            });
            menu.Items.Add(itemConvertTool);

            var itemQuickConvert = new ToolStripMenuItem("Chuyển mã nhanh", null, (s, e) =>
            {
                _quickConvertAction?.Invoke();
            });
            menu.Items.Add(itemQuickConvert);

            menu.Items.Add(new ToolStripSeparator());

            // 3. Nhóm Kiểu gõ hiển thị 1 chạm (Chuẩn OpenKey C++ - Ảnh 2)
            void SetMethod(InputMethod im)
            {
                _settings.CurrentInputMethod = im;
                SettingsManager.SaveSettings(_settings);
                UpdateTrayIcon();
                BuildContextMenu();
                StateChanged?.Invoke();
            }

            var itemTelex = new ToolStripMenuItem("Kiểu gõ Telex", null, (s, e) => SetMethod(InputMethod.Telex))
            {
                Checked = (_settings.CurrentInputMethod == InputMethod.Telex)
            };
            menu.Items.Add(itemTelex);

            var itemVni = new ToolStripMenuItem("Kiểu gõ VNI", null, (s, e) => SetMethod(InputMethod.Vni))
            {
                Checked = (_settings.CurrentInputMethod == InputMethod.Vni)
            };
            menu.Items.Add(itemVni);

            var itemSimpleTelex = new ToolStripMenuItem("Kiểu gõ Simple Telex", null, (s, e) => SetMethod(InputMethod.SimpleTelex))
            {
                Checked = (_settings.CurrentInputMethod == InputMethod.SimpleTelex)
            };
            menu.Items.Add(itemSimpleTelex);

            var itemTuBinhTran = new ToolStripMenuItem("Kiểu gõ Tự Bình Trần đơn giản", null, (s, e) => SetMethod(InputMethod.TuBinhTran))
            {
                Checked = (_settings.CurrentInputMethod == InputMethod.TuBinhTran)
            };
            menu.Items.Add(itemTuBinhTran);

            var itemCustom = new ToolStripMenuItem("Kiểu gõ Tự định nghĩa", null, (s, e) =>
            {
                SetMethod(InputMethod.Custom);
                // Mở cửa sổ cấu hình kiểu gõ tự định nghĩa
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var wnd = new CustomInputMethodWindow(_settings);
                        wnd.Show();
                        wnd.Activate();
                    }
                    catch { }
                }));
            })
            {
                Checked = (_settings.CurrentInputMethod == InputMethod.Custom)
            };
            menu.Items.Add(itemCustom);

            menu.Items.Add(new ToolStripSeparator());

            // 4. Nhóm Bảng mã hiển thị 1 chạm & Bảng mã khác (Chuẩn OpenKey C++ - Ảnh 2)
            void SetCharset(Charset cs)
            {
                _settings.CurrentCharset = cs;
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
                StateChanged?.Invoke();
            }

            var itemUnicode = new ToolStripMenuItem("Unicode dựng sẵn", null, (s, e) => SetCharset(Charset.Unicode))
            {
                Checked = (_settings.CurrentCharset == Charset.Unicode)
            };
            menu.Items.Add(itemUnicode);

            var itemTcvn3 = new ToolStripMenuItem("TCVN3 (ABC)", null, (s, e) => SetCharset(Charset.TCVN3))
            {
                Checked = (_settings.CurrentCharset == Charset.TCVN3)
            };
            menu.Items.Add(itemTcvn3);

            var itemVniWin = new ToolStripMenuItem("VNI Windows", null, (s, e) => SetCharset(Charset.VniWindows))
            {
                Checked = (_settings.CurrentCharset == Charset.VniWindows)
            };
            menu.Items.Add(itemVniWin);

            // Submenu Bảng mã khác (Chuẩn OpenKey C++ - Ảnh 2)
            var itemOtherCharsets = new ToolStripMenuItem("Bảng mã khác");
            var otherCharsets = new[]
            {
                new { Name = "Unicode tổ hợp", Charset = Charset.UnicodeCompound },
                new { Name = "VIQR", Charset = Charset.Viqr },
                new { Name = "BK HCM1", Charset = Charset.BkpHcm1 },
                new { Name = "BK HCM2", Charset = Charset.BkpHcm2 },
                new { Name = "Vietware X", Charset = Charset.VietwareX },
                new { Name = "Vietware F", Charset = Charset.VietwareF }
            };

            foreach (var oc in otherCharsets)
            {
                var csVal = oc.Charset;
                var subItem = new ToolStripMenuItem(oc.Name, null, (s, e) => SetCharset(csVal))
                {
                    Checked = (_settings.CurrentCharset == csVal)
                };
                itemOtherCharsets.DropDownItems.Add(subItem);
            }
            menu.Items.Add(itemOtherCharsets);

            menu.Items.Add(new ToolStripSeparator());

            // 5. Nhóm Khởi động cùng Windows / Admin (Chuẩn OpenKey C++ - Ảnh 2)
            var itemStartupUser = new ToolStripMenuItem("Khởi động (không quyền Admin)", null, (s, e) =>
            {
                bool willEnable = !(_settings.StartWithWindows && !_settings.StartAsAdmin);
                _settings.StartWithWindows = willEnable;
                _settings.StartAsAdmin = false;
                SettingsManager.ApplyStartupConfig(_settings.StartWithWindows, _settings.StartAsAdmin);
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
            })
            {
                Checked = (_settings.StartWithWindows && !_settings.StartAsAdmin)
            };
            menu.Items.Add(itemStartupUser);

            var itemStartupAdmin = new ToolStripMenuItem("Khởi động với quyền Admin", null, (s, e) =>
            {
                bool willEnable = !(_settings.StartWithWindows && _settings.StartAsAdmin);
                _settings.StartWithWindows = willEnable;
                _settings.StartAsAdmin = willEnable;
                SettingsManager.ApplyStartupConfig(_settings.StartWithWindows, _settings.StartAsAdmin);
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
            })
            {
                Checked = (_settings.StartWithWindows && _settings.StartAsAdmin)
            };
            menu.Items.Add(itemStartupAdmin);

            menu.Items.Add(new ToolStripSeparator());

            // 6. Nhóm Bảng điều khiển & Tiện ích ModernKey
            var itemOpen = new ToolStripMenuItem("Bảng điều khiển...", null, (s, e) => _showMainWindowAction?.Invoke(-1));
            itemOpen.Font = new Font(itemOpen.Font, FontStyle.Bold);
            menu.Items.Add(itemOpen);

            var itemClipboard = new ToolStripMenuItem("Quản lý Clipboard (Win+Ins / Ctrl+Alt+V)", null, (s, e) => _showClipboardAction?.Invoke());
            menu.Items.Add(itemClipboard);

            var itemComfort = new ToolStripMenuItem("Phím tắt Comfort Keys Pro...", null, (s, e) => _showComfortShortcutsAction?.Invoke());
            menu.Items.Add(itemComfort);

            var itemAbout = new ToolStripMenuItem("Giới thiệu ModernKey...", null, (s, e) =>
            {
                _showMainWindowAction?.Invoke(7); // Mở Tab Thông tin (Tab index 7)
            });
            menu.Items.Add(itemAbout);

            menu.Items.Add(new ToolStripSeparator());

            // 7. Nút Thoát sạch an toàn
            var itemExit = new ToolStripMenuItem("Thoát", null, (s, e) => _exitAction?.Invoke());
            menu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = menu;
        }

        public void Dispose()
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _iconViet?.Dispose();
                _iconEng?.Dispose();
            }
            catch { }
        }
    }
}
