using System;

namespace ConstiBoost.Localization;

/// <summary>Games, Game Detail, Fortnite, Gaming Mode and Thermal view models. Simple, everyday German ("du").</summary>
internal static class DeGamesVm
{
    public static void Register(Action<string, string> add)
    {
        // ---------- Games list ----------
        add("Size", "Größe");
        add("Last played", "Zuletzt gespielt");
        add("No games found yet.", "Noch keine Spiele gefunden.");
        add("{0} games · {1} on disk · Steam {2} · Epic {3} · Store/Xbox {4} · other {5}", "{0} Spiele · {1} auf der Festplatte · Steam {2} · Epic {3} · Store/Xbox {4} · andere {5}");
        add("🎯 Focus active: {0} (since {1:HH:mm})", "🎯 Fokus aktiv: {0} (seit {1:HH:mm})");
        add("The game library couldn't be read completely. Details were written to the log.", "Die Spiele-Liste konnte nicht ganz gelesen werden. Details stehen im Protokoll.");

        // ---------- Game detail: general ----------
        add("{0}×{1} (native)", "{0}×{1} (nativ)");
        add("{0}×{1} (in-game)", "{0}×{1} (im Spiel)");
        add("Copied: {0} – paste it into the browser's address bar.", "Kopiert: {0} – füge es oben in die Adresszeile vom Browser ein.");
        add("Clipboard busy – type the address manually: {0}", "Zwischenablage ist belegt – tippe die Adresse selbst ein: {0}");

        // Bottleneck check
        add("No clear limit found – run it again while you actively play.", "Kein klarer Engpass gefunden – starte es nochmal, während du wirklich spielst.");
        add("Analysis failed: {0}", "Analyse hat nicht geklappt: {0}");

        // Browser performance launch
        add("Full window (maximized) – most pixels, lowest FPS.", "Ganzes Fenster (maximiert) – die meisten Pixel, die wenigsten FPS.");
        add("Window {0} % of the screen → ~{1:0} % of the pixels to render. Press F11 in the window for borderless full screen only if you want 100 % again.",
            "Fenster {0} % vom Bildschirm → nur ~{1:0} % der Pixel müssen berechnet werden. Drück F11 im Fenster für Vollbild nur, wenn du wieder 100 % willst.");
        add("Please enter the web address of the game first (e.g. https://…).", "Gib zuerst die Web-Adresse vom Spiel ein (z. B. https://…).");
        add("GPU start flags (GPU block list off, GPU rasterization, high-performance GPU, render size) only work when the browser starts.",
            "Die GPU-Start-Schalter (GPU-Sperrliste aus, GPU-Rasterung, starke GPU, Bildgröße) wirken nur, wenn der Browser neu startet.");
        add("ConstiBoost closes the browser windows normally (like clicking X – nothing is force-killed).", "ConstiBoost schließt die Browser-Fenster ganz normal (wie ein Klick auf X – nichts wird hart beendet).");
        add("The browser then reopens with your previous tabs (--restore-last-session) and the game in a new window.", "Danach öffnet sich der Browser wieder mit deinen alten Tabs (--restore-last-session) und das Spiel in einem neuen Fenster.");
        add("Save anything you are typing in the browser first.", "Speichere vorher alles, was du gerade im Browser schreibst.");
        add("Choose 'Cancel' to just open the game in the running browser without the flags.", "Wähle „Abbrechen“, um das Spiel einfach im offenen Browser ohne die Schalter zu öffnen.");
        add("Restart {0} for full GPU speed?", "{0} neu starten für volle GPU-Leistung?");
        add("The browser is running, so the speed flags cannot be applied right now.", "Der Browser läuft schon. Deshalb können die Schalter für mehr Tempo gerade nicht wirken.");
        add("RESTART BROWSER", "BROWSER NEU STARTEN");
        add("{0} is still running in the background (browser settings → System → 'Continue running background apps' OFF helps). The game was opened without the start flags.",
            "{0} läuft noch im Hintergrund (Browser-Einstellungen → System → „Apps im Hintergrund weiter ausführen“ AUS hilft). Das Spiel wurde ohne die Start-Schalter geöffnet.");

        // Real FPS measurement
        add("Measure again after a change to see Before / After.", "Miss nach einer Änderung nochmal, dann siehst du Vorher / Nachher.");
        add("No measurable difference (within normal variation).", "Kein messbarer Unterschied (normale Schwankung).");
        add("Faster than before.", "Schneller als vorher.");
        add("Slower than before.", "Langsamer als vorher.");
        add("Before ({0}, {1}, {2}): {3:0} FPS · 1 % low {4:0}", "Vorher ({0}, {1}, {2}): {3:0} FPS · 1 % Tiefstwert {4:0}");
        add("After ({0}, {1}, {2}): {3:0} FPS · 1 % low {4:0}", "Nachher ({0}, {1}, {2}): {3:0} FPS · 1 % Tiefstwert {4:0}");
        add("Difference: {0:+0;-0;0} FPS ({1:+0.0;-0.0;0} %) – {2}", "Unterschied: {0:+0;-0;0} FPS ({1:+0.0;-0.0;0} %) – {2}");
        add("Measure real FPS in {0}?", "Echte FPS in {0} messen?");
        add("ConstiBoost records the frames Windows reports for the game (DirectX present events – no injection, anti-cheat safe). Windows only allows this with administrator rights, so a UAC prompt appears once.\n\nStart the game, then play normally for {0} seconds – measuring starts automatically as soon as the game renders.",
            "ConstiBoost zählt die Bilder, die Windows für das Spiel meldet (DirectX-Ereignisse – nichts wird ins Spiel eingeschleust, sicher für Anti-Cheat). Windows erlaubt das nur mit Admin-Rechten, deshalb kommt einmal eine Windows-Abfrage (UAC).\n\nStarte das Spiel und spiel {0} Sekunden ganz normal – die Messung startet von selbst, sobald das Spiel Bilder zeigt.");
        add("Tip: measure the same situation each time (e.g. a Creative map or the same match phase) for a fair Before / After.", "Tipp: Miss jedes Mal die gleiche Situation (z. B. eine Kreativ-Karte oder die gleiche Spielphase), damit Vorher / Nachher fair ist.");
        add("START MEASURING", "MESSUNG STARTEN");
        add("Measurement not possible.", "Messen ist nicht möglich.");
        add("Measured {0} for {1:0} s: {2}", "{0} für {1:0} s gemessen: {2}");
        add("Measurement cancelled.", "Messung abgebrochen.");

        // Game settings editor
        add("Close the game first", "Schließ zuerst das Spiel");
        add("{0} saves its settings when it exits and would overwrite the change. Close it completely, then apply again.", "{0} speichert seine Einstellungen beim Beenden und würde die Änderung überschreiben. Schließ es ganz und wende es dann nochmal an.");
        add("Apply {0} to {1}?", "{0} für {1} anwenden?");
        add("The settings file is backed up first (Backups → RESTORE).", "Die Einstellungsdatei wird vorher gesichert (Sicherungen → WIEDERHERSTELLEN).");

        // Guided FPS tuner
        add("Step 1: press MEASURE REAL FPS and play a normal match for 60 s. The tuner then tells you exactly what to change next.",
            "Schritt 1: Drück ECHTE FPS MESSEN und spiel 60 s ein normales Match. Danach sagt dir der Tuner genau, was du als Nächstes ändern sollst.");
        add("Fortnite's settings file was not found – start Fortnite once.", "Die Einstellungsdatei von Fortnite wurde nicht gefunden – starte Fortnite einmal.");
        add("Last measurement ({0}): {1:0} FPS, 1 % low {2:0}. Target: {3} FPS.", "Letzte Messung ({0}): {1:0} FPS, 1 % Tiefstwert {2:0}. Ziel: {3} FPS.");
        add("Next step: switch Rendering Mode to PERFORMANCE in Fortnite (Settings > Video > Advanced Graphics) and restart the game – the biggest single gain on integrated graphics. Then measure again.",
            "Nächster Schritt: Stell in Fortnite den Rendering-Modus auf PERFORMANCE (Einstellungen > Video > Erweiterte Grafik) und starte das Spiel neu – das bringt bei eingebauter Grafik am meisten. Dann miss nochmal.");
        add("Next step: set all graphics groups to Low (MAX FPS preset). Then play and measure again.", "Nächster Schritt: Stell alle Grafik-Bereiche auf Niedrig (Vorgabe MAX FPS). Dann spiel und miss nochmal.");
        add("all graphics Low", "alle Grafik auf Niedrig");
        add("Next step: lower 3D resolution from {0:0} % to {1} % (sharper image loss is small, GPU load drops a lot). Then measure again.",
            "Nächster Schritt: Senk die 3D-Auflösung von {0:0} % auf {1} % (das Bild wird nur etwas weicher, die GPU hat viel weniger Arbeit). Dann miss nochmal.");
        add("3D resolution {0} %", "3D-Auflösung {0} %");
        add("Your PC is at its limit for this target with these settings. Options: lower the target, use 1280×720 in-game, close background apps (Game Focus), keep the laptop plugged in and check the Thermal page for heat throttling.",
            "Dein PC schafft dieses Ziel mit diesen Einstellungen nicht mehr. Was du tun kannst: Ziel senken, im Spiel 1280×720 nehmen, Hintergrund-Apps schließen (Spiel-Fokus), den Laptop am Ladekabel lassen und auf der Seite Temperatur nach Drosselung durch Hitze schauen.");
        add("You have headroom ✓ – you can raise 3D resolution to {0} % for a sharper image, or cap FPS at {1} for cooler, smoother play.",
            "Du hast Luft nach oben ✓ – du kannst die 3D-Auflösung für ein schärferes Bild auf {0} % erhöhen oder die FPS auf {1} begrenzen, dann bleibt alles kühler und läuft gleichmäßiger.");
        add("Target reached ✓. Tip: cap FPS at {0} – more even frame times and less heat.", "Ziel erreicht ✓. Tipp: Begrenz die FPS auf {0} – gleichmäßigere Bilder und weniger Hitze.");
        add("FPS limit {0}", "FPS-Grenze {0}");
        add("APPLY: {0}", "ANWENDEN: {0}");
        add("Apply tuner step?", "Tuner-Schritt anwenden?");
        add("Fortnite must be closed. GameUserSettings.ini is backed up first.", "Fortnite muss geschlossen sein. GameUserSettings.ini wird vorher gesichert.");
        add("Afterwards: start Fortnite, play the same situation and press MEASURE REAL FPS again.", "Danach: Starte Fortnite, spiel die gleiche Situation und drück nochmal ECHTE FPS MESSEN.");

        // Estimates
        add("No FPS estimate data for this game yet.", "Für dieses Spiel gibt es noch keine FPS-Schätzung.");
        add("Your graphics chip is not in the estimate database – no FPS estimate shown.", "Dein Grafikchip ist nicht in der Schätz-Datenbank – deshalb keine FPS-Schätzung.");
        add("Estimated FPS for {0} on this PC", "Geschätzte FPS für {0} auf diesem PC");
        add("Lower resolution, shadows and effects first – they cost the most on integrated graphics.", "Senk zuerst Auflösung, Schatten und Effekte – die kosten bei eingebauter Grafik am meisten.");
        add("Fortnite settings file not found – start Fortnite once.", "Fortnite-Einstellungsdatei nicht gefunden – starte Fortnite einmal.");
        add("Your current Fortnite settings: {0} · ~{1} · {2} · 3D resolution {3} %", "Deine aktuellen Fortnite-Einstellungen: {0} · ~{1} · {2} · 3D-Auflösung {3} %");
        add("✓ Calibrated with your real measurement from {0} ({1:0} FPS at {2}) – correction ×{3:0.00}.", "✓ Angepasst an deine echte Messung vom {0} ({1:0} FPS bei {2}) – Korrektur ×{3:0.00}.");

        // Preset / setting names (display only)
        add("Low", "Niedrig");
        add("Medium", "Mittel");
        add("High", "Hoch");
        add("Unknown", "Unbekannt");
        add("Normal", "Normal");
        add("Normal (no Gaming Mode)", "Normal (ohne Gaming-Modus)");
        add("Performance Mode · Low", "Performance-Modus · Niedrig");
        add("DirectX 12 · High", "DirectX 12 · Hoch");
        add("DirectX 11 · Medium", "DirectX 11 · Mittel");
        add("DirectX 11 · Low", "DirectX 11 · Niedrig");
        add("BALANCED", "AUSGEWOGEN");
        add("COOL", "KÜHL");

        // Auto-Focus / Game Focus
        add("Auto-Focus for {0} is on: when the game starts, ConstiBoost applies these options automatically (ConstiBoost must be running, e.g. in the tray).",
            "Auto-Fokus für {0} ist an: Wenn das Spiel startet, stellt ConstiBoost diese Optionen von selbst ein (ConstiBoost muss laufen, z. B. unten im Infobereich).");
        add("Auto-Focus for {0} is off.", "Auto-Fokus für {0} ist aus.");
        add("STOP FOCUS", "FOKUS BEENDEN");
        add("🎯 FOCUS ON THIS GAME", "🎯 FOKUS AUF DIESES SPIEL");
        add("Game Focus stopped – everything restored.", "Spiel-Fokus beendet – alles ist wieder wie vorher.");
        add("Game Focus stopped with notes: {0}", "Spiel-Fokus beendet, mit Hinweisen: {0}");
        add("Gaming Mode profile: {0} (Game Mode ON, power settings for the profile)", "Gaming-Modus-Profil: {0} (Windows-Spielmodus AN, Energie-Einstellungen für das Profil)");
        add("Close {0}", "{0} schließen");
        add("Lower priority of browsers/launchers still running", "Browser/Launcher, die noch laufen, bekommen weniger Vorrang");
        add("{0}: priority 'Above normal' while it runs (anti-cheat may block this – harmless)", "{0}: Vorrang „Höher als normal“, solange es läuft (Anti-Cheat kann das blockieren – harmlos)");
        add("Windows graphics preference 'High performance' for {0}", "Windows-Grafikeinstellung „Hohe Leistung“ für {0}");
        add("No Windows efficiency mode (EcoQoS) for the game processes", "Kein Windows-Sparmodus (EcoQoS) für das Spiel");
        add("Other browser tabs: priority 'Below normal' (extensions untouched)", "Andere Browser-Tabs: Vorrang „Niedriger als normal“ (Erweiterungen bleiben, wie sie sind)");
        add("Pause Windows Search indexing (UAC prompt; restarted on Stop)", "Windows-Suche (Indizierung) pausieren (Windows-Abfrage; startet beim Beenden wieder)");
        add("Browser: priority 'Above normal' only for the browser GPU process and the game tab (busiest tab) – other tabs stay normal",
            "Browser: Vorrang „Höher als normal“ nur für den GPU-Teil vom Browser und den Spiel-Tab (der Tab mit der meisten Arbeit) – andere Tabs bleiben normal");
        add("Mute Windows notification banners", "Windows-Benachrichtigungen stumm schalten");
        add("Fortnite frame rate limit {0} FPS (only while Fortnite is closed)", "Fortnite-FPS-Grenze {0} FPS (geht nur, wenn Fortnite geschlossen ist)");
        add("Everything is restored when you press STOP FOCUS.", "Alles wird zurückgestellt, wenn du FOKUS BEENDEN drückst.");
        add("Focus on {0}?", "Fokus auf {0}?");
        add("ConstiBoost puts all safe system resources behind this game:", "ConstiBoost gibt diesem Spiel alles, was sicher geht:");
        add("START FOCUS", "FOKUS STARTEN");
        add("🎯 Focus on {0} active. Have fun!", "🎯 Fokus auf {0} ist aktiv. Viel Spaß!");
        add("Focus active with notes: {0}", "Fokus aktiv, mit Hinweisen: {0}");

        // Uninstall
        add("Uninstall {0}?", "{0} deinstallieren?");
        add("ConstiBoost starts the official uninstaller (Steam, Epic or the game's own uninstaller). Game files are never deleted by ConstiBoost itself. Cloud saves are usually kept, local saves may be removed by the uninstaller.",
            "ConstiBoost startet die offizielle Deinstallation (Steam, Epic oder die vom Spiel selbst). ConstiBoost löscht selbst nie Spieldateien. Spielstände in der Cloud bleiben meistens, Spielstände auf dem PC kann die Deinstallation löschen.");
        add("UNINSTALL", "DEINSTALLIEREN");

        // ---------- Fortnite ----------
        add("Fortnite is not running", "Fortnite läuft nicht");
        add("Not found", "Nicht gefunden");
        add("Fortnite was not found in the Epic Games Launcher manifests or common install folders.", "Fortnite wurde weder im Epic Games Launcher noch in den üblichen Installations-Ordnern gefunden.");
        add("Detected via {0}", "Gefunden über {0}");
        add("Settings file found", "Einstellungsdatei gefunden");
        add("Settings file not found – start Fortnite once so it creates its settings.", "Einstellungsdatei nicht gefunden – starte Fortnite einmal, dann legt es sie an.");
        add("Not in file (set in-game)", "Nicht in der Datei (im Spiel einstellen)");
        add("Unlimited", "Unbegrenzt");
        add("Fullscreen", "Vollbild");
        add("Windowed Fullscreen", "Vollbild im Fenster");
        add("Windowed", "Fenster");
        add("Sharpest image, highest GPU load.", "Schärfstes Bild, meiste Arbeit für die GPU.");
        add("Slightly softer image, noticeably less GPU load on integrated graphics.", "Etwas weicheres Bild, bei eingebauter Grafik spürbar weniger Arbeit für die GPU.");
        add("Visibly softer image, much less GPU load – good for weak iGPUs.", "Sichtbar weicheres Bild, viel weniger Arbeit für die GPU – gut für schwache eingebaute Grafik.");
        add("Blurry image, lowest GPU load. Only if nothing else helps.", "Unscharfes Bild, am wenigsten Arbeit für die GPU. Nur, wenn sonst nichts hilft.");
        add("Low / Off", "Niedrig / Aus");
        add("Cinematic", "Kino");
        add("Available on your NVIDIA GPU: set 'NVIDIA Reflex Low Latency' to On in Fortnite (Settings > Video > Advanced Graphics). Reflex is only available in DirectX 11/12 modes.",
            "Geht mit deiner NVIDIA-GPU: Stell in Fortnite „NVIDIA Reflex Low Latency“ auf An (Einstellungen > Video > Erweiterte Grafik). Reflex gibt es nur in den Modi DirectX 11/12.");
        add("Unavailable on this system – NVIDIA Reflex requires a supported NVIDIA graphics card. ConstiBoost does not enable it.",
            "Auf deinem PC nicht verfügbar – NVIDIA Reflex braucht eine passende NVIDIA-Grafikkarte. ConstiBoost schaltet es nicht ein.");
        add("{0}: use PERFORMANCE MODE with LOW settings, 3D resolution 70–85 % (depending on how sharp you want it), VSync OFF, Motion Blur OFF, Shadows OFF and an FPS cap at your display's {1} Hz for stable frame times and lower temperatures.",
            "{0}: Nimm den PERFORMANCE-MODUS mit NIEDRIGEN Einstellungen, 3D-Auflösung 70–85 % (je nachdem, wie scharf du es willst), VSync AUS, Bewegungsunschärfe AUS, Schatten AUS und eine FPS-Grenze bei {1} Hz (wie dein Bildschirm) – für gleichmäßige Bilder und weniger Hitze.");
        add("Dedicated GPU detected: BALANCED or QUALITY usually give the best mix. Use MAX FPS for competitive play.", "Eigene Grafikkarte erkannt: BALANCED oder QUALITY sind meist die beste Mischung. Für Wettkampf nimm MAX FPS.");
        add("Use MAX FPS or BALANCED depending on how smooth the game runs.", "Nimm MAX FPS oder BALANCED, je nachdem, wie flüssig das Spiel läuft.");
        add("Close Fortnite first", "Schließ zuerst Fortnite");
        add("Fortnite overwrites its settings file when it exits. Close Fortnite completely, then apply the preset again.", "Fortnite überschreibt beim Beenden seine Einstellungsdatei. Schließ Fortnite ganz und wende die Vorgabe dann nochmal an.");
        add("3D Resolution: {0} %", "3D-Auflösung: {0} %");
        add("Frame rate limit: {0} FPS", "FPS-Grenze: {0} FPS");
        add("Window mode: Fullscreen", "Fenstermodus: Vollbild");
        add("Estimated impact: {0} (no FPS promise – depends on map, scene and hardware)", "Geschätzte Wirkung: {0} (kein FPS-Versprechen – hängt von Karte, Szene und Hardware ab)");
        add("GameUserSettings.ini is backed up first.", "GameUserSettings.ini wird vorher gesichert.");
        add("Apply {0}?", "{0} anwenden?");
        add("ConstiBoost will update Fortnite's own settings file:", "ConstiBoost ändert die Einstellungsdatei von Fortnite:");
        add("APPLY PRESET", "VORGABE ANWENDEN");
        add("One step in Fortnite", "Ein Schritt in Fortnite");
        add("ConstiBoost does not change Fortnite's rendering mode itself. In Fortnite open Settings > Video > Advanced Graphics and set 'Rendering Mode' to PERFORMANCE, then restart Fortnite.",
            "ConstiBoost ändert den Rendering-Modus von Fortnite nicht selbst. Öffne in Fortnite Einstellungen > Video > Erweiterte Grafik, stell „Rendering-Modus“ auf PERFORMANCE und starte Fortnite neu.");
        add("Not in your settings file (set in-game): {0}", "Nicht in deiner Einstellungsdatei (im Spiel einstellen): {0}");
        add("Running · PID {0} · Priority {1} · RAM {2}", "Läuft · PID {0} · Vorrang {1} · RAM {2}");
        add("protected by anti-cheat", "durch Anti-Cheat geschützt");
        add(" · changed by ConstiBoost", " · von ConstiBoost geändert");
        add("Shader caches are in use while Fortnite is running.", "Solange Fortnite läuft, werden die Shader-Caches benutzt.");
        add("Clear shader caches?", "Shader-Caches leeren?");
        add("DirectX and GPU driver shader caches will be deleted. They are rebuilt automatically – the first match afterwards can stutter while shaders compile again. Recommended after GPU driver updates or permanent stutter.",
            "Die Shader-Caches von DirectX und vom Grafik-Treiber werden gelöscht. Sie bauen sich von selbst neu auf – das erste Match danach kann kurz Ruckler haben. Sinnvoll nach einem neuen Grafik-Treiber oder bei ständigen Rucklern.");
        add("CLEAR", "LEEREN");
        add("Shader caches cleared: {0} freed.", "Shader-Caches geleert: {0} frei geworden.");
        add("Close apps?", "Apps schließen?");
        add("These apps will be closed. Unsaved work in normal apps is protected – they are only asked to close.", "Diese Apps werden geschlossen. Nicht gespeicherte Arbeit ist geschützt – normale Apps werden nur gebeten, sich zu schließen.");
        add("CLOSE", "SCHLIESSEN");
        add("{0} app(s) closed.", "{0} App(s) geschlossen.");
        add("Fortnite couldn't be started via the Epic Games Launcher: {0}", "Fortnite konnte nicht über den Epic Games Launcher gestartet werden: {0}");

        // ---------- Gaming Mode ----------
        add("MAX FPS – maximum performance: 'Best performance' power mode. Higher temperature possible.", "MAX FPS – volle Leistung: Energiemodus „Beste Leistung“. Der PC kann wärmer werden.");
        add("BALANCED – good FPS with less sustained heat: 'Balanced' power mode, CPU boost stays on. Optional FPS cap.", "AUSGEWOGEN – gute FPS mit weniger Dauer-Hitze: Energiemodus „Ausbalanciert“, CPU-Boost bleibt an. FPS-Grenze nach Wunsch.");
        add("COOL – lower heat, somewhat less performance: 'Balanced' power mode + maximum processor state 99 % (turns off CPU boost; can noticeably lower FPS in CPU-heavy scenes). Thermal protection stays fully active.",
            "KÜHL – weniger Hitze, etwas weniger Leistung: Energiemodus „Ausbalanciert“ + höchster Prozessorzustand 99 % (schaltet den CPU-Boost aus; kann in CPU-lastigen Szenen spürbar FPS kosten). Der Hitzeschutz bleibt voll aktiv.");
        add("Profile: {0}", "Profil: {0}");
        add("Gaming Mode ACTIVE since {0:HH:mm}", "Gaming-Modus AKTIV seit {0:HH:mm}");
        add("Gaming Mode is off", "Gaming-Modus ist aus");
        add("STOP GAMING MODE", "GAMING-MODUS BEENDEN");
        add("START GAMING MODE", "GAMING-MODUS STARTEN");
        add("Gaming Mode stopped – settings restored.", "Gaming-Modus beendet – Einstellungen sind wieder wie vorher.");
        add("Gaming Mode stopped. Some items couldn't be restored: {0}", "Gaming-Modus beendet. Einiges konnte nicht zurückgestellt werden: {0}");
        add("Turn on Windows Game Mode", "Windows-Spielmodus einschalten");
        add("Power mode: Best performance (if available)", "Energiemodus: Beste Leistung (wenn vorhanden)");
        add("Power mode: Balanced", "Energiemodus: Ausbalanciert");
        add("Maximum processor state 99 % (CPU boost off) – may require administrator permission", "Höchster Prozessorzustand 99 % (CPU-Boost aus) – braucht eventuell Admin-Rechte");
        add("Fortnite frame rate limit: {0} FPS (only if Fortnite is closed; previous value is restored)", "Fortnite-FPS-Grenze: {0} FPS (nur wenn Fortnite geschlossen ist; der alte Wert kommt später zurück)");
        add("Lower priority of browsers/launchers still running (Below normal)", "Browser/Launcher, die noch laufen, bekommen weniger Vorrang (Niedriger als normal)");
        add("Everything is restored when you stop Gaming Mode.", "Alles wird zurückgestellt, wenn du den Gaming-Modus beendest.");
        add("Start Gaming Mode?", "Gaming-Modus starten?");
        add("ConstiBoost will make these temporary changes:", "ConstiBoost macht diese Änderungen für eine Weile:");
        add("START", "STARTEN");
        add("Gaming Mode ACTIVE. Have fun!", "Gaming-Modus AKTIV. Viel Spaß!");
        add("Gaming Mode active with notes: {0}", "Gaming-Modus aktiv, mit Hinweisen: {0}");

        // ---------- Thermal ----------
        add("Fan speed unavailable on this device.", "Lüfter-Drehzahl ist auf diesem Gerät nicht verfügbar.");
        add("Enable Auto-Cool?", "Auto-Kühlen einschalten?");
        add("When the temperature stays above the trigger for 60 seconds, ConstiBoost switches to the Balanced power mode (and optionally turns off CPU boost). After 2 minutes of lower temperature the previous settings are restored. Fans, voltages and thermal protection are never touched. Thermal Monitoring runs in the background while Auto-Cool is on.",
            "Wenn die Temperatur 60 Sekunden lang über der Grenze bleibt, schaltet ConstiBoost auf den Energiemodus „Ausbalanciert“ (und auf Wunsch den CPU-Boost aus). Ist es 2 Minuten lang wieder kühler, kommen die alten Einstellungen zurück. Lüfter, Spannungen und der Hitzeschutz werden nie angefasst. Solange Auto-Kühlen an ist, läuft die Temperatur-Überwachung im Hintergrund.");
        add("ENABLE", "EINSCHALTEN");
        add("❄️ Auto-Cool ENGAGED since {0:HH:mm}", "❄️ Auto-Kühlen AKTIV seit {0:HH:mm}");
        add("Auto-Cool armed – watching the temperature", "Auto-Kühlen bereit – passt auf die Temperatur auf");
        add("Auto-Cool off", "Auto-Kühlen aus");
        add("🔴 CRITICAL TEMPERATURE", "🔴 KRITISCHE TEMPERATUR");
        add("🟠 HIGH TEMPERATURE", "🟠 HOHE TEMPERATUR");
        add("The system may be under excessive thermal load. Consider stopping the workload and checking cooling.", "Dein PC ist vielleicht zu heiß. Beende am besten, was gerade läuft, und prüf die Kühlung.");
        add("Your CPU temperature is currently high. Check CPU load and cooling conditions.", "Deine CPU ist gerade heiß. Schau, wie stark die CPU arbeitet und ob die Kühlung gut ist.");
        add("{0} detected.", "{0} erkannt.");
        add("{0} MHz (reported by Windows)", "{0} MHz (von Windows gemeldet)");
        add("now {0} · avg {1} · max {2}", "jetzt {0} · Schnitt {1} · max. {2}");
        add("{0:0.00} GHz avg", "{0:0.00} GHz im Schnitt");
        add("{0} · Temp {1}", "{0} · Temp. {1}");
        add("Dedicated {0} · Shared {1}", "Eigener Speicher {0} · Geteilt {1}");
        add("{0} used · {1} free · {2} total", "{0} belegt · {1} frei · {2} gesamt");
        add(" (partial)", " (teilweise)");
        add("Fan active ({0})", "Lüfter läuft ({0})");
        add("No safe software measures found – the current load and settings already look reasonable.", "Nichts Sicheres zum Verbessern gefunden – Last und Einstellungen sehen schon gut aus.");
        add("We found {0} potentially useful optimization(s) and {1} piece(s) of advice.", "Wir haben {0} mögliche Verbesserung(en) und {1} Tipp(s) gefunden.");
        add("Temperature measured {0:0.0} °C lower than before. Workload and room temperature also influence this value.", "Die Temperatur ist {0:0.0} °C niedriger als vorher. Auch was gerade läuft und die Raumtemperatur spielen mit.");
        add("No measurable temperature improvement detected.", "Keine messbare Verbesserung der Temperatur.");
        add(" The temperature was higher during the second measurement – the workload may have changed.", " Bei der zweiten Messung war es wärmer – vielleicht lief da mehr.");
        add("Thermal sensor unavailable – only CPU load, RAM and background processes can be compared.", "Kein Temperatur-Sensor verfügbar – nur CPU-Last, RAM und Hintergrund-Prozesse lassen sich vergleichen.");
        add("Thermal sensor unavailable.", "Temperatur-Sensor nicht verfügbar.");
        add(" · game running", " · Spiel läuft");
        add("FPS: unavailable – no supported FPS source (ConstiBoost does not hook into the game).", "FPS: nicht verfügbar – keine passende FPS-Quelle (ConstiBoost greift nicht ins Spiel ein).");
        add("Background load: run SCAN MY PC once to see what else uses CPU while you play.", "Hintergrund-Last: Starte einmal PC SCANNEN, dann siehst du, was beim Spielen sonst noch die CPU nutzt.");
        add("Background load: other apps use about {0:0} % CPU – closing them leaves more headroom for Fortnite and may reduce heat.", "Hintergrund-Last: Andere Apps brauchen etwa {0:0} % CPU – wenn du sie schließt, hat Fortnite mehr Luft und es wird vielleicht kühler.");
        add("Background load: low – good.", "Hintergrund-Last: niedrig – gut.");
        add("Graphics load: the GPU is at its limit. A lower 3D resolution or an FPS cap (e.g. 60) reduces GPU heat; use Performance Mode on integrated graphics.",
            "Grafik-Last: Die GPU ist am Limit. Eine niedrigere 3D-Auflösung oder eine FPS-Grenze (z. B. 60) macht die GPU kühler; bei eingebauter Grafik nimm den Performance-Modus.");
        add("Graphics load: the CPU is the limit. Lower view distance and an FPS cap reduce CPU work and heat.", "Grafik-Last: Die CPU bremst. Weniger Sichtweite und eine FPS-Grenze geben der CPU weniger Arbeit und weniger Hitze.");
        add("Graphics load: neither CPU nor GPU is fully loaded.", "Grafik-Last: Weder CPU noch GPU sind voll ausgelastet.");
        add("Power mode: maximum performance is active – highest FPS, but also the most heat. BALANCED in Cool Gaming Mode is a middle ground.", "Energiemodus: Volle Leistung ist an – die meisten FPS, aber auch die meiste Hitze. AUSGEWOGEN im Gaming-Modus ist ein guter Mittelweg.");
        add("Power mode: {0}.", "Energiemodus: {0}.");
        add("Cooling status: no temperature sensor readable.", "Kühlung: Kein Temperatur-Sensor lesbar.");
        add("Cooling status: {0} ({1:0} °C). High temperatures during gaming can be expected under load.", "Kühlung: {0} ({1:0} °C). Beim Spielen unter Last sind hohe Temperaturen normal.");
        add("Reading hardware…", "Hardware wird gelesen…");
        add("Scan complete – main cause: {0}.", "Scan fertig – Hauptgrund: {0}.");
        add("Scan cancelled.", "Scan abgebrochen.");
        add("The thermal scan couldn't be completed. Details were written to the log.", "Der Temperatur-Scan konnte nicht fertig werden. Details stehen im Protokoll.");
        add("{0} → {1}  (impact: {2})", "{0} → {1}  (Wirkung: {2})");
        add("Changed settings are backed up first. Closed apps can simply be opened again.", "Geänderte Einstellungen werden vorher gesichert. Geschlossene Apps kannst du einfach wieder öffnen.");
        add("Afterwards ConstiBoost measures again for 30 seconds and compares the real values.", "Danach misst ConstiBoost nochmal 30 Sekunden und vergleicht die echten Werte.");
        add("We found {0} potentially useful optimization(s).", "Wir haben {0} mögliche Verbesserung(en) gefunden.");
        add("Apply these safe measures?", "Diese sicheren Schritte anwenden?");
        add("OPTIMIZE TEMPERATURE", "TEMPERATUR VERBESSERN");
        add("Creating backup…", "Sicherung wird erstellt…");
        add("Applying: {0}…", "Wird angewendet: {0}…");
        add("Letting the system settle…", "Kurz warten, bis sich der PC beruhigt…");
        add("Measuring again… {0}/{1} s", "Neue Messung… {0}/{1} s");
        add("Measurement cancelled – changes were applied.", "Messung abgebrochen – die Änderungen wurden aber angewendet.");
        add("Before: Temperature {0} · CPU load {1}", "Vorher: Temperatur {0} · CPU-Last {1}");
        add("Test cancelled.", "Test abgebrochen.");
        add("n/a", "k. A.");
        add("{0}: {1} gives higher performance ({2:0} FPS measured).", "{0}: {1} bringt mehr Leistung ({2:0} FPS gemessen).");
        add("{0}: {1} runs cooler ({2:0} °C at {3:0} FPS). You decide.", "{0}: {1} bleibt kühler ({2:0} °C bei {3:0} FPS). Du entscheidest.");
        add("{0} gives higher performance potential (highest average CPU clock: {1:0.00} GHz).", "{0} hat mehr Leistung drin (höchster CPU-Takt im Schnitt: {1:0.00} GHz).");
        add("{0} runs cooler (lowest average temperature: {1:0} °C).", "{0} bleibt kühler (niedrigste Temperatur im Schnitt: {1:0} °C).");
        add("Only compare tests with a similar workload (e.g. the same game scene). You decide which balance you prefer.", "Vergleiche nur Tests mit ähnlicher Last (z. B. die gleiche Spielszene). Du entscheidest, was dir lieber ist.");
        add("Run the 60 second thermal test in different Gaming Mode profiles (e.g. during a Fortnite match) to compare performance and temperature with real measurements.",
            "Mach den 60-Sekunden-Temperaturtest mit verschiedenen Gaming-Modus-Profilen (z. B. in einem Fortnite-Match). Dann kannst du Leistung und Temperatur mit echten Messwerten vergleichen.");
    }
}
