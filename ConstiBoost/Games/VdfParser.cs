using System;
using System.Collections.Generic;
using System.Text;

namespace ConstiBoost.Games;

/// <summary>Minimal parser for Valve's KeyValues text format (libraryfolders.vdf, appmanifest_*.acf).</summary>
public sealed class VdfNode
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string key) => Values.TryGetValue(key, out var v) ? v : null;

    public VdfNode? Child(string key) => Children.TryGetValue(key, out var c) ? c : null;

    public static VdfNode Parse(string text)
    {
        var tokens = Tokenize(text);
        int index = 0;
        var root = new VdfNode();
        ParseInto(root, tokens, ref index);
        return root;
    }

    private static void ParseInto(VdfNode node, List<string> tokens, ref int index)
    {
        while (index < tokens.Count)
        {
            string token = tokens[index++];
            if (token == "}")
            {
                return;
            }

            if (index >= tokens.Count)
            {
                return;
            }

            string next = tokens[index];
            if (next == "{")
            {
                index++;
                var child = new VdfNode();
                ParseInto(child, tokens, ref index);
                node.Children[token] = child;
            }
            else
            {
                index++;
                node.Values[token] = next;
            }
        }
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '{' || c == '}')
            {
                tokens.Add(c.ToString());
                i++;
            }
            else if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        sb.Append(text[i] switch { 'n' => '\n', 't' => '\t', _ => text[i] });
                    }
                    else
                    {
                        sb.Append(text[i]);
                    }

                    i++;
                }

                i++;
                tokens.Add(sb.ToString());
            }
            else
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}')
                {
                    i++;
                }

                tokens.Add(text[start..i]);
            }
        }

        return tokens;
    }
}
