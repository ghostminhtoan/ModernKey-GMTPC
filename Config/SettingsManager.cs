using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
using ModernKey.Core;
using ModernKey.Models;

namespace ModernKey.Config
{
    public static class SettingsManager
    {
        private const string AppName = "ModernKeyGMTPC";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static string GetAppDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        public static bool IsPortableMode()
        {
            string portableMarker = Path.Combine(GetAppDirectory(), ".portable");
            return Directory.Exists(portableMarker) || File.Exists(portableMarker);
        }

        public static string CustomSyncDirectory { get; set; } = null;
        private static FileSystemWatcher _syncWatcher = null;
        private static DateTime _lastWatcherTrigger = DateTime.MinValue;
        public static event Action OnExternalSyncUpdate;

        public static void SetupSyncWatcher(string syncFolder)
        {
            try
            {
                if (_syncWatcher != null)
                {
                    _syncWatcher.EnableRaisingEvents = false;
                    _syncWatcher.Dispose();
                    _syncWatcher = null;
                }

                if (!string.IsNullOrEmpty(syncFolder) && Directory.Exists(syncFolder))
                {
                    CustomSyncDirectory = syncFolder;
                    _syncWatcher = new FileSystemWatcher(syncFolder)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                        Filter = "*.*",
                        EnableRaisingEvents = true
                    };

                    FileSystemEventHandler handler = (s, e) =>
                    {
                        string ext = Path.GetExtension(e.Name)?.ToLowerInvariant();
                        if (ext == ".ini" || ext == ".txt")
                        {
                            if ((DateTime.Now - _lastWatcherTrigger).TotalMilliseconds > 800)
                            {
                                _lastWatcherTrigger = DateTime.Now;
                                OnExternalSyncUpdate?.Invoke();
                            }
                        }
                    };

                    _syncWatcher.Changed += handler;
                    _syncWatcher.Created += handler;
                }
                else
                {
                    CustomSyncDirectory = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error setting up sync watcher: " + ex.Message);
            }
        }

        public static void StopSyncWatcher()
        {
            try
            {
                if (_syncWatcher != null)
                {
                    _syncWatcher.EnableRaisingEvents = false;
                    _syncWatcher.Dispose();
                    _syncWatcher = null;
                }
            }
            catch { }
        }

        public static void ExportToSyncFolder(string targetFolder, AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                string srcIni = Path.Combine(GetConfigDirectory(), "modernkey.ini");
                string targetIni = Path.Combine(targetFolder, "modernkey.ini");
                if (File.Exists(srcIni))
                {
                    File.Copy(srcIni, targetIni, true);
                }
                else
                {
                    SaveSettings(settings);
                    if (File.Exists(srcIni)) File.Copy(srcIni, targetIni, true);
                }

                string srcMacro = Path.Combine(GetConfigDirectory(), "macro.txt");
                string targetMacro = Path.Combine(targetFolder, "macro.txt");
                if (File.Exists(srcMacro))
                {
                    File.Copy(srcMacro, targetMacro, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ExportToSyncFolder error: " + ex.Message);
            }
        }

        public static string GetConfigDirectory()
        {
            if (!string.IsNullOrEmpty(CustomSyncDirectory) && Directory.Exists(CustomSyncDirectory))
            {
                return CustomSyncDirectory;
            }

            if (IsPortableMode())
            {
                string portableDir = Path.Combine(GetAppDirectory(), ".portable");
                if (!Directory.Exists(portableDir))
                {
                    try
                    {
                        Directory.CreateDirectory(portableDir);
                    }
                    catch { }
                }
                return Directory.Exists(portableDir) ? portableDir : GetAppDirectory();
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        public static string GetConfigFilePath()
        {
            string cfgDir = GetConfigDirectory();
            string iniPath = Path.Combine(cfgDir, "settings.ini");

            // Nếu ở portable mode mà trong .portable chưa có settings.ini nhưng ngoài app dir có thì sao chép vào
            if (IsPortableMode() && !File.Exists(iniPath))
            {
                string legacyAppIni = Path.Combine(GetAppDirectory(), "settings.ini");
                if (File.Exists(legacyAppIni))
                {
                    try
                    {
                        File.Copy(legacyAppIni, iniPath, false);
                    }
                    catch { }
                }
            }

            return iniPath;
        }

        public static string GetMacroFilePath()
        {
            string cfgDir = GetConfigDirectory();

            if (IsPortableMode())
            {
                // Trong chế độ portable: lưu và đọc trực tiếp từ thư mục .portable
                string pPortableWpf = Path.Combine(cfgDir, "openkeymacro wpf.txt");
                if (File.Exists(pPortableWpf)) return pPortableWpf;

                string pPortableOpenKey = Path.Combine(cfgDir, "openkeymacro.txt");
                if (File.Exists(pPortableOpenKey)) return pPortableOpenKey;

                // Tự động sao chép từ thư mục cha nếu có sẵn
                string pAppWpf = Path.Combine(GetAppDirectory(), "openkeymacro wpf.txt");
                if (File.Exists(pAppWpf))
                {
                    try
                    {
                        File.Copy(pAppWpf, pPortableWpf, false);
                        return pPortableWpf;
                    }
                    catch
                    {
                        return pAppWpf;
                    }
                }

                string pAppOpenKey = Path.Combine(GetAppDirectory(), "openkeymacro.txt");
                if (File.Exists(pAppOpenKey))
                {
                    try
                    {
                        File.Copy(pAppOpenKey, pPortableWpf, false);
                        return pPortableWpf;
                    }
                    catch
                    {
                        return pAppOpenKey;
                    }
                }

                return pPortableWpf;
            }

            // Chế độ thông thường (AppData)
            // 1. Trong thư mục Config (AppData)
            string pConfigWpf = Path.Combine(cfgDir, "openkeymacro wpf.txt");
            if (File.Exists(pConfigWpf)) return pConfigWpf;

            string pConfig = Path.Combine(cfgDir, "openkeymacro.txt");
            if (File.Exists(pConfig)) return pConfig;

            string pLegacy = Path.Combine(cfgDir, "macro.txt");
            if (File.Exists(pLegacy)) return pLegacy;

            // 2. Thử tại app directory
            string pAppDirWpf = Path.Combine(GetAppDirectory(), "openkeymacro wpf.txt");
            if (File.Exists(pAppDirWpf)) return pAppDirWpf;

            string pAppDirOpenKey = Path.Combine(GetAppDirectory(), "openkeymacro.txt");
            if (File.Exists(pAppDirOpenKey)) return pAppDirOpenKey;

            return pConfigWpf;
        }

        public static AppSettings LoadSettings()
        {
            var settings = new AppSettings();
            string path = GetConfigFilePath();
            if (!File.Exists(path))
            {
                return settings;
            }

            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                        continue;

                    int idx = trimmed.IndexOf('=');
                    if (idx <= 0) continue;

                    string key = trimmed.Substring(0, idx).Trim();
                    string val = trimmed.Substring(idx + 1).Trim();

                    switch (key)
                    {
                        case "InputMethod":
                            if (Enum.TryParse<InputMethod>(val, out var im)) settings.CurrentInputMethod = im;
                            break;
                        case "Charset":
                            if (Enum.TryParse<Charset>(val, out var cs)) settings.CurrentCharset = cs;
                            break;
                        case "SwitchMode":
                            if (Enum.TryParse<SwitchKeyMode>(val, out var sm)) settings.SwitchMode = sm;
                            break;
                        case "SwitchCtrl":
                            if (bool.TryParse(val, out var sctrl)) settings.SwitchCtrl = sctrl;
                            break;
                        case "SwitchAlt":
                            if (bool.TryParse(val, out var salt)) settings.SwitchAlt = salt;
                            break;
                        case "SwitchWin":
                            if (bool.TryParse(val, out var swin)) settings.SwitchWin = swin;
                            break;
                        case "SwitchShift":
                            if (bool.TryParse(val, out var sshift)) settings.SwitchShift = sshift;
                            break;
                        case "SwitchKeyChar":
                            settings.SwitchKeyChar = val;
                            break;
                        case "IsVietnamese":
                            if (bool.TryParse(val, out var iv)) settings.IsVietnamese = iv;
                            break;
                        case "CheckSpelling":
                            if (bool.TryParse(val, out var csb)) settings.CheckSpelling = csb;
                            break;
                        case "UseMacro":
                            if (bool.TryParse(val, out var um)) settings.UseMacro = um;
                            break;
                        case "StartWithWindows":
                            if (bool.TryParse(val, out var sw)) settings.StartWithWindows = sw;
                            break;
                        case "StartAsAdmin":
                            if (bool.TryParse(val, out var sa)) settings.StartAsAdmin = sa;
                            break;
                        case "OpenDialogOnStartup":
                            if (bool.TryParse(val, out var od)) settings.OpenDialogOnStartup = od;
                            break;
                        case "SendViaClipboard":
                            if (bool.TryParse(val, out var svc)) settings.SendViaClipboard = svc;
                            break;
                        case "ModernDarkTheme":
                            if (bool.TryParse(val, out var mdt)) settings.ModernDarkTheme = mdt;
                            break;
                        case "ModernToneRules":
                            if (bool.TryParse(val, out var mtr)) settings.ModernToneRules = mtr;
                            break;
                        case "RestoreIfWrongSpelling":
                            if (bool.TryParse(val, out var rws)) settings.RestoreIfWrongSpelling = rws;
                            break;
                        case "SwitchBeep":
                            if (bool.TryParse(val, out var sbp)) settings.SwitchBeep = sbp;
                            break;
                        case "AutoCapsMacro":
                            if (bool.TryParse(val, out var acm)) settings.AutoCapsMacro = acm;
                            break;
                        case "UseMacroInEnglish":
                            if (bool.TryParse(val, out var umie)) settings.UseMacroInEnglish = umie;
                            break;
                        case "MacroTriggerMask":
                            if (int.TryParse(val, out var mtm)) settings.MacroTriggerMask = mtm;
                            break;
                        case "AllowConsonantZFWJ":
                            if (bool.TryParse(val, out var zfwj)) settings.AllowConsonantZFWJ = zfwj;
                            break;
                        case "FixRecommendBrowser":
                            if (bool.TryParse(val, out var frb)) settings.FixRecommendBrowser = frb;
                            break;
                        case "UpperCaseFirstChar":
                            if (bool.TryParse(val, out var ufc)) settings.UpperCaseFirstChar = ufc;
                            break;
                        case "AllowNumberInWordBreak":
                            if (bool.TryParse(val, out var anw)) settings.AllowNumberInWordBreak = anw;
                            break;
                        case "ShortcutModifier":
                            if (int.TryParse(val, out var scm)) settings.ShortcutModifier = scm;
                            break;
                        case "ShortcutEnableMask":
                            if (int.TryParse(val, out var scem)) settings.ShortcutEnableMask = scem;
                            break;
                        case "ShortcutF4Charset":
                            if (Enum.TryParse<Charset>(val, out var scfc)) settings.ShortcutF4Charset = scfc;
                            break;
                        case "ShortcutF6InputMethod":
                            if (Enum.TryParse<InputMethod>(val, out var scf6)) settings.ShortcutF6InputMethod = scf6;
                            break;
                        case "ShortcutF7InputMethod":
                            if (Enum.TryParse<InputMethod>(val, out var scf7)) settings.ShortcutF7InputMethod = scf7;
                            break;
                        case "GameModeCtrl":
                            if (bool.TryParse(val, out var gmctrl)) settings.GameModeCtrl = gmctrl;
                            break;
                        case "GameModeAlt":
                            if (bool.TryParse(val, out var gmalt)) settings.GameModeAlt = gmalt;
                            break;
                        case "GameModeWin":
                            if (bool.TryParse(val, out var gmwin)) settings.GameModeWin = gmwin;
                            break;
                        case "GameModeShift":
                            if (bool.TryParse(val, out var gmshift)) settings.GameModeShift = gmshift;
                            break;
                        case "GameModeKeyChar":
                            settings.GameModeKeyChar = val ?? "F11";
                            break;
                        case "QuickTextCtrl":
                            if (bool.TryParse(val, out var qtctrl)) settings.QuickTextCtrl = qtctrl;
                            break;
                        case "QuickTextAlt":
                            if (bool.TryParse(val, out var qtalt)) settings.QuickTextAlt = qtalt;
                            break;
                        case "QuickTextWin":
                            if (bool.TryParse(val, out var qtwin)) settings.QuickTextWin = qtwin;
                            break;
                        case "QuickTextShift":
                            if (bool.TryParse(val, out var qtshift)) settings.QuickTextShift = qtshift;
                            break;
                        case "QuickTextKeyChar":
                            settings.QuickTextKeyChar = val ?? "U";
                            break;
                        case "ClipboardBlurMode":
                            settings.ClipboardBlurMode = string.IsNullOrEmpty(val) ? "None" : val;
                            break;
                        case "ClipboardBlurRadius":
                            if (double.TryParse(val, out var cbr)) settings.ClipboardBlurRadius = cbr;
                            break;
                        case "ClipboardPixelateSize":
                            if (double.TryParse(val, out var cps)) settings.ClipboardPixelateSize = cps;
                            break;
                        case "AutoExcludeEnabled":
                            if (bool.TryParse(val, out var aee)) settings.AutoExcludeEnabled = aee;
                            break;
                        case "ExcludedApps":
                            if (!string.IsNullOrEmpty(val))
                            {
                                settings.ExcludedApps = new List<string>(val.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries));
                            }
                            break;
                        case "EnableStatusOsd":
                            if (bool.TryParse(val, out var eso)) settings.EnableStatusOsd = eso;
                            break;
                        case "SmartCodePassthrough":
                            if (bool.TryParse(val, out var scp)) settings.SmartCodePassthrough = scp;
                            break;
                        case "EscKeyUndo":
                            if (bool.TryParse(val, out var eku)) settings.EscKeyUndo = eku;
                            break;
                        case "EnableClipboardHistory":
                            if (bool.TryParse(val, out var ech)) settings.EnableClipboardHistory = ech;
                            break;
                        case "ClipboardAutoHide":
                            if (bool.TryParse(val, out var cah)) settings.ClipboardAutoHide = cah;
                            break;
                        case "ClipboardAlwaysOnTop":
                            if (bool.TryParse(val, out var caot)) settings.ClipboardAlwaysOnTop = caot;
                            break;
                        case "ClipboardIgnoreDuplicates":
                            if (bool.TryParse(val, out var cid)) settings.ClipboardIgnoreDuplicates = cid;
                            break;
                        case "ClipboardPasteAsPlainText":
                            if (bool.TryParse(val, out var cpp)) settings.ClipboardPasteAsPlainText = cpp;
                            break;
                        case "ClipboardMaxItems":
                            if (int.TryParse(val, out var cmi)) settings.ClipboardMaxItems = (cmi >= 5 && cmi <= 99999) ? cmi : 200;
                            break;
                        case "ClipboardMergeNumbering":
                            if (int.TryParse(val, out var cmn)) settings.ClipboardMergeNumbering = cmn;
                            break;
                        case "ClipboardMergePrefixDash":
                            if (bool.TryParse(val, out var cmpd)) settings.ClipboardMergePrefixDash = cmpd;
                            break;
                        case "ClipboardMergePrefixArrow":
                            if (bool.TryParse(val, out var cmpa)) settings.ClipboardMergePrefixArrow = cmpa;
                            break;
                        case "ClipboardMergePrefixImplies":
                            if (bool.TryParse(val, out var cmpi)) settings.ClipboardMergePrefixImplies = cmpi;
                            break;
                        case "ClipboardMergePrefixAsterisk":
                            if (bool.TryParse(val, out var cmpas)) settings.ClipboardMergePrefixAsterisk = cmpas;
                            break;
                        case "ClipboardMergeDoubleSpacing":
                            if (bool.TryParse(val, out var cmds)) settings.ClipboardMergeDoubleSpacing = cmds;
                            break;
                        case "IsCompactMode":
                            if (bool.TryParse(val, out var icm)) settings.IsCompactMode = icm;
                            break;
                        case "ActiveProfile":
                            if (!string.IsNullOrEmpty(val)) settings.ActiveProfile = val;
                            break;
                        case "SmartEnglishBypass":
                            if (bool.TryParse(val, out var seb)) settings.SmartEnglishBypass = seb;
                            break;
                        case "GameModeEnabled":
                            if (bool.TryParse(val, out var gme)) settings.GameModeEnabled = gme;
                            break;
                        case "OneKeyUndoRaw":
                            if (bool.TryParse(val, out var oku)) settings.OneKeyUndoRaw = oku;
                            break;
                        case "SensitiveDataMasking":
                            if (bool.TryParse(val, out var sdm)) settings.SensitiveDataMasking = sdm;
                            break;
                        case "SensitiveAutoPurgeMinutes":
                            if (int.TryParse(val, out var sapm)) settings.SensitiveAutoPurgeMinutes = sapm;
                            break;
                        case "DynamicMacroEnabled":
                            if (bool.TryParse(val, out var dme)) settings.DynamicMacroEnabled = dme;
                            break;
                        case "InlineMathEvaluator":
                            if (bool.TryParse(val, out var ime)) settings.InlineMathEvaluator = ime;
                            break;
                        case "EnableCaretIndicator":
                            if (bool.TryParse(val, out var eci)) settings.EnableCaretIndicator = eci;
                            break;
                        case "EnableKeySound":
                            if (bool.TryParse(val, out var eks)) settings.EnableKeySound = eks;
                            break;
                        case "SyncFolderPath":
                            settings.SyncFolderPath = val ?? string.Empty;
                            break;
                        case "CustomRules":
                            if (!string.IsNullOrEmpty(val))
                            {
                                var rules = new List<CustomInputRule>();
                                var items = val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var it in items)
                                {
                                    var parts = it.Split(':');
                                    if (parts.Length == 2 && int.TryParse(parts[0], out int keyCode) && int.TryParse(parts[1], out int act))
                                    {
                                        rules.Add(new CustomInputRule((char)keyCode, act));
                                    }
                                }
                                if (rules.Count > 0)
                                {
                                    settings.CustomRules = rules;
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error reading settings: " + ex.Message);
            }

            // Tránh đúp OSD: Nếu cả EnableStatusOsd và EnableCaretIndicator đều true, ưu tiên CaretIndicator và tắt StatusOsd
            if (settings.EnableStatusOsd && settings.EnableCaretIndicator)
            {
                settings.EnableStatusOsd = false;
            }

            return settings;
        }

        public static void SaveSettings(AppSettings settings)
        {
            if (settings == null) return;
            string path = GetConfigFilePath();
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ModernKey GMTPC Configuration File");
                sb.AppendLine("InputMethod=" + settings.CurrentInputMethod);
                sb.AppendLine("Charset=" + settings.CurrentCharset);
                sb.AppendLine("SwitchMode=" + settings.SwitchMode);
                sb.AppendLine("SwitchCtrl=" + settings.SwitchCtrl);
                sb.AppendLine("SwitchAlt=" + settings.SwitchAlt);
                sb.AppendLine("SwitchWin=" + settings.SwitchWin);
                sb.AppendLine("SwitchShift=" + settings.SwitchShift);
                sb.AppendLine("SwitchKeyChar=" + (settings.SwitchKeyChar ?? ""));
                sb.AppendLine("IsVietnamese=" + settings.IsVietnamese);
                sb.AppendLine("CheckSpelling=" + settings.CheckSpelling);
                sb.AppendLine("UseMacro=" + settings.UseMacro);
                sb.AppendLine("StartWithWindows=" + settings.StartWithWindows);
                sb.AppendLine("StartAsAdmin=" + settings.StartAsAdmin);
                sb.AppendLine("OpenDialogOnStartup=" + settings.OpenDialogOnStartup);
                sb.AppendLine("SendViaClipboard=" + settings.SendViaClipboard);
                sb.AppendLine("ModernDarkTheme=" + settings.ModernDarkTheme);
                sb.AppendLine("ModernToneRules=" + settings.ModernToneRules);
                sb.AppendLine("RestoreIfWrongSpelling=" + settings.RestoreIfWrongSpelling);
                sb.AppendLine("SwitchBeep=" + settings.SwitchBeep);
                sb.AppendLine("AutoCapsMacro=" + settings.AutoCapsMacro);
                sb.AppendLine("UseMacroInEnglish=" + settings.UseMacroInEnglish);
                sb.AppendLine("MacroTriggerMask=" + settings.MacroTriggerMask);
                sb.AppendLine("AllowConsonantZFWJ=" + settings.AllowConsonantZFWJ);
                sb.AppendLine("FixRecommendBrowser=" + settings.FixRecommendBrowser);
                sb.AppendLine("UpperCaseFirstChar=" + settings.UpperCaseFirstChar);
                sb.AppendLine("AllowNumberInWordBreak=" + settings.AllowNumberInWordBreak);
                sb.AppendLine("ShortcutModifier=" + settings.ShortcutModifier);
                sb.AppendLine("ShortcutEnableMask=" + settings.ShortcutEnableMask);
                sb.AppendLine("ShortcutF4Charset=" + settings.ShortcutF4Charset);
                sb.AppendLine("ShortcutF6InputMethod=" + settings.ShortcutF6InputMethod);
                sb.AppendLine("ShortcutF7InputMethod=" + settings.ShortcutF7InputMethod);
                sb.AppendLine("GameModeCtrl=" + settings.GameModeCtrl);
                sb.AppendLine("GameModeAlt=" + settings.GameModeAlt);
                sb.AppendLine("GameModeWin=" + settings.GameModeWin);
                sb.AppendLine("GameModeShift=" + settings.GameModeShift);
                sb.AppendLine("GameModeKeyChar=" + (settings.GameModeKeyChar ?? "F11"));
                sb.AppendLine("QuickTextCtrl=" + settings.QuickTextCtrl);
                sb.AppendLine("QuickTextAlt=" + settings.QuickTextAlt);
                sb.AppendLine("QuickTextWin=" + settings.QuickTextWin);
                sb.AppendLine("QuickTextShift=" + settings.QuickTextShift);
                sb.AppendLine("QuickTextKeyChar=" + (settings.QuickTextKeyChar ?? "U"));
                sb.AppendLine("ClipboardBlurMode=" + settings.ClipboardBlurMode);
                sb.AppendLine("ClipboardBlurRadius=" + settings.ClipboardBlurRadius);
                sb.AppendLine("ClipboardPixelateSize=" + settings.ClipboardPixelateSize);
                sb.AppendLine("AutoExcludeEnabled=" + settings.AutoExcludeEnabled);
                sb.AppendLine("ExcludedApps=" + (settings.ExcludedApps != null ? string.Join(";", settings.ExcludedApps) : ""));
                sb.AppendLine("EnableStatusOsd=" + settings.EnableStatusOsd);
                sb.AppendLine("EnableClipboardHistory=" + settings.EnableClipboardHistory);
                sb.AppendLine("ClipboardAutoHide=" + settings.ClipboardAutoHide);
                sb.AppendLine("ClipboardAlwaysOnTop=" + settings.ClipboardAlwaysOnTop);
                sb.AppendLine("ClipboardIgnoreDuplicates=" + settings.ClipboardIgnoreDuplicates);
                sb.AppendLine("ClipboardPasteAsPlainText=" + settings.ClipboardPasteAsPlainText);
                sb.AppendLine("ClipboardMaxItems=" + settings.ClipboardMaxItems);
                sb.AppendLine("ClipboardMergeNumbering=" + settings.ClipboardMergeNumbering);
                sb.AppendLine("ClipboardMergePrefixDash=" + settings.ClipboardMergePrefixDash);
                sb.AppendLine("ClipboardMergePrefixArrow=" + settings.ClipboardMergePrefixArrow);
                sb.AppendLine("ClipboardMergePrefixImplies=" + settings.ClipboardMergePrefixImplies);
                sb.AppendLine("ClipboardMergePrefixAsterisk=" + settings.ClipboardMergePrefixAsterisk);
                sb.AppendLine("ClipboardMergeDoubleSpacing=" + settings.ClipboardMergeDoubleSpacing);
                sb.AppendLine("SmartCodePassthrough=" + settings.SmartCodePassthrough);
                sb.AppendLine("EscKeyUndo=" + settings.EscKeyUndo);
                sb.AppendLine("IsCompactMode=" + settings.IsCompactMode);
                sb.AppendLine("ActiveProfile=" + (settings.ActiveProfile ?? "Office"));
                sb.AppendLine("SmartEnglishBypass=" + settings.SmartEnglishBypass);
                sb.AppendLine("GameModeEnabled=" + settings.GameModeEnabled);
                sb.AppendLine("OneKeyUndoRaw=" + settings.OneKeyUndoRaw);
                sb.AppendLine("SensitiveDataMasking=" + settings.SensitiveDataMasking);
                sb.AppendLine("SensitiveAutoPurgeMinutes=" + settings.SensitiveAutoPurgeMinutes);
                sb.AppendLine("DynamicMacroEnabled=" + settings.DynamicMacroEnabled);
                sb.AppendLine("InlineMathEvaluator=" + settings.InlineMathEvaluator);
                sb.AppendLine("EnableCaretIndicator=" + settings.EnableCaretIndicator);
                sb.AppendLine("EnableKeySound=" + settings.EnableKeySound);
                sb.AppendLine("SyncFolderPath=" + (settings.SyncFolderPath ?? ""));

                if (settings.CustomRules != null && settings.CustomRules.Count > 0)
                {
                    var ruleStrings = new List<string>();
                    foreach (var r in settings.CustomRules)
                    {
                        ruleStrings.Add($"{(int)r.Key}:{r.Action}");
                    }
                    sb.AppendLine("CustomRules=" + string.Join(",", ruleStrings));
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error saving settings: " + ex.Message);
            }
        }

        public static bool ExportProfileJson(AppSettings settings, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"ActiveProfile\": \"{settings.ActiveProfile}\",");
                sb.AppendLine($"  \"InputMethod\": \"{settings.CurrentInputMethod}\",");
                sb.AppendLine($"  \"Charset\": \"{settings.CurrentCharset}\",");
                sb.AppendLine($"  \"SwitchMode\": \"{settings.SwitchMode}\",");
                sb.AppendLine($"  \"IsVietnamese\": {settings.IsVietnamese.ToString().ToLower()},");
                sb.AppendLine($"  \"CheckSpelling\": {settings.CheckSpelling.ToString().ToLower()},");
                sb.AppendLine($"  \"RestoreIfWrongSpelling\": {settings.RestoreIfWrongSpelling.ToString().ToLower()},");
                sb.AppendLine($"  \"ModernToneRules\": {settings.ModernToneRules.ToString().ToLower()},");
                sb.AppendLine($"  \"UseMacro\": {settings.UseMacro.ToString().ToLower()},");
                sb.AppendLine($"  \"AutoCapsMacro\": {settings.AutoCapsMacro.ToString().ToLower()},");
                sb.AppendLine($"  \"SmartCodePassthrough\": {settings.SmartCodePassthrough.ToString().ToLower()},");
                sb.AppendLine($"  \"EscKeyUndo\": {settings.EscKeyUndo.ToString().ToLower()},");
                sb.AppendLine($"  \"EnableStatusOsd\": {settings.EnableStatusOsd.ToString().ToLower()},");
                sb.AppendLine($"  \"AutoExcludeEnabled\": {settings.AutoExcludeEnabled.ToString().ToLower()},");
                sb.AppendLine($"  \"ExcludedApps\": \"{string.Join(";", settings.ExcludedApps ?? new List<string>())}\"");
                sb.AppendLine("}");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error exporting JSON: " + ex.Message);
                return false;
            }
        }

        public static bool ImportProfileJson(string filePath, AppSettings targetSettings)
        {
            try
            {
                if (!File.Exists(filePath) || targetSettings == null) return false;
                string text = File.ReadAllText(filePath, Encoding.UTF8);

                foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim().TrimEnd(',');
                    int colonIdx = trimmed.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string k = trimmed.Substring(0, colonIdx).Trim().Trim('"');
                    string v = trimmed.Substring(colonIdx + 1).Trim().Trim('"');

                    switch (k)
                    {
                        case "ActiveProfile": targetSettings.ActiveProfile = v; break;
                        case "InputMethod": if (Enum.TryParse<InputMethod>(v, out var im)) targetSettings.CurrentInputMethod = im; break;
                        case "Charset": if (Enum.TryParse<Charset>(v, out var cs)) targetSettings.CurrentCharset = cs; break;
                        case "SwitchMode": if (Enum.TryParse<SwitchKeyMode>(v, out var sm)) targetSettings.SwitchMode = sm; break;
                        case "IsVietnamese": if (bool.TryParse(v, out var iv)) targetSettings.IsVietnamese = iv; break;
                        case "CheckSpelling": if (bool.TryParse(v, out var csb)) targetSettings.CheckSpelling = csb; break;
                        case "RestoreIfWrongSpelling": if (bool.TryParse(v, out var rws)) targetSettings.RestoreIfWrongSpelling = rws; break;
                        case "ModernToneRules": if (bool.TryParse(v, out var mtr)) targetSettings.ModernToneRules = mtr; break;
                        case "UseMacro": if (bool.TryParse(v, out var um)) targetSettings.UseMacro = um; break;
                        case "AutoCapsMacro": if (bool.TryParse(v, out var acm)) targetSettings.AutoCapsMacro = acm; break;
                        case "SmartCodePassthrough": if (bool.TryParse(v, out var scp)) targetSettings.SmartCodePassthrough = scp; break;
                        case "EscKeyUndo": if (bool.TryParse(v, out var eku)) targetSettings.EscKeyUndo = eku; break;
                        case "EnableStatusOsd": if (bool.TryParse(v, out var eso)) targetSettings.EnableStatusOsd = eso; break;
                        case "AutoExcludeEnabled": if (bool.TryParse(v, out var aee)) targetSettings.AutoExcludeEnabled = aee; break;
                        case "ExcludedApps":
                            targetSettings.ExcludedApps = new List<string>(v.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries));
                            break;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error importing JSON: " + ex.Message);
                return false;
            }
        }

        public static void ApplyStartupConfig(bool enable, bool asAdmin)
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            try
            {
                if (enable)
                {
                    if (asAdmin)
                    {
                        // Remove HKCU run key to avoid double launch
                        using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                        {
                            key?.DeleteValue(AppName, false);
                        }

                        // Create elevated task
                        string cmd = $"/create /sc onlogon /tn \"{AppName}\" /rl highest /tr \"\\\"{exePath}\\\"\" /f";
                        var psi = new ProcessStartInfo("schtasks", cmd)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi)?.WaitForExit();
                    }
                    else
                    {
                        // Remove scheduled task if any
                        var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{AppName}\" /f")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi)?.WaitForExit();

                        // Write to HKCU
                        using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                        {
                            key?.SetValue(AppName, $"\"{exePath}\"");
                        }
                    }
                }
                else
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                    {
                        key?.DeleteValue(AppName, false);
                    }

                    var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{AppName}\" /f")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi)?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error configuring startup: " + ex.Message);
            }
        }
    }
}
