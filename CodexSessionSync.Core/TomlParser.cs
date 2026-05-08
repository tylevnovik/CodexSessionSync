using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodexSessionSync.Core;

public class TomlParser
{
    public static Dictionary<string, object?> Parse(string text)
    {
        var root = new Dictionary<string, object?>();
        Dictionary<string, object?> currentTable = root;

        foreach (var rawLine in text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = StripComment(rawLine).Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string tableName;
            if (line.StartsWith("[[") && line.EndsWith("]]"))
                tableName = line[2..^2].Trim();
            else if (line.StartsWith('[') && line.EndsWith(']'))
                tableName = line[1..^1].Trim();
            else
                tableName = string.Empty;

            if (!string.IsNullOrEmpty(tableName))
            {
                currentTable = root;
                foreach (var part in SplitDottedName(tableName))
                {
                    if (!currentTable.TryGetValue(part, out var child) || child is not Dictionary<string, object?> dict)
                    {
                        dict = new Dictionary<string, object?>();
                        currentTable[part] = dict;
                    }
                    currentTable = dict;
                }
                continue;
            }

            if (!line.Contains('=')) continue;

            var eqIdx = line.IndexOf('=');
            var rawKey = line[..eqIdx];
            var rawValue = line[(eqIdx + 1)..];

            var keyParts = SplitDottedName(rawKey);
            if (keyParts.Count == 0) continue;

            var target = currentTable;
            for (int i = 0; i < keyParts.Count - 1; i++)
            {
                if (!target.TryGetValue(keyParts[i], out var child) || child is not Dictionary<string, object?> dict)
                {
                    dict = new Dictionary<string, object?>();
                    target[keyParts[i]] = dict;
                }
                target = dict;
            }

            target[keyParts[^1]] = ParseScalar(rawValue);
        }

        return root;
    }

    private static string StripComment(string line)
    {
        bool inSingle = false, inDouble = false, escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inDouble)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inDouble = false;
                continue;
            }
            if (inSingle)
            {
                if (c == '\'') inSingle = false;
                continue;
            }
            if (c == '"') inDouble = true;
            else if (c == '\'') inSingle = true;
            else if (c == '#') return line[..i];
        }
        return line;
    }

    private static List<string> SplitDottedName(string raw)
    {
        var parts = new List<string>();
        var current = new List<char>();
        char? quote = null;
        bool escaped = false;

        foreach (var c in raw.Trim())
        {
            if (quote == '"')
            {
                if (escaped) { current.Add(c); escaped = false; }
                else if (c == '\\') escaped = true;
                else if (c == '"') quote = null;
                else current.Add(c);
                continue;
            }
            if (quote == '\'')
            {
                if (c == '\'') quote = null;
                else current.Add(c);
                continue;
            }
            if (c is '"' or '\'') quote = c;
            else if (c == '.') { parts.Add(new string(current.ToArray()).Trim()); current.Clear(); }
            else current.Add(c);
        }

        parts.Add(new string(current.ToArray()).Trim());
        return parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
    }

    private static object? ParseScalar(string raw)
    {
        var v = raw.Trim();
        if (v.Length >= 2 && v[0] == '\'' && v[^1] == '\'') return v[1..^1];
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') return v[1..^1];
        if (v == "true") return true;
        if (v == "false") return false;
        return v;
    }
}
