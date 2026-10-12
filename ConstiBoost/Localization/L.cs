using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConstiBoost.Localization;

/// <summary>
/// App languages. The English text is the key: <c>L.T("Apply")</c> returns the German text when German is active,
/// otherwise (or when no translation exists) the English text itself. Formats use <c>L.F("{0} FPS", x)</c>.
/// The language is chosen at startup (Settings → Language; changing it restarts ConstiBoost).
/// </summary>
public static class L
{
    private static readonly Dictionary<string, string> German = new(StringComparer.Ordinal);

    public static string Language { get; private set; } = "en";

    public static bool IsGerman => Language == "de";

    /// <param name="setting">"de", "en" or "auto" (Windows display language).</param>
    public static void Init(string? setting)
    {
        Language = setting switch
        {
            "de" => "de",
            "en" => "en",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de" : "en",
        };

        German.Clear();
        if (IsGerman)
        {
            foreach (var register in Dictionaries.All)
            {
                register(Add);
            }
        }
    }

    private static void Add(string english, string german)
    {
        if (!string.IsNullOrEmpty(english) && !string.IsNullOrEmpty(german))
        {
            German[english] = german;
        }
    }

    /// <summary>Translated text (or the English text when there is no translation).</summary>
    public static string T(string? english)
    {
        if (string.IsNullOrEmpty(english) || !IsGerman)
        {
            return english ?? string.Empty;
        }

        if (German.TryGetValue(english, out string? german))
        {
            return german;
        }

        // Keep leading emoji / symbols ("🎯 FOCUS") translatable when only the words are in the dictionary.
        string trimmed = english.Trim();
        return trimmed != english && German.TryGetValue(trimmed, out german) ? english.Replace(trimmed, german) : english;
    }

    /// <summary>Translated format string filled with the arguments.</summary>
    public static string F(string english, params object?[] args)
    {
        string format = T(english);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, english, args);
        }
    }

    public static bool HasTranslation(string english) => German.ContainsKey(english);
}
