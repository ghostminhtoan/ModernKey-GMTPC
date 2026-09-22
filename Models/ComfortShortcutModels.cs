using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModernKey.Models
{
    public enum ShortcutActionType
    {
        RunProgram,
        OpenUrl,
        PasteText,
        AudioControl,
        MonitorControl,
        WindowControl,
        SystemAction,
        ChangeLanguage,
        BlockKey,
        ReplaceKey,
        MouseControl,
        KeystrokeMacro,
        ChangeCase
    }

    public class ComfortShortcutItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _category = "Run program";
        private string _keyCombination = "Ctrl+Nm 1";
        private ShortcutActionType _actionType = ShortcutActionType.RunProgram;
        private string _activeScope = "In all screen modes";
        private string _soundPath = string.Empty;
        private string _label = string.Empty;
        private DateTime _lastChanged = DateTime.Now;

        // Bật / Tắt và thống kê
        private bool _isEnabled = true;
        private int _triggerCount = 0;
        private DateTime? _lastUsed = null;
        private string _targetApp = string.Empty;

        // Run program options
        private string _programPaths = string.Empty;
        private string _startInFolder = string.Empty;
        private bool _switchToAlreadyLaunched = true;

        // Open URL options
        private string _urls = string.Empty;
        private string _urlOpenType = "Default"; // Default, In New Window, Working In Background

        // Paste text options
        private string _pasteText = string.Empty;
        private bool _showTextOnKeyboard = false;
        private bool _pasteAsPlainText = false;

        // Audio control options
        private string _audioAction = "Volume up";
        private int _audioStepSize = 10;

        // Replace key options
        private string _replaceWithKey = "5B - Win";
        private bool _replaceShift = false;
        private bool _replaceCtrl = false;
        private bool _replaceAlt = false;
        private bool _replaceWin = false;

        // Window control options
        private string _windowAction = "Close window";
        private int _windowTransparency = 80;

        // Monitor control options
        private string _monitorAction = "Turn off monitor";

        // System action options
        private string _systemActionType = "Lock workstation";

        // Mouse action options
        private string _mouseAction = "Left click";

        // Keystroke macro options
        private string _macroKeystrokes = string.Empty;

        // Change case options
        private string _changeCaseMode = "UPPERCASE";

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string KeyCombination
        {
            get => _keyCombination;
            set { _keyCombination = value; OnPropertyChanged(); }
        }

        public ShortcutActionType ActionType
        {
            get => _actionType;
            set { _actionType = value; OnPropertyChanged(); }
        }

        public string ActiveScope
        {
            get => _activeScope;
            set { _activeScope = value; OnPropertyChanged(); }
        }

        public string SoundPath
        {
            get => _soundPath;
            set { _soundPath = value; OnPropertyChanged(); }
        }

        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        public DateTime LastChanged
        {
            get => _lastChanged;
            set { _lastChanged = value; OnPropertyChanged(); }
        }

        public string ProgramPaths
        {
            get => _programPaths;
            set { _programPaths = value; OnPropertyChanged(); }
        }

        public string StartInFolder
        {
            get => _startInFolder;
            set { _startInFolder = value; OnPropertyChanged(); }
        }

        public bool SwitchToAlreadyLaunched
        {
            get => _switchToAlreadyLaunched;
            set { _switchToAlreadyLaunched = value; OnPropertyChanged(); }
        }

        public string Urls
        {
            get => _urls;
            set { _urls = value; OnPropertyChanged(); }
        }

        public string UrlOpenType
        {
            get => _urlOpenType;
            set { _urlOpenType = value; OnPropertyChanged(); }
        }

        public string PasteText
        {
            get => _pasteText;
            set { _pasteText = value; OnPropertyChanged(); }
        }

        public bool ShowTextOnKeyboard
        {
            get => _showTextOnKeyboard;
            set { _showTextOnKeyboard = value; OnPropertyChanged(); }
        }

        public string AudioAction
        {
            get => _audioAction;
            set { _audioAction = value; OnPropertyChanged(); }
        }

        public int AudioStepSize
        {
            get => _audioStepSize;
            set { _audioStepSize = value; OnPropertyChanged(); }
        }

        public string ReplaceWithKey
        {
            get => _replaceWithKey;
            set { _replaceWithKey = value; OnPropertyChanged(); }
        }

        public bool ReplaceShift
        {
            get => _replaceShift;
            set { _replaceShift = value; OnPropertyChanged(); }
        }

        public bool ReplaceCtrl
        {
            get => _replaceCtrl;
            set { _replaceCtrl = value; OnPropertyChanged(); }
        }

        public bool ReplaceAlt
        {
            get => _replaceAlt;
            set { _replaceAlt = value; OnPropertyChanged(); }
        }

        public bool ReplaceWin
        {
            get => _replaceWin;
            set { _replaceWin = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public int TriggerCount
        {
            get => _triggerCount;
            set { _triggerCount = value; OnPropertyChanged(); }
        }

        public DateTime? LastUsed
        {
            get => _lastUsed;
            set { _lastUsed = value; OnPropertyChanged(); }
        }

        public string TargetApp
        {
            get => _targetApp;
            set { _targetApp = value; OnPropertyChanged(); }
        }

        public string WindowAction
        {
            get => _windowAction;
            set { _windowAction = value; OnPropertyChanged(); }
        }

        public int WindowTransparency
        {
            get => _windowTransparency;
            set { _windowTransparency = value; OnPropertyChanged(); }
        }

        public string MonitorAction
        {
            get => _monitorAction;
            set { _monitorAction = value; OnPropertyChanged(); }
        }

        public string SystemActionType
        {
            get => _systemActionType;
            set { _systemActionType = value; OnPropertyChanged(); }
        }

        public string MouseAction
        {
            get => _mouseAction;
            set { _mouseAction = value; OnPropertyChanged(); }
        }

        public string MacroKeystrokes
        {
            get => _macroKeystrokes;
            set { _macroKeystrokes = value; OnPropertyChanged(); }
        }

        public string ChangeCaseMode
        {
            get => _changeCaseMode;
            set { _changeCaseMode = value; OnPropertyChanged(); }
        }

        public bool PasteAsPlainText
        {
            get => _pasteAsPlainText;
            set { _pasteAsPlainText = value; OnPropertyChanged(); }
        }

        public uint GetReplaceTargetVk()
        {
            if (string.IsNullOrEmpty(_replaceWithKey)) return 0;
            int dashIdx = _replaceWithKey.IndexOf('-');
            string hexStr = dashIdx > 0 ? _replaceWithKey.Substring(0, dashIdx).Trim() : _replaceWithKey.Trim();
            if (uint.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber, null, out uint vk))
            {
                return vk;
            }
            return 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ComfortShortcutItem Clone()
        {
            var item = new ComfortShortcutItem
            {
                Id = Guid.NewGuid().ToString(),
                Category = this.Category,
                KeyCombination = this.KeyCombination,
                ActionType = this.ActionType,
                ActiveScope = this.ActiveScope,
                SoundPath = this.SoundPath,
                Label = this.Label,
                LastChanged = DateTime.Now,
                IsEnabled = this.IsEnabled,
                TriggerCount = this.TriggerCount,
                LastUsed = this.LastUsed,
                TargetApp = this.TargetApp,
                ProgramPaths = this.ProgramPaths,
                StartInFolder = this.StartInFolder,
                SwitchToAlreadyLaunched = this.SwitchToAlreadyLaunched,
                Urls = this.Urls,
                UrlOpenType = this.UrlOpenType,
                PasteText = this.PasteText,
                ShowTextOnKeyboard = this.ShowTextOnKeyboard,
                PasteAsPlainText = this.PasteAsPlainText,
                AudioAction = this.AudioAction,
                AudioStepSize = this.AudioStepSize,
                ReplaceWithKey = this.ReplaceWithKey,
                ReplaceShift = this.ReplaceShift,
                ReplaceCtrl = this.ReplaceCtrl,
                ReplaceAlt = this.ReplaceAlt,
                ReplaceWin = this.ReplaceWin,
                WindowAction = this.WindowAction,
                WindowTransparency = this.WindowTransparency,
                MonitorAction = this.MonitorAction,
                SystemActionType = this.SystemActionType,
                MouseAction = this.MouseAction,
                MacroKeystrokes = this.MacroKeystrokes,
                ChangeCaseMode = this.ChangeCaseMode
            };

            return item;
        }
    }
}
