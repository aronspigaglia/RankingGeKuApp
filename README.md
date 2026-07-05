# RankingGeKu App – Download & CSV-Format

## Download und Start
- **macOS (Apple Silicon)**: ZIP `RankingGeKu-mac-m-chip-<version>.zip` entpacken, App nach `Programme` verschieben und von dort starten. Falls macOS blockt: Rechtsklick → Öffnen → Öffnen. Backend & Tectonic sind mitgeliefert, keine Zusatz-Installation nötig.
- **macOS (Intel)**: Nicht verfügbar
- **Windows**: `RankingGeKu-win-x64-Setup.exe` ausführen und installieren. Falls SmartScreen warnt: „Weitere Informationen“ → Trotzdem ausführen. Backend & Tectonic sind enthalten.

## CSV-Format (Athleten)
- **Trenner**: Semikolon `;`
- **Keine Header-Zeile**
- **Spaltenreihenfolge**: `Nachname;Vorname;JG;Verein;Kat`
- **Gruppen trennen**: Eine Zeile mit nur einem `-` erzeugt eine neue Gruppe.
- **Leere Zeilen**: werden ignoriert.
- **Pro Wettkampfdurchgang**: Ein separates CSV verwenden.

### Beispiel
```
Meier;Anna;2005;LC Beispiel;U18
Muster;Luca;2004;TV Stadt;U20
-
Schmid;Eva;2007;LG Test;U16
```
Ergebnis: Zwei Gruppen – Gruppe 1 mit Meier/Muster, Gruppe 2 mit Schmid. Blanke Zeilen sind egal, eine Zeile mit `-` startet die nächste Gruppe.

## Hinweise
- PDFs werden lokal erzeugt; Tectonic liegt dem Backend bei.
- Momenat nur macos-m-chip und Windows 10/11
- Zwischenstände sichern: Button **Export** speichert den aktuellen Stand als JSON. Über **Import** kann jeder exportierte Zwischenstand später wieder geladen werden.

## Architektur (für Entwickler)

Die App besteht aus drei Teilen, die zusammen als Desktop-App ausgeliefert werden:

```
┌─────────────────────────────────────────────────────────┐
│ Electron (rankingGeKuAppElectron/)                       │
│                                                          │
│  ┌────────────────────────┐   HTTP (127.0.0.1:5157)      │
│  │ Angular-Frontend       │ ────────────────────────┐    │
│  │ (Frontend_RankingGeKu) │                         ▼    │
│  └────────────────────────┘   ┌────────────────────────┐ │
│                               │ ASP.NET-Core-Backend   │ │
│                               │ (solBackend_RankingGeKu│ │
│                               │  + Tectonic → PDF)     │ │
│                               └────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

- **Electron** ([main.js](rankingGeKuAppElectron/main.js)): startet beim App-Start das gepublishte Backend als Kindprozess (`--urls http://127.0.0.1:5157`) und lädt das gebaute Angular-Frontend aus `app/dist/browser`. Beim Beenden wird das Backend mitbeendet.
- **Frontend** (Angular, `Frontend_RankingGeKu/`): UI für CSV-Import, Noteneingabe (D1–D6) und PDF-Erzeugung. `ng build` schreibt direkt nach `rankingGeKuAppElectron/app/dist`.
- **Backend** (ASP.NET Core 8, `solBackend_RankingGeKu/`): erzeugt aus den Daten LaTeX-Quelltext und kompiliert ihn mit dem mitgelieferten **Tectonic** zu PDFs. Läuft nur lokal, kein Netzwerkzugriff nötig (Tectonic-Pakete sind im `tectonic-cache/` gebundelt).

### Frontend-Struktur (`Frontend_RankingGeKu/src/app/`)

| Baustein | Aufgabe |
|---|---|
| `core/sidebar/` | Buttons: CSV-Import, PDF-Erzeugung, Export/Import des Zwischenstands, Alles löschen |
| `features/content/` | Noteneingabe-Tabellen pro Gruppe, Tabs D1–D6 |
| `services/notes-state.service.ts` | Zentraler Zustand (Gruppen + Noten), persistiert jeden Stand in `localStorage` |
| `services/notesheets-api.service.ts` | HTTP-Client für die beiden Backend-Endpoints |
| `models/gymnastics.ts` | Fachliche Konstanten: Geräteliste, EPA-Regeln |
| `shared/file-download.ts` | Browser-Download-Helfer für die PDF-/JSON-Antworten |

Die Geräte rotieren pro Gruppe: Gruppe 1 startet in D1 am Boden, Gruppe 2 am Pferd usw. Vor dem Ranglisten-Request dreht die Sidebar die Noten so, dass Index 0 immer "Boden" ist.

### Backend-Struktur (`solBackend_RankingGeKu/Backend_RankingGeKu/`)

**Endpoints:**
- `POST /api/notesheets/merged` — nimmt die Athleten-CSV, liefert EIN PDF mit allen Notenblättern (pro Gruppe 6 Durchgänge, Gerät rotiert).
- `POST /api/ranking` — nimmt Athleten + Noten einer Kategorie als JSON, liefert die Ranglisten-PDF.

**Aufbau:**

| Baustein | Aufgabe |
|---|---|
| `Controllers/` | Nur HTTP-Handling (Validierung, Request → Services → PDF-Response) |
| `Services/CsvParser.cs` | CSV → Athleten-Gruppen (`-`-Zeile trennt Gruppen) |
| `Services/RankingCalculator.cs` | Totale, Ränge pro Kategorie, Geräte-Ränge, Auszeichnungen (Top 40 %) |
| `Services/LatexBuilder.cs` | LaTeX für die Notenblätter |
| `Services/RankingLatexBuilder.cs` | LaTeX für die Rangliste |
| `Services/LatexEscaper.cs` | Escaped Benutzereingaben für LaTeX |
| `Services/PdfCompiler.cs` | Kompiliert LaTeX mit Tectonic zu PDF (Temp-Verzeichnis, Timeout, Logo-Assets) |
| `Domain/Gymnastics.cs` | Fachliche Konstanten: Geräteliste, EPA-Regeln, Geräteanzahl |
| `Models/` | DTOs (Request/Response) und `RankingRow` (berechnete Ranglisten-Zeile) |

**Fachregeln:**
- Kategorie **EPA** turnt nicht an Pferd und Ring: die Spalten fehlen in der Rangliste, die Eingabe ist im Frontend gesperrt, und Notenblätter für reine EPA-Gruppen an diesen Geräten entfallen.
- Ins Total fließt pro Gerät die **End-Note**; fehlt sie, die D-Note. Gleiches Total = gleicher Rang (1, 1, 3, …).
- Die besten 40 % pro Kategorie erhalten eine Auszeichnung (Smiley in der Rangliste).

### Lokal entwickeln

```bash
# Backend (Port 5157)
cd solBackend_RankingGeKu/Backend_RankingGeKu
dotnet run --urls http://127.0.0.1:5157

# Frontend (Dev-Server auf Port 4200)
cd Frontend_RankingGeKu
npm start

# Electron mit Dev-Server: in rankingGeKuAppElectron/main.js isDev = true setzen
cd rankingGeKuAppElectron
npm start
```

### Release bauen

Die Build-Skripte liegen in `rankingGeKuAppElectron/package.json`:

```bash
cd rankingGeKuAppElectron
npm run make:mac   # Frontend + Backend (osx-arm64) publishen, dann Electron-Forge make
npm run make:win   # dito für Windows (win-x64)
```
