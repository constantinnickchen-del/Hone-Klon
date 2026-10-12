# ⚡ ConstiBoost PRO 2.0

**Lokaler, transparenter und reversibler FPS- & Performance-Optimizer für Windows 10/11** – mit Fokus auf Fortnite und Low-End-Systeme mit integrierter Grafik (z. B. Acer Aspire 3 15 · Ryzen 5 5000 · Radeon Graphics).

ConstiBoost verspricht **keine** „+300 FPS“. Jede Änderung zeigt *aktuellen Zustand → Empfehlung → Grund → mögliche Nachteile*, wird vorher gesichert, geloggt und lässt sich wiederherstellen.

- C# · .NET 8 · WPF · MVVM (ohne externe UI-Frameworks)
- 100 % lokal: kein Account, keine Cloud, keine API-Keys, keine Telemetrie, keine Werbung
- Läuft als normaler Benutzer – Admin-Aktionen nur über eine einzelne UAC-Abfrage

---

## Installation

**Voraussetzungen:** Windows 10 (1809+) oder Windows 11, x64.

### Variante A – fertige EXE bauen (empfohlen)
```powershell
dotnet publish ConstiBoost -c Release -r win-x64 --self-contained true -o publish
.\publish\ConstiBoost.exe
```
`publish\ConstiBoost.exe` ist eine einzelne, selbstständige Datei (keine .NET-Installation nötig). Einfach irgendwohin kopieren und starten.

Kleine Variante (~2,6 MB, benötigt die [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0)):
```powershell
dotnet publish ConstiBoost -c Release -r win-x64 --self-contained false -o publish-small
```

### Variante B – aus dem Quellcode starten
Benötigt das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
```powershell
dotnet restore
dotnet build
dotnet run --project ConstiBoost
```
(Im Ordner `ConstiBoost\` reicht auch einfach `dotnet run`.)

## Build

| Befehl | Ergebnis |
|---|---|
| `dotnet restore` | lädt die 3 NuGet-Pakete (System.Management, PerformanceCounter, ServiceController) |
| `dotnet build` | Debug-Build nach `ConstiBoost\bin\Debug\net8.0-windows\` |
| `dotnet run --project ConstiBoost` | startet die App |
| `dotnet publish ConstiBoost -c Release -r win-x64 --self-contained true` | Single-File-EXE unter `ConstiBoost\bin\Release\net8.0-windows\win-x64\publish\` |

Das Projekt setzt `EnableWindowsTargeting=true`, daher lässt es sich auch auf Linux/macOS (z. B. in CI) **kompilieren** – ausführen nur unter Windows.

---

## Features

| Bereich | Was es wirklich tut |
|---|---|
| **Ersteinrichtung** | „WELCOME TO CONSTIBOOST“ → *SCAN MY PC* → „Your PC is ready.“ mit Gaming Readiness (0–100) und empfohlenen Optimierungen |
| **Dashboard** | CPU, GPU, RAM, Windows (per WMI), Live-Auslastung, Temperaturen (nur echte Sensoren), Gaming Readiness Score mit Checkliste, Status 🟢/🟡/🔴, ⚡ OPTIMIZE MY PC, 🎮 Gaming Mode |
| **Low-End-Erkennung** | Erkennt integrierte GPUs (und speziell Acer Aspire 3 + Ryzen 5 5000 + Radeon) → „We detected a low-power integrated-GPU system.“ + LOW-END GAMING PROFILE |
| **One-Click Optimize** | 1. Analyse → 2. Liste → 3. Bestätigung → 4. Backup (+ optional Wiederherstellungspunkt) → 5. Anwenden → 6. Ergebnis inkl. Score vorher/nachher |
| **Windows Optimizer** | Game Mode, Energieprofil (Windows-„Power mode: Best performance“ bzw. Höchstleistung), Xbox-Hintergrundaufnahme, Game-DVR-Captures, Autostart-Apps, Hintergrund-Apps schließen, Temp-Dateien, Shader-Cache, Transparenz, Animationen, Store-Hintergrund-Apps, Mausbeschleunigung, HAGS (Toggle, UAC) – jeweils mit *Current / Recommended / Why / Downsides / Impact* |
| **Gaming Features** | Game Mode, Xbox Game Bar, Hardware-accelerated GPU Scheduling: Status + Erklärung + Nachteile; Status der Xbox/Gaming Services (nur Anzeige) |
| **🎮 Games** (ersetzt den Fortnite-Tab) | Alle installierten Spiele aus Steam-Bibliotheken, Epic-Manifesten, **Microsoft Store** (z. B. Minecraft Bedrock, Roblox, Solitaire), **Xbox-App / Game Pass** (XboxGames-Ordner, MicrosoftGame.config), **Minecraft: Java Edition** (Minecraft Launcher, letzte Nutzung aus launcher_profiles.json), der Windows-Game-Bar-Spieleliste und Windows-App-Einträgen (GOG, EA, Ubisoft, Riot, Battle.net, Rockstar …) · Suche & Sortierung (zuletzt gespielt / A–Z / Größe) · **Zuletzt gespielt** aus echten Quellen (Steam, Windows UserAssist, Spiel-Logs, eigene Sitzungserkennung) · Läuft-Anzeige · **PLAY** · **UNINSTALL** (nur über offizielle Uninstaller) · aufklappbar mit **📈 FPS-Schätzung** (Spanne, alle Faktoren offengelegt, Auflösung/Render-Scale wählbar, bei Fortnite aus deiner echten Config) und **🎯 Game Focus** |
| **🎯 Game Focus** | Alle sicheren Ressourcen auf ein Spiel: Gaming-Mode-Profil, Game Mode, Energieprofil, Apps schließen, Hintergrund-Priorität senken, Spiel „Above normal“ solange es läuft, Windows-GPU-Präferenz „Hohe Leistung“, Benachrichtigungs-Banner stumm, Fortnite-FPS-Cap – alles mit STOP FOCUS rückgängig |
| **Fortnite Tools** | Jetzt in der Fortnite-Karte unter Games: Presets MAX FPS / BALANCED / QUALITY, aktuelle Config, Shader-Cache, Prozesspriorität |
| **🧪 Boost Lab** | **Hardware-Insights**: Single-/Dual-Channel-RAM, RAM-Größe & -Takt, reservierter iGPU-Speicher (UMA), Grafiktreiber-Alter, HDD/SSD/NVMe der Spiele-Laufwerke, Bildwiederholrate, Akku/Netz, GPU-Leistungsklasse · **Low-End Boost Pack**: Energieprofil, Game Mode, Xbox-Aufnahme, Autostart, Transparenz, Animationen, **Desktop-Effekte (Best Performance)**, **Windows-Tipps & Vorschläge**, Store-Hintergrund-Apps, Temp-Dateien |
| **🌡️ Thermal Monitor** | Live: CPU-/GPU-Temperatur, CPU-/GPU-Last, RAM, CPU-Takt, GPU-Takt, Lüfter, Energieprofil, Prozessanzahl · Stufen Cool/Normal/Warm/Hot/Critical (Richtwerte) · Verlauf (5 min Chart + Minutentabelle mit Phasen Idle/Gaming/Heavy) · 🔍 SCAN MY PC mit Ursachenerkennung (CPU-Last, Hintergrund, Energieprofil, Gaming, Kühlung) · ❄️ FIND WAYS TO LOWER TEMPERATURE · ❄️ OPTIMIZE TEMPERATURE mit Backup + Nachmessung (Before/After) · 🌡️ 60 SECOND THERMAL TEST · Thermal Health Score · Alerts · Cooling Check · Fan Check · Fortnite Thermal Mode · Ryzen-Erkennung (z. B. „AMD Ryzen 7 5700U detected.“, Zen 2) |
| **❄️ Auto-Cool & Thermal Settings** | Einstellbare Alarmschwellen (High/Critical/Dauer), Quick-/Deep-Scan, optionaler **Auto-Cool-Wächter**: bleibt die Temperatur 60 s über dem Trigger, wird auf „Ausgewogen“ (optional CPU-Boost aus) geschaltet und nach 2 min Abkühlung automatisch zurückgesetzt – ohne UAC-Popups, pausiert bei Gaming Mode/Focus |
| **🎮 Cool Gaming Mode** | Profile **MAX FPS** (Best performance), **BALANCED** (Balanced, Boost an, optionaler Fortnite-FPS-Cap), **COOL** (Balanced + max. Prozessorzustand 99 % = CPU-Boost aus) · Vergleich der Profile anhand echter Thermal-Tests (Temperatur vs. CPU-Takt) |
| **Cleaner** | Temporary Files, Windows Temporary Files (UAC), Shader Cache, Crash Reports, Browser Cache – mit „This will remove approximately XX MB.“ |
| **Startup Manager** | App, Publisher, Startup Impact (typisch, nicht gemessen), Enabled/Disabled – gleicher Mechanismus wie der Task-Manager; System-/Treiber-/Sicherheits-Apps geschützt |
| **Hardware** | Live-Charts CPU/GPU/RAM (1 s), Thermal Zone, Systemdetails, Laufwerke |
| **Prozess-Manager** | Process, PID, CPU %, RAM, GPU %, Status, sortierbar, *Close Process* mit Safety List |
| **Network** | Adapter, Verbindungstyp, Link-Speed, Live-Traffic, Ping/Jitter, DNS-Test, optionaler Speedtest |
| **Benchmark** | „System Performance Benchmark“ (CPU single/multi, Speicherbandbreite, Hintergrundlast, Scheduling-Jitter) mit Before / After / Difference |
| **Backups** | Liste aller Backups, Details, RESTORE, Löschen, CREATE RESTORE POINT, Aktivitätslog |
| **Settings** | Theme (Dark/Midnight), Accent Color, Start with Windows, Create Restore Point, Automatic Backup (immer an), Notifications |

### Bewusst NICHT enthalten
- Keine „Network Booster“, „RAM Booster“, „CPU/GPU Unlock“, „0 Ping“-Registry-Tweaks oder andere Placebo-Tweaks.
- Keine gemessenen Fake-FPS: Live-FPS bleiben *„Unavailable“* (keine Injection, Anti-Cheat-sicher). Die **FPS-Schätzung** im Games-Tab ist immer als Schätzung mit Spanne gekennzeichnet und legt alle Faktoren offen (GPU-Klasse, RAM-Kanäle, Auflösung, Energiemodus, Temperatur).
- Keine geschätzten Temperaturen. ConstiBoost installiert keine Kernel-Treiber. Echte CPU-/GPU-Temperaturen, GPU-Takt und Lüfter-RPM werden **read-only** übernommen, wenn **HWiNFO** (Einstellung „Shared Memory Support“) oder **LibreHardwareMonitor** im Hintergrund laufen. Sonst wird die Windows-ACPI-Thermal-Zone genutzt und als „indirekt“ gekennzeichnet; ist nichts lesbar: *„Sensor unavailable“*.
- Keine Spannungsänderungen, kein Undervolting/Overclocking, kein BIOS-/Firmware-Zugriff, keine Lüftersteuerung, kein Abschalten von Thermal Throttling.
- Kein automatisches Umstellen des Fortnite-Rendering-Modus: ConstiBoost erklärt den Schritt im Spiel, statt unsichere Config-Keys zu erfinden.

---

## Safety

**ConstiBoost darf und wird nie:** Dateien verschlüsseln · Defender, Firewall oder Windows Update deaktivieren · Passwörter, Cookies oder Browserdaten lesen · persönliche Dateien löschen · heimlich Prozesse manipulieren · Telemetrie oder Daten an Server senden.

Technische Schutzmechanismen:
- **Backup vor jeder Änderung** (`BackupSession`): Registry-Werte (inkl. „existierte nicht“), Energieplan, Power Mode, SystemParametersInfo-Werte und Dateien (Fortnite-INI) werden **vor** dem Schreiben auf Platte gesichert.
- **Verifikation:** Registry-Schreibvorgänge werden zurückgelesen; schlägt das fehl → *FAILED*.
- **Admin nur bei Bedarf:** Die App läuft mit `asInvoker`. Für HKLM-Werte, `C:\Windows\Temp` oder Wiederherstellungspunkte startet sie kurz eine zweite Instanz per UAC (`--elevated`), die **nur eine feste Allowlist** an Registry-Pfaden und Ordnern akzeptiert.
- **Cleaner-Pfadschutz:** nur bekannte Cache-/Temp-Ordner; Dokumente, Bilder, Desktop, Downloads, Musik, Videos, OneDrive und „Saved Games“ sind gesperrt; Junctions/Symlinks werden nie verfolgt; Dateien in Benutzung werden übersprungen; Temp-Dateien < 24 h bleiben.
- **Prozess-Safety-List:** `System`, `csrss`, `winlogon`, `lsass`, `services`, `svchost`, `smss`, `wininit`, `dwm`, Defender, Anti-Cheat u. a. werden **nie** beendet; `explorer.exe` nur nach extra Warnung; Session-0-Prozesse sind tabu. Normale Apps werden zuerst höflich geschlossen (wie Klick auf X).
- **Startup:** Nichts wird gelöscht – nur das „StartupApproved“-Flag wie im Task-Manager. Windows-, Sicherheits-, Audio-, Touchpad- und Grafiktreiber-Einträge sind geschützt.
- **Fortnite:** Nur Keys, die in deiner `GameUserSettings.ini` bereits existieren, werden geändert; Encoding/Zeilenenden bleiben erhalten; Fortnite muss geschlossen sein.
- **Fehlerbehandlung:** Jede Systemoperation ist in `try/catch`; fehlende Rechte führen zu „This optimization couldn't be applied.“ statt zu einem Absturz; Details im technischen Log.
- **Leichtgewichtig:** Live-Monitoring läuft nur, solange Dashboard/Hardware sichtbar sind, und pausiert bei minimiertem Fenster.

## Restore

1. **Backups** in der Sidebar öffnen → gewünschtes Backup → **RESTORE**. Alle gespeicherten Werte werden in umgekehrter Reihenfolge zurückgeschrieben (HKLM-Werte mit einer UAC-Abfrage).
2. **Gaming Mode** wird über *STOP GAMING MODE* zurückgesetzt (Game Mode, Power Mode, Prozessprioritäten, geschlossene Apps optional wieder öffnen). Der Zustand liegt auf der Platte – auch nach einem Absturz kann er beim nächsten Start gestoppt werden.
3. **Windows-Wiederherstellungspunkt:** *CREATE RESTORE POINT* (Backups-Seite) bzw. automatisch vor größeren Optimierungen (Settings). Zurücksetzen über *SYSTEM RESTORE* (`rstrui.exe`).
4. **Manuell:** Jedes Backup liegt als lesbares JSON unter `%LOCALAPPDATA%\ConstiBoost\Backups\yyyy-MM-dd_HH-mm-ss\manifest.json` (Fortnite-INI als Dateikopie daneben).

## Daten & Logs

| Pfad | Inhalt |
|---|---|
| `%LOCALAPPDATA%\ConstiBoost\settings.json` | Einstellungen |
| `%LOCALAPPDATA%\ConstiBoost\Backups\` | Backups (ein Ordner pro Änderungsvorgang) |
| `%LOCALAPPDATA%\ConstiBoost\Logs\changes-*.log` | Änderungslog: Timestamp · Action · Old → New · SUCCESS/FAILED + Reason |
| `%LOCALAPPDATA%\ConstiBoost\Logs\technical-*.log` | technisches Log (Exceptions, Warnungen) |
| `%LOCALAPPDATA%\ConstiBoost\benchmarks.json` | Benchmark-Verlauf |

Beispiel Änderungslog:
```
[2026-10-07 23:14:03]
Game Mode
OFF → ON
SUCCESS
```

---

## Troubleshooting

| Problem | Lösung |
|---|---|
| „Administrator permission was declined (UAC)“ | Aktion erneut ausführen und die UAC-Abfrage bestätigen. Normale Funktionen brauchen keine Adminrechte. |
| GPU-Auslastung „Unavailable“ | Die Windows-Leistungsindikatoren „GPU Engine“ fehlen/sind defekt. In einer Admin-Konsole `lodctr /R` ausführen und neu starten. Grafiktreiber aktualisieren. |
| Temperaturen „Unavailable“ / nur ACPI | HWiNFO starten → Settings → „Shared Memory Support“ aktivieren (oder LibreHardwareMonitor starten). ConstiBoost erkennt das innerhalb von ~20 s automatisch. |
| Lüfter „Fan speed unavailable on this device.“ | Viele Laptops (auch Acer Aspire) melden die Lüfterdrehzahl nicht an Windows – das ist normal. |
| COOL-Profil fragt nach Adminrechten | Windows erlaubt das Ändern des max. Prozessorzustands je nach System nur mit UAC. Ablehnen ist unkritisch – dann bleibt der Boost an. |
| Wiederherstellungspunkt wird nicht erstellt | Computerschutz für Laufwerk C: aktivieren (`SystemPropertiesProtection.exe`). Windows erlaubt standardmäßig nur einen Punkt pro 24 h. |
| Fortnite wird nicht gefunden | Fortnite einmal über den Epic Games Launcher starten bzw. den Launcher-Bibliotheksordner prüfen. |
| „Settings file not found“ | Fortnite einmal starten und beenden, damit `%LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini` entsteht. |
| Preset wird nach Spielstart zurückgesetzt | Fortnite war beim Anwenden offen – schließen, Preset erneut anwenden. Rendering Mode im Spiel auf *Performance* stellen. |
| Prozesspriorität lässt sich nicht ändern | Easy Anti-Cheat schützt den Fortnite-Prozess – erwartet und unkritisch. |
| Power Mode ändert sich beim Ab-/Anstecken | Windows speichert den Power Mode getrennt für Akku/Netz. Optimierung im eingesteckten Zustand erneut anwenden. |
| Browser-Cache wird nur teilweise gelöscht | Browser vorher schließen – Dateien in Benutzung werden übersprungen. |
| App startet nicht / Fehler | `%LOCALAPPDATA%\ConstiBoost\Logs\technical-*.log` prüfen. |

---

## Projektstruktur

```
ConstiBoost.sln
ConstiBoost/
├── App/          App.xaml(.cs) – Start, Elevated-Helper-Modus, globale Fehlerbehandlung, app.manifest (asInvoker)
├── Core/         ObservableObject, (Async)RelayCommand, AppPaths, Safe (try/catch-Helfer), OperationResult, Formatting
├── Models/       HardwareInfo, SystemState, ReadinessCheck, StartupEntry, CleanupCategory, ProcessRow, AppGroup, AppSettings …
├── Services/     Scan/Readiness, Monitor, Startup, Cleaner, Prozesse, Gaming Mode, Elevation (UAC), Restore Point, Settings, Theme, Dialoge
├── Optimizers/   Optimization-Basisklasse + jede Optimierung als eigene Klasse, OptimizationCatalog
├── Hardware/     WMI-Scanner, DeviceProfile (Low-End/Acer-Erkennung), GPU-/Thermal-Sampler, Network, Benchmark
├── Fortnite/     Locator (Epic-Manifeste), IniDocument, Config-Service, Presets, Prozesspriorität
├── Windows/      P/Invoke (powrprof, kernel32, user32), Registry-Helfer/-Snapshots, PowerManager, SystemParameters, ProcessSafety
├── UI/           SparklineChart, ScoreRing, Converter
├── Views/        MainWindow, WelcomeView, Dashboard, Optimizer, Fortnite, Cleaner, Startup, Hardware, Backups, Settings, ConfirmDialog
├── ViewModels/   ein ViewModel pro Seite + Gaming Mode, Prozesse, Network, Benchmark
├── Resources/    Theme.xaml (Farben), Styles.xaml (Controls), Templates.xaml (Converter, Gaming-Mode-Karte), Icon
├── Backup/       BackupSession/Manifest/Entry, BackupService (Restore)
└── Logs/         ActionLog (Änderungslog), AppLog (technisches Log)
```

## Entwicklungsphasen – was wo umgesetzt ist und wie man es testet

| Phase | Dateien | Testen |
|---|---|---|
| 1 Projekt, UI, Navigation, Theme | `App/*`, `Views/MainWindow.*`, `Resources/*`, `ViewModels/MainViewModel.cs`, `ViewModels/PageViewModel.cs` | `dotnet run` → Sidebar-Navigation, Fenster verschieben/maximieren, Settings → Theme/Accent wechseln |
| 2 Hardware Detection | `Hardware/HardwareScanner.cs`, `Hardware/DeviceProfile.cs`, `Services/ScanService.cs` | Erststart → SCAN MY PC; Werte mit Task-Manager → Leistung vergleichen |
| 3 Dashboard | `Views/DashboardView.xaml`, `ViewModels/DashboardViewModel.cs`, `UI/ScoreRing.cs` | Score + Checkliste; Game Mode in Windows ausschalten → RESCAN → Score sinkt |
| 4 Windows Optimizer | `Optimizers/*`, `ViewModels/OptimizerViewModel.cs`, `Views/OptimizerView.xaml` | ⚡ OPTIMIZE MY PC → Liste prüfen → APPLY → Einstellungen in Windows kontrollieren → Backups → RESTORE |
| 5 Fortnite | `Fortnite/*`, `ViewModels/FortniteViewModel.cs`, `Views/FortniteView.xaml` | Fortnite geschlossen → Preset anwenden → INI-Werte vergleichen → Backup wiederherstellen |
| 6 Cleaner | `Services/CleanerService.cs`, `ViewModels/CleanerViewModel.cs` | SCAN → Größe prüfen → CLEAN → erneut scannen |
| 7 Startup Manager | `Services/StartupService.cs`, `ViewModels/StartupViewModel.cs` | App deaktivieren → im Task-Manager (Autostart) ebenfalls „Deaktiviert“ |
| 8 Gaming Mode | `Services/GamingModeService.cs`, `Services/BackgroundAppsService.cs`, `ViewModels/GamingModeViewModel.cs` | START → Power Mode/Prioritäten im Task-Manager prüfen → STOP → alles zurück |
| 9 Backup/Restore | `Backup/*`, `Services/ElevationService.cs`, `Services/ElevatedTaskRunner.cs`, `Services/RestorePointService.cs` | Backups-Seite: Einträge ansehen, RESTORE, CREATE RESTORE POINT |
| 10 Hardware Monitor | `Services/MonitorService.cs`, `Hardware/GpuUsageSampler.cs`, `Hardware/ThermalSampler.cs`, `UI/SparklineChart.cs`, `ViewModels/HardwareViewModel.cs`, `ViewModels/ProcessesViewModel.cs`, `ViewModels/NetworkViewModel.cs` | Hardware → Monitor mit Task-Manager vergleichen; Processes sortieren; Network → Ping/DNS |
| 11 Benchmark | `Hardware/BenchmarkService.cs`, `ViewModels/BenchmarkViewModel.cs` | RUN BEFORE → optimieren → RUN AFTER → Difference |
| 13 Thermal System | `Hardware/Sensors/*` (HWiNFO, LibreHardwareMonitor, ACPI), `Hardware/CpuFrequencySampler.cs`, `Hardware/GpuMemoryReader.cs`, `Hardware/CpuDatabase.cs`, `Services/ThermalMonitorService.cs`, `Services/ThermalAnalysisService.cs`, `Services/ThermalTestService.cs`, `Services/PowerTweakService.cs`, `ViewModels/ThermalViewModel.cs`, `Views/ThermalView.xaml` | Thermal → SCAN MY PC; Werte mit HWiNFO vergleichen; 60-s-Test in MAX FPS und COOL während Fortnite → Vergleichstabelle |
| 14 PRO 2.0 | `Games/*` (Bibliothek, VDF-Parser, Last-Played, FPS-Schätzer, Game Focus), `Hardware/GpuDatabase.cs`, `Hardware/StorageInfo.cs`, `Services/HardwareInsightsService.cs`, `Services/AutoCoolService.cs`, `Optimizers/LowEndOptimizations.cs`, `ViewModels/GamesViewModel.cs`, `ViewModels/BoostLabViewModel.cs`, `Views/GamesView.xaml`, `Views/BoostLabView.xaml` | Games: Spiel aufklappen → Schätzung prüfen → FOCUS → Spiel starten → STOP FOCUS; Boost Lab: Insights prüfen → APPLY BOOST PACK → Backups → RESTORE |
| 12 Polishing | Animationen (Seitenwechsel, Score-Ring, Toggles), Toasts, Pause bei Minimierung, Gaming-Mode-Abfrage beim Schließen | – |

## Lizenz / Haftung

Privates Projekt. Nutzung auf eigene Verantwortung – dank Backups und Wiederherstellungspunkten lassen sich alle Systemänderungen zurücksetzen.
