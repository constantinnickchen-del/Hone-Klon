using System;

namespace ConstiBoost.Localization;

/// <summary>Shared words: page names, buttons, statuses. Simple, everyday German ("du").</summary>
internal static class DeCommon
{
    public static void Register(Action<string, string> add)
    {
        // Pages
        add("Dashboard", "Übersicht");
        add("Optimizer", "Optimieren");
        add("FPS Boost", "FPS-Boost");
        add("Boost Lab", "Boost-Labor");
        add("Games", "Spiele");
        add("Thermal", "Temperatur");
        add("Cleaner", "Aufräumen");
        add("Startup", "Autostart");
        add("Hardware", "Hardware");
        add("Backups", "Sicherungen");
        add("Settings", "Einstellungen");

        // Common buttons / words
        add("APPLY", "ANWENDEN");
        add("Apply", "Anwenden");
        add("↩ UNDO", "↩ RÜCKGÄNGIG");
        add("UNDO", "RÜCKGÄNGIG");
        add("Undo", "Rückgängig");
        add("CONFIRM", "BESTÄTIGEN");
        add("Cancel", "Abbrechen");
        add("OK", "OK");
        add("Close", "Schließen");
        add("Restore", "Wiederherstellen");
        add("RESTORE", "WIEDERHERSTELLEN");
        add("Copy address", "Adresse kopieren");
        add("Optimal", "Optimal");
        add("Unavailable", "Nicht verfügbar");
        add("Can be improved", "Kann besser werden");
        add("Your choice", "Deine Wahl");
        add("Sensor unavailable", "Sensor nicht verfügbar");
        add("HIGH", "HOCH");
        add("MEDIUM", "MITTEL");
        add("LOW", "NIEDRIG");
        add("INFO", "INFO");
        add("FPS IMPACT", "FPS-WIRKUNG");
        add("Done", "Fertig");
        add("This optimization is unavailable on your system.", "Diese Optimierung ist auf deinem PC nicht verfügbar.");
        add("This optimization couldn't be applied.", "Diese Optimierung konnte nicht angewendet werden.");
        add("Already optimal.", "Schon optimal.");
        add("ON", "AN");
        add("OFF", "AUS");
        add("On", "An");
        add("Off", "Aus");

        // Settings: language
        add("Language", "Sprache");
        add("Automatic (Windows language)", "Automatisch (Windows-Sprache)");
        add("The new language is used after a restart of ConstiBoost.", "Die neue Sprache gilt nach einem Neustart von ConstiBoost.");
        add("RESTART NOW", "JETZT NEU STARTEN");
    }
}
