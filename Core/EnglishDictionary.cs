using System;
using System.Collections.Generic;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ từ điển tiếng Anh thông dụng (IT, Gaming, Văn phòng, Lập trình) để bảo vệ từ không bị ép dấu tiếng Việt.
    /// </summary>
    public static class EnglishDictionary
    {
        private static readonly HashSet<string> CommonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Từ vựng IT & Lập trình
            // Từ vựng IT & Lập trình (Chỉ giữ từ an toàn, không xung đột với tiếng Việt)
            "post", "posts", "cost", "costs", "host", "hosts", "tests", "best", "fast",
            "last", "past", "cast", "guest", "west", "east", "rest", "dust", "trust", "just",
            "list", "lists", "most", "lost", "must", "risk", "task", "mask", "desk", "disk",
            "scale", "server", "client", "game", "pass", "clear", "link", "free", "like",
            "view", "case", "break", "true", "false", "null", "text", "user", "admin",
            "root", "port", "load", "drop", "make", "take", "find", "send", "read",
            "root", "port", "load", "drop", "send",
            "open", "save", "stop", "play", "push", "pull", "fetch", "merge", "reset", "branch",
            "commit", "status", "stage", "diff", "patch", "help", "info", "type", "date", "time",
            "name", "code", "mode", "file", "path", "size", "data", "item", "page",
            "site", "node", "edge", "call", "wait", "loop", "next", "prev", "back", "home",
            "work", "team", "chat", "mail", "blog", "feed", "card", "form", "icon", "menu",
            "base", "core", "rule", "unit", "byte", "bits", "hash", "auth", "token",
            "commit", "status", "stage", "diff", "patch", "help", "info", "type",
            "code", "mode", "file", "path", "size", "item", "page",
            "site", "node", "edge", "call", "wait", "loop", "next", "prev", "back",
            "team", "chat", "mail", "blog", "feed", "card", "form", "icon", "menu",
            "base", "unit", "byte", "hash", "auth", "token",
            "seed", "peer", "ping", "pong", "sync", "hook", "pipe", "pool", "lock", "sock",
            "bind", "kill", "boot", "init", "exec", "scan", "dump", "sort", "seek", "grep",
            "line", "word", "char", "font", "bold", "fade", "hide", "show", "zoom", "move",
            "font", "bold", "fade", "hide", "show", "zoom", "move",
            "drag", "pick", "fill", "crop", "edit", "copy", "cut", "paste", "undo", "redo",
            "pack", "span", "flex", "grid", "wrap", "auto", "dark", "glow",
            "pack", "span", "flex", "grid", "wrap", "dark", "glow",
            "neon", "cyan", "pink", "gold", "blue", "gray", "thin", "wide", "tall",
            "telex", "simpletelex", "tele", "new", "news", "knew", "chew", "flew", "grew",
            "drew", "screw", "stew", "crew", "views", "review", "reviews", "interview", "interviews",
            "complex", "duplex", "multiplex", "simplex", "index", "vertex", "matrix", "cortex",
            "latex", "regex", "apex", "ajax", "inbox", "dropbox", "firefox", "netflix", "linux", "unix",
            "latex", "regex", "apex", "inbox", "dropbox", "firefox", "netflix", "linux", "unix",
            "pixel", "pixels", "proxy", "fax", "relax", "remix", "prefix", "suffix",
            "fix", "fox", "qwen", "qwerty", "sweet", "swift", "switch", "tweet", "twenty", "twin", "two", "dwarf",
            "fix", "fox", "qwen", "qwerty", "sweet", "swift", "switch", "tweet", "twenty", "twin", "dwarf",

            // Từ vựng tiếng Anh thông dụng hay bị dính dấu oan
            // Từ vựng tiếng Anh an toàn (Không trùng âm tiết hay tiền tố bỏ dấu tự do tiếng Việt)
            "about", "after", "again", "almost", "along", "also", "always", "among", "animal",
            "another", "answer", "around", "became", "become", "before", "behind", "being",
            "below", "between", "black", "board", "change", "check", "close", "color", "come",
            "could", "course", "cover", "cross", "direct", "draw", "early", "earth", "enough",
            "every", "example", "family", "fast", "feel", "feet", "fire", "first", "fish",
            "food", "form", "found", "four", "friend", "from", "front", "give", "given",
            "good", "great", "green", "ground", "group", "grow", "half", "hand", "hard",
            "have", "head", "hear", "heard", "help", "here", "high", "hold", "house",
            "keep", "kind", "know", "land", "large", "last", "later", "learn", "leave",
            "left", "life", "light", "line", "live", "look", "made", "make", "many",
            "mean", "might", "mile", "miss", "more", "most", "move", "much", "must",
            "name", "near", "need", "never", "next", "night", "number", "often", "once",
            "only", "open", "order", "other", "part", "people", "picture", "place", "plant",
            "play", "point", "press", "read", "real", "right", "river", "room", "round",
            "same", "school", "second", "seem", "sentence", "side", "small", "some", "sound",
            "spell", "stand", "start", "state", "still", "story", "study", "such", "take",
            "talk", "tell", "than", "that", "them", "then", "there", "these", "they",
            "thing", "think", "this", "those", "thought", "three", "through", "time",
            "together", "took", "tree", "turn", "under", "until", "walk", "want", "watch",
            "water", "well", "went", "were", "what", "when", "where", "which", "while",
            "white", "whole", "will", "with", "without", "word", "work", "world", "would",
            "write", "year", "young",
            "below", "between", "black", "board", "could", "course", "cross", "direct",
            "early", "earth", "enough", "every", "example", "family", "feel", "first",
            "found", "friend", "front", "ground", "group", "large", "later", "learn",
            "leave", "might", "never", "number", "often", "order", "other", "people",
            "picture", "place", "plant", "point", "press", "problem", "public", "question",
            "right", "river", "school", "second", "sentence", "small", "sound", "stand",
            "start", "state", "still", "story", "study", "system", "thing", "think",
            "thought", "through", "together", "under", "until", "young"
        };

        private static readonly HashSet<string> CustomWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _customWordsLock = new object();

        public static int CustomWordCount
        {
            get
            {
                lock (_customWordsLock)
                {
                    return CustomWords.Count;
                }
            }
        }

        public static void ClearCustomWords()
        {
            lock (_customWordsLock)
            {
                CustomWords.Clear();
            }
        }

        public static void AddCustomWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;
            lock (_customWordsLock)
            {
                CustomWords.Add(word.Trim());
            }
        }

        public static void SetCustomWords(IEnumerable<string> words)
        {
            lock (_customWordsLock)
            {
                CustomWords.Clear();
                if (words != null)
                {
                    foreach (var w in words)
                    {
                        if (!string.IsNullOrWhiteSpace(w))
                        {
                            CustomWords.Add(w.Trim());
                        }
                    }
                }
            }
        }

        public static bool IsCommonEnglishWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            if (CommonWords.Contains(word)) return true;
            lock (_customWordsLock)
            {
                return CustomWords.Contains(word);
            }
        }
    }
}

