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
            return File.Exists(portableMarker) || Directory.Exists(portableMarker);
        }

        public static string GetConfigDirectory()
        {
            if (IsPortableMode())
            {
                return GetAppDirectory();
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
            return Path.Combine(GetConfigDirectory(), "settings.ini");
        }

        public static string GetMacroFilePath()
        {
            string appDir = GetAppDirectory();

            // 1. Ưu tiên openkeymacro wpf.txt cạnh exe nếu tồn tại
            string pWpf = Path.Combine(appDir, "openkeymacro wpf.txt");
            if (File.Exists(pWpf)) return pWpf;

            // 2. Ưu tiên openkeymacro.txt cạnh exe nếu tồn tại
            string pOpenKey = Path.Combine(appDir, "openkeymacro.txt");
            if (File.Exists(pOpenKey)) return pOpenKey;

            // 3. Trong thư mục Config (AppData)
            string pConfigWpf = Path.Combine(GetConfigDirectory(), "openkeymacro wpf.txt");
            if (File.Exists(pConfigWpf)) return pConfigWpf;

            string pConfig = Path.Combine(GetConfigDirectory(), "openkeymacro.txt");
            if (File.Exists(pConfig)) return pConfig;

            string pLegacy = Path.Combine(GetConfigDirectory(), "macro.txt");
            if (File.Exists(pLegacy)) return pLegacy;

            // Mặc định lưu vào openkeymacro wpf.txt cạnh exe
            return pWpf;
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
