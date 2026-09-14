using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ModernKey.Config;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ ghi nhận và phân tích thống kê gõ phím Cyberpunk WPM & Heatmap thời gian offline.
    /// </summary>
    public class TypingStatsManager
    {
        private static TypingStatsManager _instance;
        public static TypingStatsManager Instance => _instance ?? (_instance = new TypingStatsManager());

        private readonly object _lock = new object();
        private readonly Queue<long> _recentKeyTimestamps = new Queue<long>();

        public int TodayKeystrokes { get; private set; } = 0;
        public int TodayWords { get; private set; } = 0;
        public int TodayVietnameseWords { get; private set; } = 0;
        public int TodayEnglishWords { get; private set; } = 0;
        public long TotalKeystrokes { get; private set; } = 0;
        public int[] HourlyKeystrokes { get; } = new int[24];

        public int CurrentWpm => GetCurrentWpm();
        public int TotalWords => TodayWords;
        public int ActiveTypingMinutes => Math.Max(1, (int)(TotalKeystrokes / 120));

        public int[] GetHourlyActivity()
        {
            lock (_lock)
            {
                int[] copy = new int[24];
                Array.Copy(HourlyKeystrokes, copy, 24);
                return copy;
            }
        }

        public void ResetStats()
        {
            lock (_lock)
            {
                TodayKeystrokes = 0;
                TodayWords = 0;
                TodayVietnameseWords = 0;
                TodayEnglishWords = 0;
                TotalKeystrokes = 0;
                for (int i = 0; i < 24; i++) HourlyKeystrokes[i] = 0;
                _isDirty = true;
                SaveIfDirty();
            }
        }

        public string CurrentDateStr { get; private set; }

        private bool _isDirty = false;
        private Timer _saveTimer;

        public TypingStatsManager()
        {
            CurrentDateStr = DateTime.Now.ToString("yyyy-MM-dd");
            LoadStats();
            _saveTimer = new Timer(_ => SaveIfDirty(), null, 30000, 30000);
        }

        public void RecordKeystroke(bool isWordBreak = false, bool isVietnamese = true)
        {
            lock (_lock)
            {
                CheckNewDay();
                long nowTick = DateTime.UtcNow.Ticks;
                _recentKeyTimestamps.Enqueue(nowTick);

                // Loại bỏ các phím cũ hơn 60 giây để tính WPM chuẩn
                long oneMinuteAgo = nowTick - TimeSpan.FromMinutes(1).Ticks;
                while (_recentKeyTimestamps.Count > 0 && _recentKeyTimestamps.Peek() < oneMinuteAgo)
                {
                    _recentKeyTimestamps.Dequeue();
                }

                TodayKeystrokes++;
                TotalKeystrokes++;

                int hour = DateTime.Now.Hour;
                if (hour >= 0 && hour < 24)
                {
                    HourlyKeystrokes[hour]++;
                }

                if (isWordBreak)
                {
                    TodayWords++;
                    if (isVietnamese) TodayVietnameseWords++;
                    else TodayEnglishWords++;
                }

                _isDirty = true;
            }
        }

        public int GetCurrentWpm()
        {
            lock (_lock)
            {
                long nowTick = DateTime.UtcNow.Ticks;
                long oneMinuteAgo = nowTick - TimeSpan.FromMinutes(1).Ticks;
                while (_recentKeyTimestamps.Count > 0 && _recentKeyTimestamps.Peek() < oneMinuteAgo)
                {
                    _recentKeyTimestamps.Dequeue();
                }

                // Trung bình 1 từ chuẩn = 5 ký tự
                int cpm = _recentKeyTimestamps.Count;
                return Math.Max(0, cpm / 5);
            }
        }

        private void CheckNewDay()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (CurrentDateStr != today)
            {
                CurrentDateStr = today;
                TodayKeystrokes = 0;
                TodayWords = 0;
                TodayVietnameseWords = 0;
                TodayEnglishWords = 0;
                for (int i = 0; i < 24; i++) HourlyKeystrokes[i] = 0;
                _isDirty = true;
            }
        }

        private string GetStatsFilePath()
        {
            return Path.Combine(SettingsManager.GetConfigDirectory(), "typing_stats.json");
        }

        public void LoadStats()
        {
            lock (_lock)
            {
                try
                {
                    string path = GetStatsFilePath();
                    if (!File.Exists(path)) return;

                    string json = File.ReadAllText(path, Encoding.UTF8);
                    // Parse thủ công tối giản không cần kéo Newtonsoft.Json
                    ParseSimpleJson(json);
                }
                catch { }
            }
        }

        private void ParseSimpleJson(string json)
        {
            try
            {
                var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim().TrimEnd(',');
                    int colon = trimmed.IndexOf(':');
                    if (colon <= 0) continue;

                    string key = trimmed.Substring(0, colon).Trim('"', ' ');
                    string val = trimmed.Substring(colon + 1).Trim('"', ' ');

                    switch (key)
                    {
                        case "Date":
                            if (val != CurrentDateStr)
                            {
                                // Ngày cũ -> không ghi đè số liệu hôm nay
                                return;
                            }
                            break;
                        case "TodayKeystrokes":
                            if (int.TryParse(val, out var tk)) TodayKeystrokes = tk;
                            break;
                        case "TodayWords":
                            if (int.TryParse(val, out var tw)) TodayWords = tw;
                            break;
                        case "TodayVietnameseWords":
                            if (int.TryParse(val, out var tvw)) TodayVietnameseWords = tvw;
                            break;
                        case "TodayEnglishWords":
                            if (int.TryParse(val, out var tew)) TodayEnglishWords = tew;
                            break;
                        case "TotalKeystrokes":
                            if (long.TryParse(val, out var totk)) TotalKeystrokes = totk;
                            break;
                        case "HourlyKeystrokes":
                            string rawArr = val.Trim('[', ']');
                            var parts = rawArr.Split(',');
                            for (int i = 0; i < Math.Min(24, parts.Length); i++)
                            {
                                if (int.TryParse(parts[i].Trim(), out int hVal))
                                    HourlyKeystrokes[i] = hVal;
                            }
                            break;
                    }
                }
            }
            catch { }
        }

        public void SaveIfDirty()
        {
            lock (_lock)
            {
                if (!_isDirty) return;
                _isDirty = false;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string path = GetStatsFilePath();
                    var sb = new StringBuilder();
                    lock (_lock)
                    {
                        sb.AppendLine("{");
                        sb.AppendLine($"  \"Date\": \"{CurrentDateStr}\",");
                        sb.AppendLine($"  \"TodayKeystrokes\": {TodayKeystrokes},");
                        sb.AppendLine($"  \"TodayWords\": {TodayWords},");
                        sb.AppendLine($"  \"TodayVietnameseWords\": {TodayVietnameseWords},");
                        sb.AppendLine($"  \"TodayEnglishWords\": {TodayEnglishWords},");
                        sb.AppendLine($"  \"TotalKeystrokes\": {TotalKeystrokes},");
                        sb.Append("  \"HourlyKeystrokes\": [");
                        for (int i = 0; i < 24; i++)
                        {
                            sb.Append(HourlyKeystrokes[i]);
                            if (i < 23) sb.Append(", ");
                        }
                        sb.AppendLine("]");
                        sb.AppendLine("}");
                    }
                    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            });
        }
    }
}
