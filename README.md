# RankingGeKu App – Download & CSV-Format

## Download und Start
- **macOS (Apple Silicon)**: ZIP `RankingGeKu-mac-m-chip-<version>.zip` entpacken, App in Programme ziehen. Falls macOS blockt: Rechtsklick → Öffnen → Öffnen. Backend & Tectonic sind mitgeliefert, keine Zusatz-Installation nötig.
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
