using System;

namespace ConstiBoost.Localization;

/// <summary>German texts for GamesView, ThermalView, FortniteView, FpsBoostView and BoostLabView.</summary>
internal static class DeViews1
{
    public static void Register(Action<string, string> add)
    {
        // ---------- GamesView ----------
        add("▶ PLAY", "▶ SPIELEN");
        add("OPEN FOLDER", "ORDNER ÖFFNEN");
        add("UNINSTALL", "DEINSTALLIEREN");
        add("⚡ PERFORMANCE LAUNCH", "⚡ LEISTUNGS-START");
        add("⚡ PERFORMANCE LAUNCH (AUTOMATIC LOW SETTINGS)", "⚡ LEISTUNGS-START (AUTOMATISCH NIEDRIGE EINSTELLUNGEN)");
        add("Opens the game in YOUR normal browser (ad blocker and extensions keep working) with everything set for FPS: GPU block list off, GPU rasterization, high-performance GPU, smaller render window and optionally Game Focus. If the browser is running, ConstiBoost asks to restart it once – your tabs come back automatically.",
            "Öffnet das Spiel in DEINEM normalen Browser (Werbeblocker und Erweiterungen laufen weiter) – alles auf FPS eingestellt: GPU-Sperrliste aus, GPU-Rasterung an, starke GPU, kleineres Bild und auf Wunsch Spiel-Fokus. Läuft der Browser schon, fragt ConstiBoost einmal, ob er neu starten darf – deine Tabs kommen automatisch zurück.");
        add("Game address", "Adresse des Spiels");
        add("Render size", "Bildgröße");
        add("75 % (recommended)", "75 % (empfohlen)");
        add("60 % (max FPS)", "60 % (max. FPS)");
        add("Also start 🎯 Game Focus (close background apps, Max FPS power mode, boost the game tab + browser GPU process)",
            "Auch 🎯 Spiel-Fokus starten (Hintergrund-Apps schließen, Energiemodus Max FPS, Spiel-Tab + GPU-Prozess des Browsers bevorzugen)");
        add("Unlock FPS above the display refresh rate (VSync off – more FPS on the counter, more heat, possible tearing)",
            "FPS über der Bildwiederholrate erlauben (VSync aus – höhere FPS-Zahl, mehr Wärme, Bild kann reißen)");
        add("Honest note: the graphics menu inside a website belongs to the website – no external program can switch it. If the game has its own options (quality, shadows, draw distance), set them to the lowest once; the website remembers them.",
            "Ehrlich gesagt: Das Grafikmenü auf einer Website gehört der Website – kein anderes Programm kann es umstellen. Hat das Spiel eigene Optionen (Qualität, Schatten, Sichtweite), stell sie einmal auf das Niedrigste. Die Website merkt sich das.");
        add("🌐 BROWSER GAME BOOST", "🌐 BROWSERSPIEL-BOOST");
        add("Web games (WebGL) run inside the browser, so the browser is the game. Honest note: no tool can push FPS far beyond what the web port and your graphics allow – but these steps remove the most common brakes. Use 🎯 FOCUS below, then MEASURE to see the real effect.",
            "Web-Spiele (WebGL) laufen im Browser – der Browser ist also das Spiel. Ehrlich gesagt: Kein Tool holt viel mehr FPS heraus, als die Web-Version und deine Grafik schaffen. Aber diese Schritte entfernen die häufigsten Bremsen. Nutze unten 🎯 FOKUS und dann MESSEN, um die echte Wirkung zu sehen.");
        add("1. Hardware acceleration must be ON (without it the CPU renders the game – the #1 cause of lag)",
            "1. Hardwarebeschleunigung muss AN sein (sonst berechnet die CPU das Spiel – der häufigste Grund für Ruckeln)");
        add("Copy address", "Adresse kopieren");
        add("Check: ", "Prüfen: ");
        add(" → 'WebGL: Hardware accelerated' should be shown", " → dort sollte 'WebGL: Hardware accelerated' stehen");
        add("Only 5-10 FPS? Then WebGL very likely runs on the CPU. Run 🔍 ANALYZE below while playing – it detects this. Permanent fix in your normal browser (open the page, set the option, click Relaunch):",
            "Nur 5-10 FPS? Dann läuft WebGL sehr wahrscheinlich auf der CPU. Starte unten beim Spielen 🔍 ANALYSIEREN – das erkennt es. Dauerhafte Lösung in deinem normalen Browser (Seite öffnen, Option setzen, auf Relaunch / Neu starten klicken):");
        add("2. Close all other tabs and windows – every open tab costs CPU and memory (videos and streams especially).",
            "2. Schließ alle anderen Tabs und Fenster – jeder offene Tab kostet CPU und Speicher (vor allem Videos und Streams).");
        add("3. Play in a smaller window instead of fullscreen: many web games render at window size – a smaller window means fewer pixels and often clearly more FPS on integrated graphics. Ctrl + minus (browser zoom out) can have the same effect.",
            "3. Spiel in einem kleineren Fenster statt im Vollbild: Viele Web-Spiele rechnen in Fenstergröße. Kleineres Fenster = weniger Pixel = bei eingebauter Grafik oft deutlich mehr FPS. Strg + Minus (Browser verkleinern) kann genauso helfen.");
        add("4. Turn off the browser's energy saver / efficiency mode while playing and keep the laptop plugged in.",
            "4. Schalte beim Spielen den Energiesparmodus des Browsers aus und lass den Laptop am Ladekabel.");
        add("5. Lower the game's own graphics options if it has any (resolution, draw distance, shadows).",
            "5. Stell die Grafikoptionen im Spiel niedriger, falls es welche gibt (Auflösung, Sichtweite, Schatten).");
        add("6. 🎯 FOCUS ON THIS GAME: closes background apps, Gaming Mode MAX FPS, mutes notifications and gives the game tab and the browser GPU process 'Above normal' priority (other tabs and browsers are not boosted).",
            "6. 🎯 FOKUS AUF DIESES SPIEL: schließt Hintergrund-Apps, Gaming-Modus MAX FPS, schaltet Benachrichtigungen stumm und gibt dem Spiel-Tab und dem GPU-Prozess des Browsers die Priorität 'Höher als normal' (andere Tabs und Browser nicht).");
        add("Firefox: the automatic Performance Launch is unavailable (needs Chrome, Edge, Brave or Opera). Chromium browsers usually run WebGL games faster on AMD graphics.",
            "Firefox: Der automatische Leistungs-Start ist nicht verfügbar (braucht Chrome, Edge, Brave oder Opera). Mit AMD-Grafik laufen WebGL-Spiele in Chromium-Browsern meist schneller.");
        add("Safety: unofficial websites that host commercial games are often unlicensed and full of ads/pop-ups. Don't download or run files from them and don't enter passwords. ConstiBoost never reads browser data (history, cookies, passwords).",
            "Sicherheit: Inoffizielle Websites mit Kauf-Spielen sind oft ohne Lizenz und voller Werbung/Pop-ups. Lade dort keine Dateien herunter, starte nichts und gib keine Passwörter ein. ConstiBoost liest nie Browserdaten (Verlauf, Cookies, Passwörter).");
        add("🔍 ANALYZE (12 s)", "🔍 ANALYSIEREN (12 s)");
        add("🔍 WHY IS MY GPU NOT AT 100 %?", "🔍 WARUM IST MEINE GPU NICHT BEI 100 %?");
        add("Start the game, click ANALYZE and keep playing for 12 seconds. ConstiBoost measures the real load of every CPU thread, the GPU, temperature and clock and tells you what limits your FPS – and what actually helps. Read-only, nothing is changed.",
            "Starte das Spiel, klick auf ANALYSIEREN und spiel 12 Sekunden weiter. ConstiBoost misst die echte Last jedes CPU-Threads, der GPU, Temperatur und Takt und sagt dir, was deine FPS bremst – und was wirklich hilft. Nur Lesen, es wird nichts geändert.");
        add("📏 MEASURE 60 s", "📏 MESSEN 60 s");
        add("Cancel", "Abbrechen");
        add("📏 REAL FPS MEASUREMENT", "📏 ECHTE FPS-MESSUNG");
        add("Measured from Windows' own DirectX frame events (like PresentMon) – no injection, anti-cheat safe. Needs one UAC prompt. DirectX games only (OpenGL/Vulkan not yet).",
            "Gemessen über die DirectX-Bild-Ereignisse von Windows selbst (wie PresentMon) – nichts wird ins Spiel eingeschleust, sicher für Anti-Cheat. Braucht einmal eine Admin-Abfrage (UAC). Nur DirectX-Spiele (OpenGL/Vulkan noch nicht).");
        add("Setting used while measuring:", "Einstellung beim Messen:");
        add("Selected: {0}", "Ausgewählt: {0}");
        add("AVG FPS", "Ø FPS");
        add("1 % LOW", "1 % TIEF");
        add("0.1 % LOW", "0,1 % TIEF");
        add("STUTTERS", "RUCKLER");
        add("FRAME TIME", "BILDZEIT");
        add("FPS PER SECOND", "FPS PRO SEKUNDE");
        add("HISTORY", "VERLAUF");
        add("🎚️ GUIDED FPS TUNER", "🎚️ FPS-ASSISTENT");
        add("Target", "Ziel");
        add("Loop: measure → apply one step → play the same situation → measure again. Every step is backed up.",
            "Ablauf: messen → einen Schritt anwenden → gleiche Situation spielen → nochmal messen. Jeder Schritt wird gesichert.");
        add("Also cap FPS at {0} (if the game supports it in this file)", "FPS auch auf {0} begrenzen (wenn das Spiel das in dieser Datei kann)");
        add("📈 FPS ESTIMATE", "📈 FPS-SCHÄTZUNG");
        add("Render scale", "Bildgröße (Render-Skalierung)");
        add("Resolution: ", "Auflösung: ");
        add("YOUR SETTING", "DEINE EINSTELLUNG");
        add("  · above display refresh", "  · über der Bildwiederholrate");
        add("WHAT THE ESTIMATE IS BASED ON", "WORAUF DIE SCHÄTZUNG BERUHT");
        add("Estimate – not a measurement. Based on typical results of comparable hardware in average scenes. Real FPS vary with map, scene, drivers, game updates and temperature.",
            "Schätzung – keine Messung. Beruht auf typischen Werten ähnlicher Hardware in normalen Szenen. Echte FPS hängen von Karte, Szene, Treibern, Spiel-Updates und Temperatur ab.");
        add("🎯 GAME FOCUS", "🎯 SPIEL-FOKUS");
        add("Puts every safe system resource behind this game. All changes are reversible with STOP FOCUS.",
            "Gibt diesem Spiel alle Leistung, die sicher geht. Mit FOKUS STOPPEN wird alles rückgängig gemacht.");
        add("MAX FPS", "MAX FPS");
        add("BALANCED", "AUSGEWOGEN");
        add("COOL", "KÜHL");
        add("Saves the options below. When the game starts, Focus starts automatically; when it closes, everything is restored. ConstiBoost must be running (tray).",
            "Speichert die Optionen unten. Startet das Spiel, startet der Fokus von selbst. Wird es beendet, wird alles zurückgestellt. ConstiBoost muss laufen (Infobereich unten rechts).");
        add("Auto-Focus when this game starts", "Fokus automatisch, wenn dieses Spiel startet");
        add("Game priority 'Above normal' while running", "Spiel-Priorität 'Höher als normal', solange es läuft");
        add("Windows GPU preference: High performance", "Windows-GPU-Einstellung: Hohe Leistung");
        add("Settings > System > Display > Graphics. Matters on PCs with two GPUs; harmless on integrated-only systems.",
            "Einstellungen > System > Bildschirm > Grafik. Wichtig bei PCs mit zwei GPUs. Bei nur eingebauter Grafik schadet es nicht.");
        add("Mute notification banners", "Benachrichtigungen stumm schalten");
        add("Lower priority of browsers/launchers", "Browser/Launcher niedriger priorisieren");
        add("CLOSE WHILE PLAYING", "BEIM SPIELEN SCHLIESSEN");
        add("Cap Fortnite at {0} FPS (less heat)", "Fortnite auf {0} FPS begrenzen (weniger Wärme)");
        add("Fortnite Tools – presets, current config, shader cache, process priority",
            "Fortnite-Werkzeuge – Voreinstellungen, aktuelle Einstellungen, Shader-Cache, Prozess-Priorität");
        add("RESCAN", "NEU SUCHEN");
        add("🎮 Games", "🎮 Spiele");
        add("Sort:", "Sortieren:");
        add("Last played", "Zuletzt gespielt");
        add("Size", "Größe");
        add("No games found. ConstiBoost reads Steam libraries, Epic Games Launcher manifests and Windows app entries of game publishers (GOG, EA, Ubisoft, Riot, Battle.net, Rockstar, Roblox …). Microsoft Store / Xbox app games are not listed yet.",
            "Keine Spiele gefunden. ConstiBoost liest Steam-Bibliotheken, Daten vom Epic Games Launcher und Windows-App-Einträge von Spielefirmen (GOG, EA, Ubisoft, Riot, Battle.net, Rockstar, Roblox …). Spiele aus dem Microsoft Store / der Xbox-App werden noch nicht angezeigt.");
        add("RUNNING", "LÄUFT");
        add("🎯 FOCUS", "🎯 FOKUS");

        // ---------- ThermalView ----------
        add("Measures every second in the background (also when this page is closed) and shows in-app alerts if the temperature stays very high.",
            "Misst jede Sekunde im Hintergrund (auch wenn diese Seite zu ist) und warnt dich in der App, wenn die Temperatur lange sehr hoch bleibt.");
        add("Enable Thermal Monitoring", "Temperatur-Überwachung an");
        add("🌡️ Thermal Monitor", "🌡️ Temperatur");
        add("Real sensor values, diagnosis and safe measures against unnecessary heat. Temperature levels are guide values, not a technical guarantee.",
            "Echte Sensorwerte, Diagnose und sichere Tipps gegen unnötige Wärme. Die Temperaturstufen sind Richtwerte, keine technische Garantie.");
        add("🔍 SCAN MY PC", "🔍 MEINEN PC PRÜFEN");
        add("❄️ FIND WAYS TO LOWER TEMPERATURE", "❄️ WEGE ZUM ABKÜHLEN FINDEN");
        add("🌡️ 60 SECOND THERMAL TEST", "🌡️ 60-SEKUNDEN-TEMPERATURTEST");
        add("CPU: ", "CPU: ");
        add("Architecture: ", "Architektur: ");
        add("Cores: ", "Kerne: ");
        add("Threads: ", "Threads: ");
        add("Base clock: ", "Grundtakt: ");
        add("CPU TEMPERATURE", "CPU-TEMPERATUR");
        add("< 60 °C Cool · 60–75 Normal · 75–85 Warm · 85–95 Hot · ≥ 95 Critical. Guide values only – laptop CPUs are designed to run warm under load and protect themselves automatically.",
            "< 60 °C kühl · 60–75 normal · 75–85 warm · 85–95 heiß · ≥ 95 kritisch. Nur Richtwerte – Laptop-CPUs dürfen unter Last warm werden und schützen sich selbst.");
        add("Exact CPU temperature: run HWiNFO (Settings → Shared Memory Support) or LibreHardwareMonitor in the background. ConstiBoost only reads their values; it installs no drivers. Otherwise the Windows ACPI thermal zone is used (indirect).",
            "Genaue CPU-Temperatur: Lass HWiNFO (Settings → Shared Memory Support) oder LibreHardwareMonitor im Hintergrund laufen. ConstiBoost liest nur deren Werte und installiert keine Treiber. Sonst wird der Windows-ACPI-Temperaturwert genutzt (nur ungefähr).");
        add("CPU LOAD", "CPU-LAST");
        add("CPU CLOCK", "CPU-TAKT");
        add("GPU LOAD", "GPU-LAST");
        add("GPU CLOCK", "GPU-TAKT");
        add("GPU TEMPERATURE", "GPU-TEMPERATUR");
        add("FAN", "LÜFTER");
        add("POWER PROFILE", "ENERGIEPLAN");
        add("ACTIVE PROCESSES", "AKTIVE PROZESSE");
        add("TEMPERATURE HISTORY (LAST 5 MINUTES, 0–110 °C)", "TEMPERATURVERLAUF (LETZTE 5 MINUTEN, 0–110 °C)");
        add("CPU LOAD (0–100 %)", "CPU-LAST (0–100 %)");
        add("PER MINUTE", "PRO MINUTE");
        add("Time", "Zeit");
        add("Temperature", "Temperatur");
        add("Phase", "Phase");
        add("🎮 FORTNITE THERMAL MODE", "🎮 FORTNITE-KÜHLMODUS");
        add("Fortnite detected", "Fortnite erkannt");
        add("GPU: ", "GPU: ");
        add("RAM: ", "RAM: ");
        add("Temperature: ", "Temperatur: ");
        add("SCAN COMPLETE", "PRÜFUNG FERTIG");
        add("Temperature:", "Temperatur:");
        add("CPU Load:", "CPU-Last:");
        add("Power:", "Energie:");
        add("Main cause:", "Hauptgrund:");
        add("Recommendations:", "Empfehlungen:");
        add("Potential improvements:", "Mögliche Verbesserungen:");
        add("Temperature source: {0}", "Temperatur-Quelle: {0}");
        add("APPLY SAFE OPTIMIZATIONS", "SICHERE OPTIMIERUNGEN ANWENDEN");
        add("THERMAL HEALTH", "TEMPERATUR-ZUSTAND");
        add("CPU Load: ", "CPU-Last: ");
        add("Cooling: ", "Kühlung: ");
        add("Background Load: ", "Hintergrund-Last: ");
        add("The score is a rough guide based on measured values – not a technical or safety statement.",
            "Die Punktzahl ist nur ein grober Richtwert aus Messwerten – keine technische oder Sicherheits-Aussage.");
        add("Thermal cause detection", "Warum wird es warm?");
        add("SMART RECOMMENDATIONS", "TIPPS FÜR DICH");
        add("Top CPU", "Top CPU");
        add("Top RAM", "Top RAM");
        add("Top GPU", "Top GPU");
        add("No process used the GPU noticeably during the scan.", "Bei der Prüfung hat kein Prozess die GPU spürbar genutzt.");
        add("❄️ OPTIMIZE TEMPERATURE", "❄️ TEMPERATUR SENKEN");
        add("Ways to lower temperature", "Wege zum Abkühlen");
        add("Sorted by priority: real CPU load → background → power mode → startup → overlays → cooling → optional Windows settings. No measure promises a specific temperature drop.",
            "Nach Wichtigkeit sortiert: echte CPU-Last → Hintergrund → Energiemodus → Autostart → Overlays → Kühlung → optionale Windows-Einstellungen. Keine Maßnahme verspricht eine bestimmte Gradzahl weniger.");
        add("CURRENT", "AKTUELL");
        add("RECOMMENDED", "EMPFOHLEN");
        add("Trade-off: {0}", "Nachteil: {0}");
        add("POTENTIAL THERMAL IMPACT", "MÖGLICHE WIRKUNG AUF DIE TEMPERATUR");
        add("THERMAL RESULT", "TEMPERATUR-ERGEBNIS");
        add("BEFORE", "VORHER");
        add("AFTER", "NACHHER");
        add("DIFFERENCE", "UNTERSCHIED");
        add("Temperature (average)", "Temperatur (Durchschnitt)");
        add("CPU load", "CPU-Last");
        add("Background processes", "Hintergrund-Prozesse");
        add("Temperatures need a few minutes to settle and depend on workload and room temperature. Run the 60 second thermal test again later for a better comparison.",
            "Temperaturen brauchen ein paar Minuten, bis sie sich einpendeln, und hängen von der Last und der Raumtemperatur ab. Mach den 60-Sekunden-Test später nochmal für einen besseren Vergleich.");
        add("Measures the real temperature and CPU load every second for 60 seconds. Nothing is simulated.",
            "Misst 60 Sekunden lang jede Sekunde die echte Temperatur und CPU-Last. Nichts wird vorgetäuscht.");
        add("Monitoring… {0}/60 s", "Messe… {0}/60 s");
        add("START", "START");
        add("AVERAGE", "DURCHSCHNITT");
        add("MAXIMUM", "MAXIMUM");
        add("FINAL", "AM ENDE");
        add("AVG CPU LOAD", "Ø CPU-LAST");
        add("AVG CPU CLOCK", "Ø CPU-TAKT");
        add("🎮 COOL GAMING MODE", "🎮 KÜHLER GAMING-MODUS");
        add("Best possible gaming performance with less unnecessary heat. ConstiBoost never disables thermal protection, changes voltages or touches the fan control.",
            "Möglichst viel Spielleistung mit weniger unnötiger Wärme. ConstiBoost schaltet nie den Hitzeschutz ab, ändert keine Spannungen und fasst die Lüftersteuerung nicht an.");
        add("PERFORMANCE vs. TEMPERATURE (MEASURED)", "LEISTUNG vs. TEMPERATUR (GEMESSEN)");
        add("Profile", "Profil");
        add("Avg temp", "Ø Temp.");
        add("Max temp", "Max. Temp.");
        add("Avg CPU clock", "Ø CPU-Takt");
        add("Avg load", "Ø Last");
        add("Tested", "Getestet");
        add("FPS are not shown because ConstiBoost has no supported FPS source – the CPU clock is used as a measured performance indicator instead.",
            "FPS werden nicht angezeigt, weil ConstiBoost hier keine passende FPS-Quelle hat. Stattdessen dient der gemessene CPU-Takt als Leistungs-Anzeige.");
        add("⚙️ THERMAL SETTINGS", "⚙️ TEMPERATUR-EINSTELLUNGEN");
        add("ALERTS", "WARNUNGEN");
        add("🟠 High alert at", "🟠 Warnung „hoch“ ab");
        add("🔴 Critical at", "🔴 Kritisch ab");
        add("High for at least", "Hoch für mindestens");
        add("Deep scan (20 s measurement instead of 8 s – more accurate)", "Gründliche Prüfung (20 s statt 8 s messen – genauer)");
        add("Alerts are calm in-app messages only. Your CPU and Windows keep all built-in thermal protection regardless of these settings.",
            "Warnungen sind nur ruhige Hinweise in der App. Deine CPU und Windows behalten ihren eingebauten Hitzeschutz – egal, was du hier einstellst.");
        add("❄️ AUTO-COOL GUARD", "❄️ AUTO-KÜHLUNG");
        add("Enable Auto-Cool", "Auto-Kühlung an");
        add("Trigger above", "Startet ab");
        add("Also while a game is running", "Auch während ein Spiel läuft");
        add("Off = performance first: Auto-Cool stays out of your games.", "Aus = Leistung zuerst: Die Auto-Kühlung lässt deine Spiele in Ruhe.");
        add("Also turn off CPU boost (max processor state 99 %)", "Auch CPU-Boost ausschalten (max. Prozessorleistung 99 %)");
        add("Strongest cooling effect, but noticeably less CPU performance while engaged. Only applied if Windows allows it without an admin prompt.",
            "Kühlt am stärksten, aber die CPU ist solange spürbar langsamer. Wird nur gesetzt, wenn Windows es ohne Admin-Abfrage erlaubt.");
        add("If the temperature stays above the trigger for 60 s, ConstiBoost switches to the Balanced power mode. After 2 minutes at least 8 °C cooler, the previous settings are restored automatically. Paused while Gaming Mode / Game Focus is active.",
            "Bleibt die Temperatur 60 s über dem Startwert, schaltet ConstiBoost auf den Energiemodus „Ausgewogen“. Ist es 2 Minuten lang mindestens 8 °C kühler, kommen die alten Einstellungen automatisch zurück. Pausiert, solange Gaming-Modus / Spiel-Fokus an ist.");
        add("🧊 COOLING CHECK", "🧊 KÜHLUNGS-CHECK");
        add("Things software cannot fix – often the biggest lever on laptops:", "Was keine Software lösen kann – bei Laptops oft das Wichtigste:");
        add("• Place the laptop on a hard, flat surface (desk, laptop stand).", "• Stell den Laptop auf eine harte, gerade Fläche (Tisch, Laptop-Ständer).");
        add("• Do not block the vents – the Aspire 3 takes in air from the bottom.", "• Halte die Lüftungsschlitze frei – der Aspire 3 zieht Luft von unten an.");
        add("• Do not use it on a bed, blanket or your lap while gaming.", "• Beim Spielen nicht auf dem Bett, einer Decke oder dem Schoß benutzen.");
        add("• Consider the room temperature – a warm room means a warmer laptop.", "• Denk an die Raumtemperatur – warmes Zimmer heißt wärmerer Laptop.");
        add("• Dust may reduce cooling performance. Consider checking the vents and cleaning them carefully if needed (laptop off, short bursts of compressed air, hold the fan still).",
            "• Staub kann die Kühlung schwächen. Schau dir die Lüftungsschlitze an und reinige sie bei Bedarf vorsichtig (Laptop aus, kurze Stöße Druckluft, Lüfter dabei festhalten).");
        add("• A slightly raised rear edge can improve airflow under the laptop.", "• Hinten etwas höher stellen kann die Luft unter dem Laptop besser fließen lassen.");
        add("FAN CHECK", "LÜFTER-CHECK");
        add("Many laptops (including most Acer Aspire models) do not report fan speed to Windows. If HWiNFO or LibreHardwareMonitor can read it, ConstiBoost shows it here. No RPM values are ever estimated.",
            "Viele Laptops (auch die meisten Acer Aspire) melden die Lüfterdrehzahl nicht an Windows. Wenn HWiNFO oder LibreHardwareMonitor sie lesen kann, zeigt ConstiBoost sie hier. Drehzahlen werden nie geschätzt.");
        add("Safety: ConstiBoost never changes CPU voltages, never modifies the BIOS or firmware, never controls or turns off fans, never disables thermal throttling or other protection, never disables Defender or Windows Update and never shows invented temperature values. No undervolting or overclocking.",
            "Sicherheit: ConstiBoost ändert nie CPU-Spannungen, nie BIOS oder Firmware, steuert oder stoppt nie Lüfter, schaltet nie die Hitze-Drosselung oder anderen Schutz ab, nie Defender oder Windows Update und zeigt nie erfundene Temperaturen. Kein Undervolting, kein Übertakten.");

        // ---------- FortniteView ----------
        add("REFRESH", "AKTUALISIEREN");
        add("▶ LAUNCH FORTNITE", "▶ FORTNITE STARTEN");
        add("Fortnite Tools", "Fortnite-Werkzeuge");
        add("Presets for Fortnite's own settings file – transparent, backed up, no FPS promises.",
            "Voreinstellungen für Fortnites eigene Einstellungsdatei – nachvollziehbar, gesichert, ohne FPS-Versprechen.");
        add("INSTALLATION", "INSTALLATION");
        add("RECOMMENDATION FOR YOUR PC", "EMPFEHLUNG FÜR DEINEN PC");
        add("We detected a low-power integrated-GPU system.", "Dein PC hat eine schwache, eingebaute Grafik.");
        add("PERFORMANCE MODE", "PERFORMANCE-MODUS");
        add("LOW SETTINGS", "NIEDRIGE EINSTELLUNGEN");
        add("3D RES 70–85 %", "3D-AUFLÖSUNG 70–85 %");
        add("VSYNC OFF", "VSYNC AUS");
        add("MOTION BLUR OFF", "BEWEGUNGSUNSCHÄRFE AUS");
        add("SHADOWS OFF", "SCHATTEN AUS");
        add("FPS Presets", "FPS-Voreinstellungen");
        add("Estimated impact:", "Geschätzte Wirkung:");
        add("Estimated impact = how much the preset lowers your current settings (LOW / MEDIUM / HIGH). It is not an FPS number – real FPS depend on map, scene, drivers and hardware.",
            "Geschätzte Wirkung = wie stark die Voreinstellung deine jetzigen Einstellungen senkt (NIEDRIG / MITTEL / HOCH). Das ist keine FPS-Zahl – echte FPS hängen von Karte, Szene, Treibern und Hardware ab.");
        add("PRESET OPTIONS", "OPTIONEN");
        add("3D Resolution", "3D-Auflösung");
        add("VSync OFF", "VSync AUS");
        add("VSync syncs frames to the monitor: no tearing, but more input lag and FPS drops to 30 when below 60. OFF is recommended for competitive play.",
            "VSync passt die Bilder an den Monitor an: kein Reißen im Bild, aber mehr Eingabeverzögerung, und unter 60 FPS fällt es auf 30. Für Wettkampf-Spiele wird AUS empfohlen.");
        add("Motion Blur OFF", "Bewegungsunschärfe AUS");
        add("Motion blur smears the image when turning. Off = clearer image and slightly less GPU work.",
            "Bewegungsunschärfe verwischt das Bild beim Drehen. Aus = klareres Bild und etwas weniger Arbeit für die GPU.");
        add("Fullscreen", "Vollbild");
        add("Exclusive fullscreen usually gives the lowest input lag and most stable frame times.",
            "Echtes Vollbild bringt meist die kleinste Eingabeverzögerung und die gleichmäßigsten Bildzeiten.");
        add("A frame cap at your display refresh rate gives more even frame times and lower temperatures on laptops.",
            "Eine FPS-Grenze bei deiner Bildwiederholrate sorgt für gleichmäßigere Bildzeiten und bei Laptops für weniger Wärme.");
        add("Cap FPS at {0} (display refresh rate)", "FPS auf {0} begrenzen (Bildwiederholrate)");
        add("Not in your settings file – set it in-game: {0}", "Nicht in deiner Einstellungsdatei – stell es im Spiel ein: {0}");
        add("APPLY PRESET", "VOREINSTELLUNG ANWENDEN");
        add("Fortnite must be closed. GameUserSettings.ini is backed up first and can be restored on the Backups page. Only settings that already exist in your file are changed.",
            "Fortnite muss geschlossen sein. GameUserSettings.ini wird vorher gesichert und kann auf der Seite Sicherungen wiederhergestellt werden. Es werden nur Einstellungen geändert, die schon in deiner Datei stehen.");
        add("Your current Fortnite settings", "Deine aktuellen Fortnite-Einstellungen");
        add("Rendering Mode", "Rendermodus");
        add("View Distance", "Sichtweite");
        add("Shadows", "Schatten");
        add("Textures", "Texturen");
        add("Effects", "Effekte");
        add("Motion Blur", "Bewegungsunschärfe");
        add("Frame Rate Limit", "FPS-Grenze");
        add("Window Mode", "Fenstermodus");
        add("Explained", "Kurz erklärt");
        add("Performance Mode", "Performance-Modus");
        add("A lightweight renderer made for low-end PCs and integrated graphics. Much less GPU load than DirectX 11/12, simpler visuals. Change it in Fortnite: Settings > Video > Advanced Graphics > Rendering Mode > Performance (restart required). ConstiBoost does not switch the rendering mode for you.",
            "Eine leichte Grafik-Variante für schwache PCs und eingebaute Grafik. Viel weniger GPU-Last als DirectX 11/12, dafür einfacheres Bild. So stellst du es in Fortnite um: Einstellungen > Video > Erweiterte Grafik > Rendermodus > Performance (Neustart nötig). ConstiBoost stellt den Rendermodus nicht für dich um.");
        add("Synchronizes frames with your monitor to avoid tearing, but adds input lag. If FPS fall below the refresh rate, VSync can halve them. Recommended: OFF, plus an FPS cap for even frame pacing.",
            "Passt die Bilder an deinen Monitor an, damit das Bild nicht reißt – macht die Eingabe aber träger. Fallen die FPS unter die Bildwiederholrate, kann VSync sie halbieren. Empfohlen: AUS, dazu eine FPS-Grenze für gleichmäßige Bilder.");
        add("Nanite & Lumen", "Nanite & Lumen");
        add("Unreal Engine 5 features for detailed geometry (Nanite) and realistic lighting (Lumen). Very demanding – only available in DirectX 12. They are off in Performance Mode and with Global Illumination/Reflections on Low. In DirectX 12 set 'Nanite Virtualized Geometry' to Off in-game.",
            "Funktionen der Unreal Engine 5 für sehr feine Formen (Nanite) und echtes Licht (Lumen). Sehr anspruchsvoll – nur mit DirectX 12. Im Performance-Modus und mit Globaler Beleuchtung/Reflexionen auf Niedrig sind sie aus. In DirectX 12 im Spiel 'Nanite Virtualized Geometry' auf Aus stellen.");
        add("PROCESS PRIORITY (OPTIONAL)", "PROZESS-PRIORITÄT (OPTIONAL)");
        add("Sets Fortnite to 'Above normal' (never High/Realtime). Resets automatically when Fortnite closes. Fortnite's anti-cheat may block the change – that is expected and safe.",
            "Setzt Fortnite auf 'Höher als normal' (nie Hoch/Echtzeit). Wird von selbst zurückgesetzt, wenn Fortnite zugeht. Fortnites Anti-Cheat kann das blockieren – das ist normal und sicher.");
        add("SET ABOVE NORMAL", "HÖHER ALS NORMAL SETZEN");
        add("RESET", "ZURÜCKSETZEN");
        add("SHADER CACHE", "SHADER-CACHE");
        add("DirectX and GPU driver caches used by Fortnite. Clearing helps after driver updates or permanent stutter; the first match afterwards compiles shaders again.",
            "Zwischenspeicher von DirectX und GPU-Treiber, die Fortnite nutzt. Leeren hilft nach Treiber-Updates oder bei ständigen Rucklern. Die erste Runde danach baut die Shader neu auf.");
        add("CLEAR SHADER CACHE", "SHADER-CACHE LEEREN");
        add("FIND APPS", "APPS SUCHEN");
        add("CLOSE SELECTED", "AUSGEWÄHLTE SCHLIESSEN");
        add("CLOSE BACKGROUND APPS", "HINTERGRUND-APPS SCHLIESSEN");
        add("Free RAM and CPU before a match. Windows, drivers, security and anti-cheat are never listed.",
            "Mach vor einer Runde RAM und CPU frei. Windows, Treiber, Sicherheits-Programme und Anti-Cheat werden nie angezeigt.");

        // ---------- FpsBoostView ----------
        add("CHECK AGAIN", "NOCHMAL PRÜFEN");
        add("⚡ APPLY ALL RECOMMENDED", "⚡ ALLE EMPFOHLENEN ANWENDEN");
        add("⚡ FPS Boost", "⚡ FPS-Boost");
        add("20 real FPS features – each one checks your PC first, explains honestly what it does and can be undone.",
            "20 echte FPS-Funktionen – jede prüft zuerst deinen PC, erklärt ehrlich, was sie macht, und lässt sich rückgängig machen.");
        add("Honest note: settings remove brakes – they cannot turn integrated graphics into a gaming GPU. The biggest gains usually come from: charger plugged in, current AMD driver, dual-channel RAM and fewer pixels. Use 🔍 ANALYZE and 📏 MEASURE in Games to see the real effect.",
            "Ehrlich gesagt: Einstellungen lösen Bremsen – aus eingebauter Grafik machen sie keine Gaming-GPU. Am meisten bringt meist: Ladekabel dran, aktueller AMD-Treiber, Dual-Channel-RAM (zwei RAM-Riegel) und weniger Pixel. Nutze 🔍 ANALYSIEREN und 📏 MESSEN unter Spiele, um die echte Wirkung zu sehen.");
        add("→ Games: Analyze / Measure / Focus", "→ Spiele: Analysieren / Messen / Fokus");
        add("Downside: {0}", "Nachteil: {0}");
        add("FPS IMPACT", "FPS-WIRKUNG");
        add("APPLY", "ANWENDEN");
        add("↩ UNDO", "↩ RÜCKGÄNGIG");

        // ---------- BoostLabView ----------
        add("ANALYZE AGAIN", "NOCHMAL ANALYSIEREN");
        add("🧪 Boost Lab", "🧪 Boost-Labor");
        add("Your PC can do more than you think. Find what really limits your FPS – and apply every safe low-end tweak in one place.",
            "Dein PC kann mehr, als du denkst. Finde heraus, was deine FPS wirklich bremst – und wende alle sicheren Tipps für schwache PCs an einem Ort an.");
        add("→ FPS estimates & Game Focus in Games", "→ FPS-Schätzung & Spiel-Fokus unter Spiele");
        add("→ Heat check in Thermal", "→ Wärme-Check unter Temperatur");
        add("Hardware insights", "Hardware-Einblick");
        add("Real configuration details that matter most for low-end gaming. ConstiBoost only explains – it never changes BIOS or hardware settings.",
            "Echte Details deines PCs, die fürs Spielen auf schwachen PCs am wichtigsten sind. ConstiBoost erklärt nur – es ändert nie BIOS- oder Hardware-Einstellungen.");
        add("📸 CREATE PC REPORT", "📸 PC-BERICHT ERSTELLEN");
        add("🛒 Upgrade advisor", "🛒 Aufrüst-Berater");
        add("Based on the slots, modules and drives Windows reports – only upgrades that make sense for this PC.",
            "Beruht auf den Steckplätzen, RAM-Riegeln und Laufwerken, die Windows meldet – nur Aufrüstungen, die für diesen PC Sinn machen.");
        add("Effect: {0}", "Wirkung: {0}");
        add("⚡ APPLY BOOST PACK", "⚡ BOOST-PAKET ANWENDEN");
        add("Low-End Boost Pack", "Boost-Paket für schwache PCs");
        add("Every safe Windows setting that frees CPU/GPU time on weak PCs. Each change is backed up and can be restored.",
            "Alle sicheren Windows-Einstellungen, die auf schwachen PCs CPU/GPU-Zeit freimachen. Jede Änderung wird gesichert und kann wiederhergestellt werden.");
    }
}
