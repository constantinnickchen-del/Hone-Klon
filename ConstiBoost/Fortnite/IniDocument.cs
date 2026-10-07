using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ConstiBoost.Fortnite;

/// <summary>Line-preserving INI editor: only touches the exact lines that change.</summary>
public sealed class IniDocument
{
    private readonly List<string> _lines;
    private readonly Encoding _encoding;
    private readonly string _newLine;

    private IniDocument(List<string> lines, Encoding encoding, string newLine)
    {
        _lines = lines;
        _encoding = encoding;
        _newLine = newLine;
    }

    /// <summary>Loads the file and remembers its encoding (Unreal writes ANSI/UTF-8 or UTF-16) and line endings.</summary>
    public static IniDocument Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Encoding encoding;
        int skip;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = new UnicodeEncoding(false, true);
            skip = 2;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = new UnicodeEncoding(true, true);
            skip = 2;
        }
        else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = new UTF8Encoding(true);
            skip = 3;
        }
        else
        {
            encoding = new UTF8Encoding(false);
            skip = 0;
        }

        string text = encoding.GetString(bytes, skip, bytes.Length - skip);
        string newLine = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1); // trailing newline
        }

        return new IniDocument(lines, encoding, newLine);
    }

    public void Save(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        string text = string.Join(_newLine, _lines) + _newLine;
        byte[] preamble = _encoding.GetPreamble();
        byte[] body = _encoding.GetBytes(text);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(preamble, 0, preamble.Length);
        stream.Write(body, 0, body.Length);
    }

    /// <summary>Returns the first value of a key in any section.</summary>
    public string? Get(string key)
    {
        foreach (string line in _lines)
        {
            if (TryParse(line, out string k, out string v) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }

        return null;
    }

    public bool Has(string key) => Get(key) != null;

    /// <summary>Updates every existing occurrence of the key. Never adds unknown keys. Returns false if the key does not exist.</summary>
    public bool Set(string key, string value)
    {
        bool found = false;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (TryParse(_lines[i], out string k, out _) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = $"{k}={value}";
                found = true;
            }
        }

        return found;
    }

    private static bool TryParse(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('['))
        {
            return false;
        }

        int eq = trimmed.IndexOf('=');
        if (eq <= 0)
        {
            return false;
        }

        key = trimmed[..eq].Trim();
        value = trimmed[(eq + 1)..].Trim();
        return true;
    }
}
