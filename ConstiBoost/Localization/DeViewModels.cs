using System;

namespace ConstiBoost.Localization;

/// <summary>View models (Dashboard, Optimizer, Boost Lab, FPS Boost, Cleaner, Startup, Hardware, Backups, Settings, Welcome) and App.</summary>
internal static class DeViewModels
{
    public static void Register(Action<string, string> add)
    {
        // ---------- App ----------
        add("ConstiBoost is already running.", "ConstiBoost läuft schon.");
        add("Gaming Mode is still active from your last session. Stop it on the Dashboard to restore your settings.",
            "Der Gaming-Modus ist noch vom letzten Mal an. Stoppe ihn in der Übersicht, dann kommen deine Einstellungen zurück.");
        add("Something went wrong, but ConstiBoost kept running. Details were written to the log.",
            "Etwas ist schiefgelaufen, aber ConstiBoost läuft weiter. Details stehen im Protokoll.");

        // ---------- Main window ----------
        add("Administrator", "Administrator");
        add("Standard user · UAC on demand", "Normaler Benutzer · Admin-Abfrage bei Bedarf");

        // ---------- Dashboard ----------
        add("{0} cores · {1} threads", "{0} Kerne · {1} Threads");
        add("Dedicated graphics", "Eigene Grafikkarte");
        add("Integrated graphics", "Eingebaute Grafik");
        add("Graphics", "Grafik");
        add("{0} MT/s · {1} module(s)", "{0} MT/s · {1} Riegel");
        add("Good", "Gut");
        add("Could be improved", "Geht noch besser");
        add("Needs attention", "Braucht Aufmerksamkeit");
        add("Captures ON", "Aufnahmen AN");
        add("Captures OFF", "Aufnahmen AUS");
        add("{0} enabled", "{0} aktiv");
        add("CPU/GPU values read from {0}.", "CPU/GPU-Werte gelesen von {0}.");

        // ---------- Optimizer ----------
        add("Gaming Readiness: {0}/100 → {1}/100", "Gaming-Bereitschaft: {0}/100 → {1}/100");
        add("ON (captures enabled)", "AN (Aufnahmen aktiv)");
        add("Analyzing your system…", "Dein PC wird analysiert…");
        add("Your system is already well configured. Nothing important to change.", "Dein PC ist schon gut eingestellt. Es gibt nichts Wichtiges zu ändern.");
        add("Optimization found: {0} item(s). Recommended items are pre-selected – review them below.",
            "Gefunden: {0} Punkt(e) zum Verbessern. Die empfohlenen sind schon angehakt – schau sie dir unten an.");
        add("No optimizations needed – your PC is already in good shape.", "Nichts zu tun – dein PC ist schon in gutem Zustand.");
        add("The analysis couldn't be completed. Details were written to the log.", "Die Analyse hat nicht geklappt. Details stehen im Protokoll.");
        add("  [administrator permission]", "  [braucht Admin-Rechte]");
        add("  [deletes temp/cache files]", "  [löscht Temp-/Cache-Dateien]");
        add("A backup of every changed setting is created first.", "Vorher wird von jeder geänderten Einstellung eine Sicherung gemacht.");
        add("Apply optimizations?", "Optimierungen anwenden?");
        add("ConstiBoost will make the following changes:", "ConstiBoost macht diese Änderungen:");
        add("APPLY OPTIMIZATIONS", "OPTIMIERUNGEN ANWENDEN");
        add("Creating Windows restore point (UAC)…", "Windows-Wiederherstellungspunkt wird erstellt (Admin-Abfrage)…");
        add("Restore point not created", "Wiederherstellungspunkt nicht erstellt");
        add("Continue with the ConstiBoost backup only?", "Nur mit der ConstiBoost-Sicherung weitermachen?");
        add("CONTINUE", "WEITER");
        add("Creating backup…", "Sicherung wird erstellt…");
        add("Applying: {0}…", "Wird angewendet: {0}…");
        add("Re-checking Gaming Readiness…", "Gaming-Bereitschaft wird neu geprüft…");
        add("{0} of {1} optimization(s) applied. Backup: {2}", "{0} von {1} Optimierung(en) angewendet. Sicherung: {2}");
        add("✕ This optimization couldn't be applied. Details were written to the log.", "✕ Diese Optimierung konnte nicht angewendet werden. Details stehen im Protokoll.");
        add("Microsoft Store / Xbox app games", "Spiele aus dem Microsoft Store / der Xbox-App");
        add("Xbox networking for Store games", "Xbox-Netzwerk für Store-Spiele");
        add("Xbox Live sign-in", "Xbox-Live-Anmeldung");
        add("Xbox cloud saves", "Xbox-Speicherstände in der Cloud");
        add("Xbox Live multiplayer networking", "Xbox-Live-Netzwerk für Mehrspieler");
        add("Xbox accessories (controllers)", "Xbox-Zubehör (Controller)");
        add("Running", "Läuft");
        add("Stopped", "Gestoppt");
        add("Paused", "Pausiert");
        add("StartPending", "Startet");
        add("StopPending", "Stoppt");
        add("Not installed", "Nicht installiert");
        add("Windows settings couldn't be opened: {0}", "Windows-Einstellungen konnten nicht geöffnet werden: {0}");
        add("Optimize my PC", "Meinen PC optimieren");

        // ---------- Boost Lab ----------
        add("Analyzing what limits your FPS…", "Es wird geprüft, was deine FPS bremst…");
        add("No major bottlenecks found – your PC is configured well for its hardware.", "Keine großen Engpässe gefunden – dein PC ist für seine Hardware gut eingestellt.");
        add("{0} thing(s) found that can hold your FPS back.", "{0} Sache(n) gefunden, die deine FPS bremsen können.");
        add("Reading hardware…", "Hardware wird gelesen…");
        add("Checking memory, drivers, storage and display…", "Arbeitsspeicher, Treiber, Speicher und Bildschirm werden geprüft…");
        add("Memory slots: {0} · installed modules: {1}", "RAM-Steckplätze: {0} · eingebaute Riegel: {1}");
        add(" · free: {0}", " · frei: {0}");
        add("unknown", "unbekannt");
        add("Checking Windows settings…", "Windows-Einstellungen werden geprüft…");
        add("A backup of every changed setting is created first (Backups → RESTORE).", "Vorher wird von jeder geänderten Einstellung eine Sicherung gemacht (Sicherungen → WIEDERHERSTELLEN).");
        add("Apply the Low-End Boost Pack?", "Low-End-Boost-Paket anwenden?");
        add("These safe Windows changes will be made:", "Diese sicheren Windows-Änderungen werden gemacht:");
        add("APPLY BOOST PACK", "BOOST-PAKET ANWENDEN");
        add("Low-End Boost Pack applied.", "Low-End-Boost-Paket angewendet.");
        add("Low-End Boost Pack", "Low-End-Boost-Paket");

        // ---------- FPS Boost ----------
        add("Checking your system…", "Dein PC wird geprüft…");
        add("{0} of {1} optimal · {2} can be applied · {3} hint(s) to check · {4} not available on this PC",
            "{0} von {1} optimal · {2} können angewendet werden · {3} Hinweis(e) zum Prüfen · {4} auf diesem PC nicht verfügbar");
        add("Everything that can be applied automatically is already optimal.", "Alles, was automatisch geht, ist schon optimal.");
        add("Every change gets a backup and can be undone per feature (UNDO) or in Backups.",
            "Jede Änderung wird gesichert und kann einzeln (RÜCKGÄNGIG) oder unter Sicherungen zurückgenommen werden.");
        add("Not included (your choice, apply them individually): gaming resolution, refresh rate, update sharing.",
            "Nicht dabei (deine Wahl, einzeln anwenden): Spiel-Auflösung, Bildwiederholrate, Update-Freigabe.");
        add("Apply {0} FPS boosts?", "{0} FPS-Boosts anwenden?");
        add("ConstiBoost will change these settings:", "ConstiBoost ändert diese Einstellungen:");
        add("APPLY ALL", "ALLES ANWENDEN");
        add("⚡ {0} FPS boosts applied.", "⚡ {0} FPS-Boosts angewendet.");
        add("{0} applied, {1} not: ", "{0} angewendet, {1} nicht: ");
        add("Game Focus", "Spiel-Fokus");
        add("Network & Background", "Netzwerk & Hintergrund");
        add("Drivers & Hardware", "Treiber & Hardware");
        add("Power & CPU", "Energie & CPU");
        add("Graphics & Display", "Grafik & Bildschirm");

        // ---------- Cleaner ----------
        add("Scan to see how much space can be freed.", "Starte den Scan, um zu sehen, wie viel Platz frei werden kann.");
        add("This will remove approximately {0}.", "Das entfernt etwa {0}.");
        add("Scan first to see the size.", "Erst scannen, dann siehst du die Größe.");
        add("Scanning…", "Wird gescannt…");
        add("Some folders need administrator permission (UAC) – sizes of protected files may be incomplete.",
            "Manche Ordner brauchen Admin-Rechte – die Größe geschützter Dateien ist evtl. nicht vollständig.");
        add("Browser running ({0}): close it before cleaning the browser cache, otherwise files in use are skipped.",
            "Browser ist offen ({0}): Schließe ihn vor dem Leeren des Browser-Caches, sonst werden benutzte Dateien übersprungen.");
        add("Scan finished at {0:HH:mm}.", "Scan fertig um {0:HH:mm}.");
        add("Never touched: Documents, Pictures, Desktop, Downloads, game saves, cookies and passwords.",
            "Wird nie angefasst: Dokumente, Bilder, Desktop, Downloads, Spielstände, Cookies und Passwörter.");
        add("Clean now?", "Jetzt aufräumen?");
        add("CLEAN", "AUFRÄUMEN");
        add("Cleaning {0}…", "{0} wird aufgeräumt…");
        add("✓ {0}: {1} removed ({2} files, {3} skipped/in use)", "✓ {0}: {1} entfernt ({2} Dateien, {3} übersprungen/in Benutzung)");
        add("Cleaning protected folders (administrator permission)…", "Geschützte Ordner werden aufgeräumt (Admin-Rechte)…");
        add("✓ Protected folders: {0} removed ({1} files, {2} skipped)", "✓ Geschützte Ordner: {0} entfernt ({1} Dateien, {2} übersprungen)");
        add("✕ Protected folders skipped: ", "✕ Geschützte Ordner übersprungen: ");
        add("Cleaning finished.", "Aufräumen fertig.");
        add("✕ Cleaning couldn't be completed. Details were written to the log.", "✕ Aufräumen hat nicht ganz geklappt. Details stehen im Protokoll.");

        // ---------- Startup ----------
        add("{0} of {1} startup apps enabled · {2} protected", "{0} von {1} Autostart-Apps aktiv · {2} geschützt");
        add("{0} is protected: {1}", "{0} ist geschützt: {1}");
        add("Startup apps", "Autostart-Apps");

        // ---------- Hardware ----------
        add("Temperature unavailable", "Temperatur nicht verfügbar");
        add("Unavailable – no supported FPS source. ConstiBoost does not inject into games (anti-cheat safe) and never estimates FPS.",
            "Nicht verfügbar – keine passende FPS-Quelle. ConstiBoost greift nicht in Spiele ein (sicher für Anti-Cheat) und schätzt FPS nie.");
        add("Device", "Gerät");
        add("Cores / Threads", "Kerne / Threads");
        add("Max clock", "Max. Takt");
        add("GPU (integrated)", "GPU (eingebaut)");
        add("  Driver", "  Treiber");
        add("  Display", "  Bildschirm");
        add("RAM speed", "RAM-Tempo");
        add("{0} MT/s ({1} module(s){2})", "{0} MT/s ({1} Riegel{2})");
        add(" – single channel", " – Single-Channel (nur ein Kanal)");
        add("Power", "Strom");
        add("Battery {0} % · {1}", "Akku {0} % · {1}");
        add("plugged in", "am Ladekabel");
        add("on battery", "im Akkubetrieb");
        add("Desktop / no battery", "Desktop / kein Akku");
        add("{0} of {1}", "{0} von {1}");
        add("No readable sensor. CPU/GPU die temperatures need vendor drivers, which ConstiBoost does not install.",
            "Kein lesbarer Sensor. Für CPU/GPU-Chip-Temperaturen braucht man Hersteller-Treiber, die ConstiBoost nicht installiert.");
        add("ACPI thermal zone {0} – a firmware sensor, not necessarily the CPU die.", "ACPI-Temperaturzone {0} – ein Sensor der Firmware, nicht unbedingt der CPU-Chip.");

        // ---------- Processes ----------
        add("{0} processes · updates every 2 s while this tab is open", "{0} Prozesse · wird alle 2 s aktualisiert, solange dieser Tab offen ist");
        add("Protected process", "Geschützter Prozess");
        add("{0} is a critical Windows or security process. ConstiBoost never closes it.", "{0} ist ein wichtiger Windows- oder Sicherheits-Prozess. ConstiBoost schließt ihn nie.");
        add("{0} is an important app (e.g. explorer.exe runs the taskbar and desktop). Closing it can make the taskbar disappear until it restarts. Continue?",
            "{0} ist eine wichtige App (z. B. steuert explorer.exe Taskleiste und Desktop). Wenn du sie schließt, kann die Taskleiste verschwinden, bis sie neu startet. Weiter?");
        add("Close {0} (PID {1})? Unsaved work in this app may be lost.", "{0} (PID {1}) schließen? Nicht gespeicherte Arbeit in dieser App kann verloren gehen.");
        add("Close process?", "Prozess schließen?");
        add("CLOSE PROCESS", "PROZESS SCHLIESSEN");

        // ---------- Network ----------
        add("Test latency under load?", "Ping unter Last testen?");
        add("ConstiBoost pings 1.1.1.1, then downloads up to 50 MB of random test data from {0} while pinging again. This shows whether your ping spikes when the connection is busy (bufferbloat). No personal data is sent.",
            "ConstiBoost pingt 1.1.1.1 und lädt dann bis zu 50 MB zufällige Testdaten von {0}, während es weiter pingt. So siehst du, ob dein Ping hochgeht, wenn die Leitung voll ist (Bufferbloat). Es werden keine persönlichen Daten gesendet.");
        add("START TEST", "TEST STARTEN");
        add("Testing… (about 10 seconds)", "Wird getestet… (etwa 10 Sekunden)");
        add("Idle ping {0} ms · under load {1} ms. ", "Ping in Ruhe {0} ms · unter Last {1} ms. ");
        add("No active connection", "Keine aktive Verbindung");
        add("{0} · Link {1} · IP {2}", "{0} · Verbindung {1} · IP {2}");
        add("Current DNS", "Aktueller DNS");
        add("Run speed test?", "Speedtest starten?");
        add("The test downloads ~25 MB and uploads ~8 MB of random test data from/to {0} (Cloudflare). No personal or system data is sent. Mobile data may be used.",
            "Der Test lädt ca. 25 MB zufällige Testdaten von {0} (Cloudflare) herunter und ca. 8 MB hoch. Es werden keine persönlichen oder System-Daten gesendet. Mobile Daten können verbraucht werden.");
        add("Testing…", "Wird getestet…");
        add("Measured at {0:HH:mm}. Results vary with Wi-Fi signal and network load.", "Gemessen um {0:HH:mm}. Das Ergebnis hängt vom WLAN-Signal und der Netz-Auslastung ab.");

        // ---------- Benchmark ----------
        add("The benchmark couldn't be completed.", "Der Benchmark hat nicht geklappt.");
        add("Performance Score", "Leistungs-Punktzahl");
        add("CPU single-thread (Mops/s)", "CPU ein Kern (Mops/s)");
        add("CPU multi-thread (Mops/s)", "CPU alle Kerne (Mops/s)");
        add("Memory bandwidth (GB/s)", "RAM-Tempo (GB/s)");
        add("Background CPU load (%)", "CPU-Last im Hintergrund (%)");
        add("Scheduling jitter p90 (ms)", "Zeit-Schwankung p90 (ms)");

        // ---------- Backups ----------
        add("{0} saved value(s)", "{0} gespeicherte(r) Wert(e)");
        add("Restored {0:dd.MM.yyyy HH:mm}", "Wiederhergestellt {0:dd.MM.yyyy HH:mm}");
        add("Stop Gaming Mode first", "Stoppe zuerst den Gaming-Modus");
        add("Gaming Mode has its own temporary changes. Stop it before restoring a backup.", "Der Gaming-Modus hat eigene, vorübergehende Änderungen. Stoppe ihn, bevor du eine Sicherung wiederherstellst.");
        add("Restore backup?", "Sicherung wiederherstellen?");
        add("All values saved in \"{0}\" ({1}) will be written back:", "Alle Werte aus \"{0}\" ({1}) werden zurückgeschrieben:");
        add("Backup restored ({0} value(s)).", "Sicherung wiederhergestellt ({0} Wert(e)).");
        add("Restore partially failed", "Wiederherstellen hat teilweise nicht geklappt");
        add("{0} restored, {1} failed.", "{0} wiederhergestellt, {1} fehlgeschlagen.");
        add("Delete backup?", "Sicherung löschen?");
        add("\"{0}\" ({1}) will be deleted. You can no longer restore these values.", "\"{0}\" ({1}) wird gelöscht. Danach kannst du diese Werte nicht mehr wiederherstellen.");
        add("DELETE", "LÖSCHEN");
        add("Windows restore point created.", "Windows-Wiederherstellungspunkt erstellt.");

        // ---------- Settings ----------
        add("Hotkey change applies after restarting ConstiBoost.", "Die Tastenkürzel-Änderung gilt nach einem Neustart von ConstiBoost.");
        add("Install ConstiBoost {0}?", "ConstiBoost {0} installieren?");
        add("The installer is downloaded from GitHub Releases and started. ConstiBoost closes and restarts after the update. Your settings, backups and logs are kept.",
            "Der Installer wird von GitHub Releases geladen und gestartet. ConstiBoost schließt sich und startet nach dem Update neu. Deine Einstellungen, Sicherungen und Protokolle bleiben erhalten.");
        add("UPDATE", "AKTUALISIEREN");
        add("Last run: {0:dd.MM.yyyy HH:mm}", "Zuletzt: {0:dd.MM.yyyy HH:mm}");
        add("Never run", "Noch nie ausgeführt");
        add("ConstiBoost {0} PRO · local · no accounts · no telemetry", "ConstiBoost {0} PRO · lokal · kein Konto · keine Datensammlung");

        // ---------- Welcome ----------
        add("Looking for optimizations…", "Suche nach Verbesserungen…");
        add("No important changes needed – your PC is already well configured.", "Keine wichtigen Änderungen nötig – dein PC ist schon gut eingestellt.");

        // ---------- Status words (converter) ----------
        add("SUCCESS", "ERFOLG");
        add("FAILED", "FEHLER");
    }
}
