using System;

namespace ConstiBoost.Localization;

/// <summary>German texts for the main window, Dashboard, Optimizer, Hardware, Settings, Backups, Cleaner, Startup,
/// Welcome, confirm dialog and the shared templates.</summary>
internal static class DeViews2
{
    public static void Register(Action<string, string> add)
    {
        // Main window
        add("Performance Suite", "Leistungs-Paket");
        add("Gaming Mode ACTIVE", "Gaming-Modus AKTIV");
        add("v2.0 PRO · 100 % local", "v2.0 PRO · 100 % lokal");
        add("Minimize", "Minimieren");
        add("Maximize", "Maximieren");
        add("ConstiBoost keeps running", "ConstiBoost läuft weiter");
        add("ConstiBoost is still running in the notification area. Right-click the tray icon → Exit to close it completely.",
            "ConstiBoost läuft unten rechts in der Taskleiste weiter. Zum ganz Beenden: Rechtsklick auf das Symbol → Beenden.");
        add("Game Focus is still active", "Spiel-Fokus ist noch an");
        add("Gaming Mode is still active", "Gaming-Modus ist noch an");
        add("Stop it and restore your previous settings before closing? If you keep it, ConstiBoost will offer to stop it the next time it starts.",
            "Vor dem Schließen stoppen und deine alten Einstellungen zurückholen? Wenn du ihn anlässt, fragt ConstiBoost beim nächsten Start, ob er gestoppt werden soll.");
        add("STOP AND CLOSE", "STOPPEN UND SCHLIESSEN");

        // Dashboard
        add("RESCAN", "NEU PRÜFEN");
        add("LOW-END GAMING PROFILE recommended", "LOW-END-GAMING-PROFIL empfohlen");
        add("GAMING READINESS", "GAMING-BEREITSCHAFT");
        add("ONE-CLICK OPTIMIZE", "MIT EINEM KLICK OPTIMIEREN");
        add("Analyze, review and apply safe optimizations – with a backup of every change.",
            "Sichere Optimierungen prüfen, ansehen und anwenden – jede Änderung wird vorher gesichert.");
        add("⚡ OPTIMIZE MY PC", "⚡ MEINEN PC OPTIMIEREN");
        add("📊 FPS Overlay", "📊 FPS-Overlay");
        add("📸 PC Report", "📸 PC-Bericht");
        add("🧪 Boost Lab", "🧪 Boost-Labor");
        add("🎮 Games", "🎮 Spiele");
        add("🌡️ Thermal", "🌡️ Temperatur");
        add("WINDOWS", "WINDOWS");
        add("TEMPERATURES", "TEMPERATUREN");
        add("ACPI thermal zone", "ACPI-Temperaturzone");
        add("CPU/GPU temperatures are read from HWiNFO or LibreHardwareMonitor when they run in the background. ConstiBoost installs no drivers and never estimates values. The ACPI zone is a Windows firmware sensor (indirect).",
            "CPU-/GPU-Temperaturen kommen von HWiNFO oder LibreHardwareMonitor, wenn eins davon im Hintergrund läuft. ConstiBoost installiert keine Treiber und rät keine Werte. Die ACPI-Zone ist ein Sensor der Mainboard-Firmware (nur ungefähr).");
        add("GAMING FEATURES", "GAMING-FUNKTIONEN");
        add("Game Mode", "Spielmodus");
        add("HW GPU Scheduling", "HW-GPU-Planung");
        add("Startup apps", "Autostart-Apps");

        // Optimizer
        add("SELECT RECOMMENDED", "EMPFOHLENE AUSWÄHLEN");
        add("ANALYZE AGAIN", "NEU ANALYSIEREN");
        add("APPLY OPTIMIZATIONS", "OPTIMIERUNGEN ANWENDEN");
        add("Windows Optimizer", "Windows optimieren");
        add("Every change shows its current state, the recommendation and the reason. A backup is created before anything is changed.",
            "Bei jeder Änderung siehst du: wie es jetzt ist, was wir empfehlen und warum. Vor jeder Änderung wird eine Sicherung gemacht.");
        add("Optimizations", "Optimierungen");
        add("Windows gaming features", "Gaming-Funktionen von Windows");
        add("What: gives the active game priority and blocks driver installs / restart prompts during play.",
            "Was: Das laufende Spiel bekommt Vorrang. Beim Spielen gibt es keine Treiber-Installationen und keine Neustart-Hinweise.");
        add("Downside: background apps (e.g. a stream encoder) may get less CPU time.",
            "Nachteil: Hintergrund-Apps (z. B. ein Stream-Programm) bekommen evtl. weniger CPU-Zeit.");
        add("What: overlay (Win+G) with widgets, captures and performance stats. Captures/background recording cost performance; the overlay itself only runs when opened.",
            "Was: Overlay (Win+G) mit Widgets, Aufnahmen und Leistungswerten. Aufnahmen und Hintergrund-Aufnahme kosten Leistung. Das Overlay selbst läuft nur, wenn du es öffnest.");
        add("Controller button opens Game Bar: {0}", "Controller-Taste öffnet Game Bar: {0}");
        add("Open Game Bar settings", "Game-Bar-Einstellungen öffnen");
        add("HW-accelerated GPU scheduling", "Hardwarebeschleunigte GPU-Planung");
        add("What: the GPU schedules its own work queue. Can lower latency slightly.",
            "Was: Die GPU plant ihre Aufgaben selbst. Kann die Verzögerung etwas senken.");
        add("Downside: driver-dependent – may cause stutter on some systems. Needs a restart. 'Unavailable' = not supported by your driver.",
            "Nachteil: Hängt vom Treiber ab – kann auf manchen PCs Ruckler machen. Braucht einen Neustart. „Nicht verfügbar“ = dein Treiber kann das nicht.");
        add("Open graphics settings", "Grafikeinstellungen öffnen");
        add("Windows Gaming Services (status only)", "Windows-Gaming-Dienste (nur Anzeige)");
        add("These services are only needed for Microsoft Store / Xbox games and controllers. Fortnite (Epic) does not need them. ConstiBoost does not disable services.",
            "Diese Dienste braucht man nur für Spiele aus dem Microsoft Store / Xbox und für Controller. Fortnite (Epic) braucht sie nicht. ConstiBoost schaltet keine Dienste ab.");

        // Hardware
        add("Live monitor, processes, network and benchmark. Monitoring only runs while a tab is visible.",
            "Live-Anzeige, Prozesse, Netzwerk und Leistungstest. Gemessen wird nur, solange ein Tab offen ist.");
        add("Monitor", "Live-Anzeige");
        add("CPU USAGE", "CPU-AUSLASTUNG");
        add("GPU USAGE", "GPU-AUSLASTUNG");
        add("RAM USAGE", "RAM-AUSLASTUNG");
        add("CPU TEMPERATURE", "CPU-TEMPERATUR");
        add("Read from HWiNFO / LibreHardwareMonitor if running – ConstiBoost installs no drivers.",
            "Kommt von HWiNFO / LibreHardwareMonitor, wenn eins davon läuft – ConstiBoost installiert keine Treiber.");
        add("GPU TEMPERATURE", "GPU-TEMPERATUR");
        add("Integrated GPUs share the CPU package sensor; no supported source.",
            "Eingebaute GPUs teilen sich den Sensor mit der CPU. Keine passende Quelle.");
        add("ACPI THERMAL ZONE", "ACPI-TEMPERATURZONE");
        add("SYSTEM DETAILS", "SYSTEM-INFOS");
        add("DRIVES", "LAUFWERKE");
        add("Processes", "Prozesse");
        add("CLOSE PROCESS", "PROZESS BEENDEN");
        add("Protected = critical Windows/security process (never closed). Caution = closing has side effects (e.g. explorer.exe). Click a column header to sort.",
            "Geschützt = wichtiger Windows- oder Sicherheits-Prozess (wird nie beendet). Vorsicht = Beenden hat Folgen (z. B. explorer.exe). Klick auf eine Spalte zum Sortieren.");
        add("PROCESS", "PROZESS");
        add("PID", "PID");
        add("CPU %", "CPU %");
        add("GPU %", "GPU %");
        add("STATUS", "STATUS");
        add("SAFETY", "SICHERHEIT");
        add("Network", "Netzwerk");
        add("REFRESH", "AKTUALISIEREN");
        add("CONNECTION", "VERBINDUNG");
        add("LIVE DOWNLOAD", "LIVE-DOWNLOAD");
        add("current traffic", "aktueller Datenverkehr");
        add("LIVE UPLOAD", "LIVE-UPLOAD");
        add("DOWNLOAD (TEST)", "DOWNLOAD (TEST)");
        add("speed test", "Geschwindigkeitstest");
        add("UPLOAD (TEST)", "UPLOAD (TEST)");
        add("PING TEST", "PING-TEST");
        add("DNS TEST", "DNS-TEST");
        add("SPEED TEST", "TEMPO-TEST");
        add("TESTS", "TESTS");
        add("PING SERVERS", "SERVER ANPINGEN");
        add("🎮 GAME SERVER PING (FORTNITE)", "🎮 PING ZU DEN SPIELSERVERN (FORTNITE)");
        add("Uses Epic's public ping endpoints per data center. Choose the region with the lowest ping in Fortnite (Settings > Game > Matchmaking Region). 'Timeout' = the server does not answer pings.",
            "Nutzt die öffentlichen Ping-Adressen von Epic für jedes Rechenzentrum. Wähle in Fortnite die Region mit dem niedrigsten Ping (Einstellungen > Spiel > Matchmaking-Region). „Timeout“ = der Server antwortet nicht auf Pings.");
        add("TEST", "TESTEN");
        add("📶 LATENCY UNDER LOAD (BUFFERBLOAT)", "📶 PING UNTER LAST (BUFFERBLOAT)");
        add("📡 WI-FI QUALITY", "📡 WLAN-QUALITÄT");
        add("Signal", "Signal");
        add("Band", "Band");
        add("Channel", "Kanal");
        add("Standard", "Standard");
        add("Link (down/up)", "Verbindung (runter/hoch)");
        add("Honest note: no registry tweak reliably lowers your ping – ping depends on your connection, Wi-Fi signal and the server distance. For lower ping use a LAN cable and close downloads/streams. The DNS test only measures; ConstiBoost never changes your DNS settings. If a different DNS is faster, you can set it yourself in Windows Settings > Network.",
            "Ehrlich gesagt: Kein Registry-Trick senkt deinen Ping zuverlässig. Der Ping hängt von deiner Leitung, dem WLAN-Signal und der Entfernung zum Server ab. Für weniger Ping: LAN-Kabel nutzen und Downloads/Streams schließen. Der DNS-Test misst nur – ConstiBoost ändert nie deine DNS-Einstellungen. Wenn ein anderer DNS schneller ist, kannst du ihn selbst einstellen: Windows-Einstellungen > Netzwerk.");
        add("Benchmark", "Leistungstest");
        add("System Performance Benchmark", "Leistungstest für dein System");
        add("Measures CPU (single/multi-thread), memory bandwidth, background load and scheduling jitter on this PC (~15 seconds). This is NOT a Fortnite FPS benchmark – it shows whether your system got faster or calmer. Run 'Before', optimize, then run 'After'. Differences below ~3 % are normal measurement noise. Close other apps for consistent results.",
            "Misst auf diesem PC die CPU (ein Kern / alle Kerne), das Speichertempo, die Hintergrundlast und wie gleichmäßig Windows Aufgaben verteilt (ca. 15 Sekunden). Das ist KEIN Fortnite-FPS-Test – es zeigt, ob dein System schneller oder ruhiger wurde. Erst „Vorher“ messen, dann optimieren, dann „Nachher“ messen. Unterschiede unter ca. 3 % sind normale Messschwankungen. Schließ andere Apps für gleichbleibende Ergebnisse.");
        add("RUN BEFORE", "VORHER MESSEN");
        add("RUN AFTER", "NACHHER MESSEN");
        add("Clear history", "Verlauf löschen");
        add("BEFORE", "VORHER");
        add("Performance Score", "Leistungswert");
        add("AFTER", "NACHHER");
        add("DIFFERENCE", "UNTERSCHIED");

        // Settings
        add("Theme", "Design");
        add("Dark", "Dunkel");
        add("Midnight (OLED black)", "Mitternacht (OLED-Schwarz)");
        add("Accent Color", "Akzentfarbe");
        add("Start with Windows", "Mit Windows starten");
        add("Starts ConstiBoost minimized at sign-in (per-user, no admin rights). It stays idle until you open it.",
            "Startet ConstiBoost klein beim Anmelden (nur für dich, ohne Admin-Rechte). Es tut nichts, bis du es öffnest.");
        add("Create Restore Point", "Wiederherstellungspunkt erstellen");
        add("Offer a Windows System Restore point (UAC) before larger optimization runs. Windows allows at most one every 24 hours by default.",
            "Vor größeren Optimierungen einen Windows-Wiederherstellungspunkt anbieten (UAC). Windows erlaubt normal höchstens einen alle 24 Stunden.");
        add("Automatic Backup", "Automatische Sicherung");
        add("Always on for safety: ConstiBoost never changes a system setting without saving the previous value first.",
            "Zur Sicherheit immer an: ConstiBoost ändert nie eine Systemeinstellung, ohne vorher den alten Wert zu speichern.");
        add("Thermal Monitoring", "Temperatur-Überwachung");
        add("Measures temperature and load every second in the background and shows calm in-app alerts if the temperature stays very high. Uses a little CPU while active.",
            "Misst jede Sekunde im Hintergrund Temperatur und Auslastung. Bleibt es sehr heiß, kommt ein ruhiger Hinweis in der App. Braucht dabei etwas CPU.");
        add("Notifications", "Benachrichtigungen");
        add("In-app success/info messages. Warnings and errors are always shown.",
            "Erfolgs- und Info-Meldungen in der App. Warnungen und Fehler werden immer gezeigt.");
        add("Tray, hotkeys & overlay", "Taskleiste, Tastenkürzel & Overlay");
        add("Keep running in the tray when closed", "Beim Schließen in der Taskleiste weiterlaufen");
        add("Needed for Auto-Focus, hotkeys, Auto-Cool and weekly maintenance. Exit via the tray icon.",
            "Nötig für Auto-Fokus, Tastenkürzel, Auto-Kühlen und die wöchentliche Pflege. Beenden über das Symbol in der Taskleiste.");
        add("Global hotkeys", "Tastenkürzel (überall)");
        add("SHOW / HIDE OVERLAY", "OVERLAY ZEIGEN / AUSBLENDEN");
        add("FPS overlay", "FPS-Overlay");
        add("Small click-through window with real FPS, 1 % low, temperature, CPU/GPU/RAM. Visible in borderless/windowed mode (exclusive fullscreen can hide it). No injection – anti-cheat safe.",
            "Kleines Fenster, durch das du durchklicken kannst: echte FPS, 1 % Low, Temperatur, CPU/GPU/RAM. Sichtbar im Fenster- oder randlosen Modus (echtes Vollbild kann es verdecken). Greift nicht ins Spiel ein – sicher für Anti-Cheat.");
        add("Top left", "Oben links");
        add("Top right", "Oben rechts");
        add("Bottom left", "Unten links");
        add("Bottom right", "Unten rechts");
        add("Overlay shows real FPS (one UAC prompt per session)", "Overlay zeigt echte FPS (eine UAC-Abfrage pro Sitzung)");
        add("Updates & maintenance", "Updates & Pflege");
        add("Check for updates on start", "Beim Start nach Updates suchen");
        add("One anonymous request to GitHub Releases – no personal or system data is sent.",
            "Eine anonyme Anfrage an GitHub Releases – es werden keine persönlichen Daten oder Systemdaten gesendet.");
        add("CHECK NOW", "JETZT PRÜFEN");
        add("INSTALL UPDATE", "UPDATE INSTALLIEREN");
        add("Weekly maintenance", "Wöchentliche Pflege");
        add("Once a week (never while gaming): removes old temp files and crash reports, checks startup apps, free space and driver age.",
            "Einmal pro Woche (nie beim Spielen): löscht alte Temp-Dateien und Absturzberichte, prüft Autostart-Apps, freien Speicher und das Alter der Treiber.");
        add("RUN NOW", "JETZT STARTEN");
        add("Privacy & safety", "Datenschutz & Sicherheit");
        add("ConstiBoost works 100 % locally: no account, no cloud, no telemetry, no ads. It never disables Defender, the firewall or Windows Update, never reads passwords, cookies or browser data, and never deletes personal files. Network access: the tests you start yourself (ping, DNS, speed, bufferbloat) and – if enabled – the anonymous update check on GitHub.",
            "ConstiBoost läuft zu 100 % auf deinem PC: kein Konto, keine Cloud, keine Datensammlung, keine Werbung. Es schaltet nie Defender, die Firewall oder Windows Update ab, liest nie Passwörter, Cookies oder Browserdaten und löscht nie persönliche Dateien. Ins Internet geht es nur für Tests, die du selbst startest (Ping, DNS, Tempo, Bufferbloat), und – wenn an – für die anonyme Update-Prüfung bei GitHub.");
        add("Data folder (settings, backups, logs): {0}", "Datenordner (Einstellungen, Sicherungen, Protokolle): {0}");
        add("Show welcome screen again", "Willkommens-Seite nochmal zeigen");

        // Backups
        add("OPEN FOLDER", "ORDNER ÖFFNEN");
        add("SYSTEM RESTORE", "SYSTEMWIEDERHERSTELLUNG");
        add("CREATE RESTORE POINT", "WIEDERHERSTELLUNGSPUNKT ERSTELLEN");
        add("Backups & Restore", "Sicherungen & Wiederherstellen");
        add("No backups yet. ConstiBoost creates one automatically before the first change.",
            "Noch keine Sicherungen. ConstiBoost macht vor der ersten Änderung automatisch eine.");
        add("Delete", "Löschen");
        add("Saved values", "Gespeicherte Werte");
        add("Activity log", "Protokoll");
        add("OPEN LOG FILES", "PROTOKOLLDATEIEN ÖFFNEN");
        add("Every change: timestamp, action, old → new value and result. Full history is in the log files.",
            "Jede Änderung: Zeit, Aktion, alter → neuer Wert und Ergebnis. Den ganzen Verlauf findest du in den Protokolldateien.");
        add("TIME", "ZEIT");
        add("ACTION", "AKTION");
        add("CHANGE", "ÄNDERUNG");
        add("RESULT", "ERGEBNIS");
        add("Reason: {0}", "Grund: {0}");

        // Cleaner
        add("SCAN", "SUCHEN");
        add("Only safe cache and temp folders. Personal files are never touched.",
            "Nur sichere Cache- und Temp-Ordner. Deine persönlichen Dateien werden nie angefasst.");
        add("CLEAN", "AUFRÄUMEN");
        add("{0} files", "{0} Dateien");
        add("Never deleted: Documents, Pictures, Desktop, Downloads, Music, Videos, OneDrive, game saves, browser cookies, passwords and history. Symbolic links / junctions are never followed. Files in use are skipped.",
            "Wird nie gelöscht: Dokumente, Bilder, Desktop, Downloads, Musik, Videos, OneDrive, Spielstände, Browser-Cookies, Passwörter und Verlauf. Verknüpfte Ordner (Symlinks / Junctions) werden nie betreten. Dateien, die gerade benutzt werden, werden übersprungen.");

        // Startup
        add("TASK MANAGER", "TASK-MANAGER");
        add("Startup Manager", "Autostart verwalten");
        add("Disabling uses the same switch as Task Manager – nothing is deleted. Windows, security, driver and hardware utilities are protected and cannot be disabled here. 'Typical impact' is based on the app category, not measured. Entries for all users need administrator permission (UAC).",
            "Ausschalten nutzt denselben Schalter wie der Task-Manager – nichts wird gelöscht. Windows-, Sicherheits-, Treiber- und Hardware-Programme sind geschützt und lassen sich hier nicht ausschalten. „Typische Auswirkung“ ist nach Art der App geschätzt, nicht gemessen. Einträge für alle Benutzer brauchen Admin-Rechte (UAC).");
        add("APP", "APP");
        add("PUBLISHER", "HERSTELLER");
        add("STARTUP IMPACT", "AUSWIRKUNG BEIM START");

        // Welcome
        add("WELCOME TO CONSTIBOOST", "WILLKOMMEN BEI CONSTIBOOST");
        add("Let's analyze your PC.", "Lass uns deinen PC prüfen.");
        add("ConstiBoost reads your hardware and gaming settings locally. Nothing is changed during the scan and no data leaves your PC.",
            "ConstiBoost liest deine Hardware und Gaming-Einstellungen direkt auf deinem PC. Beim Prüfen wird nichts geändert und keine Daten verlassen deinen PC.");
        add("SCAN MY PC", "MEINEN PC PRÜFEN");
        add("Scanning your PC…", "Dein PC wird geprüft…");
        add("Your PC is ready.", "Dein PC ist fertig geprüft.");
        add("Gaming Readiness", "Gaming-Bereitschaft");
        add("Recommended optimizations:", "Empfohlene Optimierungen:");
        add("GO TO DASHBOARD", "ZUR ÜBERSICHT");
        add("⚡ REVIEW OPTIMIZATIONS", "⚡ OPTIMIERUNGEN ANSEHEN");

        // Confirm dialog
        add("CANCEL", "ABBRECHEN");

        // Templates (Gaming Mode card, optimization card)
        add("GAMING MODE", "GAMING-MODUS");
        add("Temporary session: Game Mode ON, power settings for the chosen profile, selected apps closed and background apps lowered in priority. Stopping Gaming Mode restores everything that can be restored (and reopens closed apps if enabled).",
            "Nur für diese Sitzung: Spielmodus AN, Energie-Einstellungen nach gewähltem Profil, ausgewählte Apps werden geschlossen und Hintergrund-Apps bekommen weniger Vorrang. Beim Stoppen wird alles zurückgestellt, was geht (und geschlossene Apps werden wieder geöffnet, wenn eingeschaltet).");
        add("MAX FPS", "MAX FPS");
        add("max performance · warmer", "volle Leistung · wärmer");
        add("BALANCED", "AUSGEWOGEN");
        add("good FPS · cooler", "gute FPS · kühler");
        add("COOL", "KÜHL");
        add("less heat · less FPS", "weniger Hitze · weniger FPS");
        add("Lower priority of browsers/launchers", "Browser/Launcher bekommen weniger Vorrang");
        add("Reopen closed apps afterwards", "Geschlossene Apps danach wieder öffnen");
        add("Cap Fortnite at {0} FPS", "Fortnite auf {0} FPS begrenzen");
        add("An FPS cap is one of the most effective ways to reduce gaming heat. Only applied while Fortnite is closed; the previous value is restored when Gaming Mode stops.",
            "Ein FPS-Limit ist einer der besten Wege, um beim Spielen weniger Hitze zu haben. Wird nur gesetzt, wenn Fortnite zu ist. Beim Stoppen vom Gaming-Modus kommt der alte Wert zurück.");
        add("Choose apps to close…", "Apps zum Schließen wählen…");
        add("Admin (UAC)", "Admin (UAC)");
        add("CURRENT", "JETZT");
        add("RECOMMENDED", "EMPFOHLEN");
        add("Why: ", "Warum: ");
        add("What does it do? · Possible downsides", "Was macht das? · Mögliche Nachteile");
        add("Downsides: ", "Nachteile: ");
        add("IMPACT", "WIRKUNG");
    }
}
