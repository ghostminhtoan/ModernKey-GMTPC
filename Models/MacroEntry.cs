using System;
using System.ComponentModel;

namespace ModernKey.Models
{
    public class MacroEntry : INotifyPropertyChanged
    {
        private string _shortcut;
        private string _replacement;

        public string Shortcut
        {
            get => _shortcut;
            set
            {
                if (_shortcut != value)
                {
                    _shortcut = value;
                    OnPropertyChanged(nameof(Shortcut));
                }
            }
        }

        public string Replacement
        {
            get => _replacement;
            set
            {
                if (_replacement != value)
                {
                    _replacement = value;
                    OnPropertyChanged(nameof(Replacement));
                }
            }
        }

        public MacroEntry()
        {
            _shortcut = string.Empty;
            _replacement = string.Empty;
        }

        public MacroEntry(string shortcut, string replacement)
        {
            _shortcut = shortcut ?? string.Empty;
            _replacement = replacement ?? string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
