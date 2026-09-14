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
            "post", "posts", "cost", "costs", "host", "hosts", "test", "tests", "best", "fast",
            "last", "past", "cast", "guest", "west", "east", "rest", "dust", "trust", "just",
            "list", "lists", "most", "lost", "must", "risk", "task", "mask", "desk", "disk",
            "scale", "server", "client", "game", "pass", "clear", "link", "free", "like",
            "view", "case", "break", "true", "false", "null", "text", "user", "admin",
            "root", "port", "load", "drop", "make", "take", "find", "send", "read",
            "open", "save", "stop", "play", "push", "pull", "fetch", "merge", "reset", "branch",
            "commit", "status", "stage", "diff", "patch", "help", "info", "type", "date", "time",
            "name", "code", "mode", "file", "path", "size", "data", "item", "page",
            "site", "node", "edge", "call", "wait", "loop", "next", "prev", "back", "home",
            "work", "team", "chat", "mail", "blog", "feed", "card", "form", "icon", "menu",
            "base", "core", "rule", "unit", "byte", "bits", "hash", "auth", "token",
            "seed", "peer", "ping", "pong", "sync", "hook", "pipe", "pool", "lock", "sock",
            "bind", "kill", "boot", "init", "exec", "scan", "dump", "sort", "seek", "grep",
            "line", "word", "char", "font", "bold", "fade", "hide", "show", "zoom", "move",
            "drag", "pick", "fill", "crop", "edit", "copy", "cut", "paste", "undo", "redo",
            "pack", "span", "flex", "grid", "wrap", "auto", "dark", "glow",
            "neon", "cyan", "pink", "gold", "blue", "gray", "thin", "wide", "tall",

            // Từ vựng tiếng Anh thông dụng hay bị dính dấu oan
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
            "write", "year", "young"
        };

        public static bool IsCommonEnglishWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            return CommonWords.Contains(word);
        }
    }
}
