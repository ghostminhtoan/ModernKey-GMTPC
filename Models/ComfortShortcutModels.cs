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
        PlayKeystrokeMacro,
        AudioControl,
        MonitorControl,
        WindowControl,
        SystemAction,
        ChangeLanguage,
        BlockKey,
        ReplaceKey
    }

    public class MacroKeyEvent : INotifyPropertyChanged
    {
        private int _delay = 50;
        private string _event = "Key Down";
        private string _key = "41 - A";
        private uint _keyCode = 0x41;
        private bool _extended = false;

        public int Delay
        {
            get => _delay;
            set { _delay = value; OnPropertyChanged(); }
        }

        public string Event
        {
            get => _event;
            set { _event = value; OnPropertyChanged(); }
        }

        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(); }
        }

        public uint KeyCode
        {
            get => _keyCode;
            set { _keyCode = value; OnPropertyChanged(); }
        }

        public bool Extended
        {
            get => _extended;
            set { _extended = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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

        // Play keystroke macro options
        private ObservableCollection<MacroKeyEvent> _macroEvents = new ObservableCollection<MacroKeyEvent>();
        private int _playSpeed = 100;
        private int _repetitions = 1;
        private string _activateProcess = string.Empty;

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

        public ObservableCollection<MacroKeyEvent> MacroEvents
        {
            get => _macroEvents;
            set { _macroEvents = value; OnPropertyChanged(); }
        }

        public int PlaySpeed
        {
            get => _playSpeed;
            set { _playSpeed = value; OnPropertyChanged(); }
        }

        public int Repetitions
        {
            get => _repetitions;
            set { _repetitions = value; OnPropertyChanged(); }
        }

        public string ActivateProcess
        {
            get => _activateProcess;
            set { _activateProcess = value; OnPropertyChanged(); }
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
                PlaySpeed = this.PlaySpeed,
                Repetitions = this.Repetitions,
                ActivateProcess = this.ActivateProcess,
                AudioAction = this.AudioAction,
                AudioStepSize = this.AudioStepSize
            };

            foreach (var ev in this.MacroEvents)
            {
                item.MacroEvents.Add(new MacroKeyEvent
                {
                    Delay = ev.Delay,
                    Event = ev.Event,
                    Key = ev.Key,
                    KeyCode = ev.KeyCode,
                    Extended = ev.Extended
                });
            }

            return item;
        }
    }
}
