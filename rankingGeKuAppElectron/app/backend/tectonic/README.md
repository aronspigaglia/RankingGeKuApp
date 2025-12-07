Bundled Tectonic binaries
=========================

Leg hier die plattformspezifischen Tectonic-Binaries ab, damit die App ohne separate Installation PDFs bauen kann.

Erwartete Struktur:

- `macos/tectonic` (aus https://github.com/tectonic-typesetting/tectonic/releases – macOS aarch64/x86_64 je nach Build)
- `windows/tectonic.exe` (Windows x64 Build)
- optional: `linux/tectonic` falls du auch Linux-Pakete baust

Die Backend-Logik sucht zuerst hier (AppContext.BaseDirectory/tectonic/<platform>/...), sonst fällt sie auf `tectonic` im PATH zurück.

Nach dem Einlegen der Binaries: `chmod +x macos/tectonic` bzw. `chmod +x linux/tectonic` nicht vergessen, bevor du packst.
