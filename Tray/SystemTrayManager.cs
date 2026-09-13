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
        private readonly Action _showMainWindowAction;
        private readonly Action _showClipboardAction;
        private readonly Action _exitAction;

        private Icon _iconViet;
        private Icon _iconEng;

        public event Action StateChanged;

        public SystemTrayManager(AppSettings settings, Action showMainWindowAction, Action exitAction, Action showClipboardAction = null)
        {
            _settings = settings;
            _showMainWindowAction = showMainWindowAction;
            _exitAction = exitAction;
            _showClipboardAction = showClipboardAction;

            _notifyIcon = new NotifyIcon();
            LoadIcons();

            _notifyIcon.Visible = true;
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.DoubleClick += (s, e) => _showMainWindowAction?.Invoke();

            UpdateTrayIcon();
            BuildContextMenu();
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
            string modeStr = _settings.CurrentInputMethod.ToString();
            string portTag = SettingsManager.IsPortableMode() ? " (Portable)" : "";
            _notifyIcon.Text = $"ModernKey GMTPC [{langStr} - {modeStr}]{portTag}";
        }

        public void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var itemOpen = new ToolStripMenuItem("Bảng điều khiển ModernKey", null, (s, e) => _showMainWindowAction?.Invoke());
            itemOpen.Font = new Font(itemOpen.Font, FontStyle.Bold);
            menu.Items.Add(itemOpen);

            var itemClipboard = new ToolStripMenuItem("Quản lý Clipboard (Win+Ins / Ctrl+Alt+V)", null, (s, e) => _showClipboardAction?.Invoke());
            menu.Items.Add(itemClipboard);

            menu.Items.Add(new ToolStripSeparator());

            // Chuyển nhanh chế độ gõ
            var itemLang = new ToolStripMenuItem(_settings.IsVietnamese ? "Chế độ: Tiếng Việt [VI]" : "Chế độ: Tiếng Anh [EN]", null, (s, e) =>
            {
                _settings.IsVietnamese = !_settings.IsVietnamese;
                SettingsManager.SaveSettings(_settings);
                UpdateTrayIcon();
                BuildContextMenu();
                StateChanged?.Invoke();
            });
            itemLang.Checked = _settings.IsVietnamese;
            menu.Items.Add(itemLang);

            // Submenu Kiểu gõ
            var itemMethod = new ToolStripMenuItem("Kiểu gõ");
            foreach (InputMethod im in Enum.GetValues(typeof(InputMethod)))
            {
                var subItem = new ToolStripMenuItem(im.ToString(), null, (s, e) =>
                {
                    _settings.CurrentInputMethod = im;
                    SettingsManager.SaveSettings(_settings);
                    UpdateTrayIcon();
                    BuildContextMenu();
                    StateChanged?.Invoke();
                })
                {
                    Checked = (_settings.CurrentInputMethod == im)
                };
                itemMethod.DropDownItems.Add(subItem);
            }
            menu.Items.Add(itemMethod);

            // Submenu Bảng mã
            var itemCharset = new ToolStripMenuItem("Bảng mã");
            foreach (Charset cs in Enum.GetValues(typeof(Charset)))
            {
                var subItem = new ToolStripMenuItem(cs.ToString(), null, (s, e) =>
                {
                    _settings.CurrentCharset = cs;
                    SettingsManager.SaveSettings(_settings);
                    BuildContextMenu();
                    StateChanged?.Invoke();
                })
                {
                    Checked = (_settings.CurrentCharset == cs)
                };
                itemCharset.DropDownItems.Add(subItem);
            }
            menu.Items.Add(itemCharset);

            menu.Items.Add(new ToolStripSeparator());

            // Tùy chọn nhanh
            var itemSpelling = new ToolStripMenuItem("Kiểm tra chính tả", null, (s, e) =>
            {
                _settings.CheckSpelling = !_settings.CheckSpelling;
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
            })
            {
                Checked = _settings.CheckSpelling
            };
            menu.Items.Add(itemSpelling);

            var itemMacro = new ToolStripMenuItem("Cho phép gõ tắt", null, (s, e) =>
            {
                _settings.UseMacro = !_settings.UseMacro;
                SettingsManager.SaveSettings(_settings);
                BuildContextMenu();
            })
            {
                Checked = _settings.UseMacro
            };
            menu.Items.Add(itemMacro);

            menu.Items.Add(new ToolStripSeparator());

            var itemExit = new ToolStripMenuItem("Thoát", null, (s, e) => _exitAction?.Invoke());
            menu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = menu;
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _iconViet?.Dispose();
            _iconEng?.Dispose();
        }
    }
}
