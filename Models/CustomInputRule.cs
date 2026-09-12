using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModernKey.Models
{
    public class CustomInputRule : INotifyPropertyChanged
    {
        private char _key;
        private int _action;

        public char Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayKey));
                }
            }
        }

        public int Action
        {
            get => _action;
            set
            {
                if (_action != value)
                {
                    _action = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActionName));
                }
            }
        }

        public CustomInputRule() { }

        public CustomInputRule(char key, int action)
        {
            _key = key;
            _action = action;
        }

        public string ActionName
        {
            get
            {
                if (Action >= 0 && Action < ActionDescriptions.Length)
                    return ActionDescriptions[Action];
                return "Hành động " + Action;
            }
        }

        public string DisplayKey => Key.ToString();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static readonly string[] ActionDescriptions = new string[]
        {
            "Xoá dấu", // 0
            "Dấu Sắc '", // 1
            "Dấu Huyền `", // 2
            "Dấu Hỏi ?", // 3
            "Dấu Ngã ~", // 4
            "Dấu Nặng .", // 5
            "Dấu mũ chung cho a, e, o thành â, ê, ô", // 6
            "Dấu mũ cho a thành â", // 7
            "Dấu mũ cho e thành ê", // 8
            "Dấu mũ cho o thành ô", // 9
            "Dấu móc cho a, u, o thành ă, ư, ơ", // 10
            "Dấu móc cho uo thành ươ", // 11
            "Dấu móc cho u thành ư", // 12
            "Dấu móc cho o thành ơ", // 13
            "Dấu móc cho a thành ă", // 14
            "Dấu gạch d thành đ", // 15
            "Dấu móc cho a, u, o thành ă, ư, ơ hoặc là chữ ư", // 16
            "Dấu móc cho a, u, o thành ă, ư, ơ hoặc là chữ ư trừ chữ bắt đầu của từ", // 17
            "Thoát bỏ dấu", // 18
            "Chữ ă", // 19
            "Chữ Ă", // 20
            "Chữ â", // 21
            "Chữ Â", // 22
            "Chữ đ", // 23
            "Chữ Đ", // 24
            "Chữ ê", // 25
            "Chữ Ê", // 26
            "Chữ ô", // 27
            "Chữ Ô", // 28
            "Chữ ơ", // 29
            "Chữ Ơ", // 30
            "Chữ ư", // 31
            "Chữ Ư" // 32
        };

        public static readonly string[] PresetNames = new string[]
        {
            "Telex",
            "VNI",
            "Simple Telex",
            "Tư Bình Trần đơn giản"
        };

        public static List<CustomInputRule> GetPreset(int presetIndex)
        {
            var rules = new List<CustomInputRule>();
            switch (presetIndex)
            {
                case 0: // Telex
                    rules.Add(new CustomInputRule('s', 1));
                    rules.Add(new CustomInputRule('f', 2));
                    rules.Add(new CustomInputRule('r', 3));
                    rules.Add(new CustomInputRule('x', 4));
                    rules.Add(new CustomInputRule('j', 5));
                    rules.Add(new CustomInputRule('a', 7));
                    rules.Add(new CustomInputRule('e', 8));
                    rules.Add(new CustomInputRule('o', 9));
                    rules.Add(new CustomInputRule('w', 16));
                    rules.Add(new CustomInputRule('d', 15));
                    rules.Add(new CustomInputRule('z', 0));
                    rules.Add(new CustomInputRule('[', 31));
                    rules.Add(new CustomInputRule(']', 29));
                    rules.Add(new CustomInputRule('{', 32));
                    rules.Add(new CustomInputRule('}', 30));
                    break;
                case 1: // VNI
                    rules.Add(new CustomInputRule('1', 1));
                    rules.Add(new CustomInputRule('2', 2));
                    rules.Add(new CustomInputRule('3', 3));
                    rules.Add(new CustomInputRule('4', 4));
                    rules.Add(new CustomInputRule('5', 5));
                    rules.Add(new CustomInputRule('6', 6));
                    rules.Add(new CustomInputRule('7', 10));
                    rules.Add(new CustomInputRule('8', 10));
                    rules.Add(new CustomInputRule('9', 15));
                    rules.Add(new CustomInputRule('0', 0));
                    break;
                case 2: // Simple Telex
                    rules.Add(new CustomInputRule('s', 1));
                    rules.Add(new CustomInputRule('f', 2));
                    rules.Add(new CustomInputRule('r', 3));
                    rules.Add(new CustomInputRule('x', 4));
                    rules.Add(new CustomInputRule('j', 5));
                    rules.Add(new CustomInputRule('a', 7));
                    rules.Add(new CustomInputRule('e', 8));
                    rules.Add(new CustomInputRule('o', 9));
                    rules.Add(new CustomInputRule('d', 15));
                    rules.Add(new CustomInputRule('z', 0));
                    break;
                case 3: // Tư Bình Trần đơn giản
                    rules.Add(new CustomInputRule('z', 0));
                    rules.Add(new CustomInputRule('d', 15));
                    rules.Add(new CustomInputRule('[', 31)); // Chữ ư
                    rules.Add(new CustomInputRule(']', 29)); // Chữ ơ
                    rules.Add(new CustomInputRule('0', 0));
                    rules.Add(new CustomInputRule('1', 1));
                    rules.Add(new CustomInputRule('2', 2));
                    rules.Add(new CustomInputRule('3', 3));
                    rules.Add(new CustomInputRule('4', 4));
                    rules.Add(new CustomInputRule('5', 5));
                    rules.Add(new CustomInputRule('6', 21)); // Chữ â
                    rules.Add(new CustomInputRule('7', 25)); // Chữ ê
                    rules.Add(new CustomInputRule('8', 27)); // Chữ ô
                    rules.Add(new CustomInputRule('9', 19)); // Chữ ă
                    rules.Add(new CustomInputRule('^', 22)); // Chữ Â
                    rules.Add(new CustomInputRule('&', 26)); // Chữ Ê
                    rules.Add(new CustomInputRule('*', 28)); // Chữ Ô
                    rules.Add(new CustomInputRule('(', 20)); // Chữ Ă
                    rules.Add(new CustomInputRule('{', 32)); // Chữ Ư
                    rules.Add(new CustomInputRule('}', 30)); // Chữ Ơ
                    break;
            }
            return rules;
        }
    }
}
