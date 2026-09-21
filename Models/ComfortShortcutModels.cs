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
        ReplaceKey
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

        // Audio control options
        private string _audioAction = "Volume up";
        private int _audioStepSize = 10;

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
                ProgramPaths = this.ProgramPaths,
                StartInFolder = this.StartInFolder,
                SwitchToAlreadyLaunched = this.SwitchToAlreadyLaunched,
                Urls = this.Urls,
                UrlOpenType = this.UrlOpenType,
                PasteText = this.PasteText,
                ShowTextOnKeyboard = this.ShowTextOnKeyboard,
                AudioAction = this.AudioAction,
                AudioStepSize = this.AudioStepSize
            };

            return item;
        }
    }
}
