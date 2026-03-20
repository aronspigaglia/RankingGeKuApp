import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Athlete, PerDurchgangNotes } from '../models/athlete';

interface PersistedState {
  csv: string | null;
  groups: Athlete[][];
}

@Injectable({ providedIn: 'root' })
export class NotesStateService {
  private readonly STORAGE_KEY = 'ranking-geku-state-v1';

  private groupsSubject = new BehaviorSubject<Athlete[][]>([]);
  groups$ = this.groupsSubject.asObservable();

  private importedSubject = new BehaviorSubject<boolean>(false);
  imported$ = this.importedSubject.asObservable();

  private rawCsv: string | null = null;
  getRawCsv(): string | null { return this.rawCsv; }

  /** Beim App-Start aufrufen: lädt vorhandenen Zustand aus localStorage. */
  loadFromStorage() {
    try {
      const raw = localStorage.getItem(this.STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as PersistedState;
      this.rawCsv = parsed.csv;
      this.groupsSubject.next(parsed.groups ?? []);
      this.importedSubject.next(!!parsed.groups?.length);

      if (parsed.groups) {
        this.setGroups(parsed.groups); 
      }

    } catch {
      // falls etwas korrupt ist: ignorieren
    }
  }

  /** Vereinheitlichte Speicherung (CSV + Gruppen) nach jeder Änderung. */
  private saveToStorage() {
    const snapshot: PersistedState = {
      csv: this.rawCsv,
      groups: this.groupsSubject.value
    };
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(snapshot));
  }

  /** Alles löschen + Speicher aufräumen. */
  clear() {
    this.rawCsv = null;
    this.groupsSubject.next([]);
    this.importedSubject.next(false);
    localStorage.removeItem(this.STORAGE_KEY);
  }

  /** Gruppen explizit setzen (und speichern). */
  setGroups(groups: Athlete[][]) {
    this.groupsSubject.next(groups);
    this.importedSubject.next(groups.length > 0);
    const cats = groups
      .flatMap(g => g)
      .map(a => a.kat?.trim())
      .filter((k): k is string => !!k)
      .filter((v, i, arr) => arr.indexOf(v) === i) 
      .sort((a, b) => a.localeCompare(b));

  this.categoriesSubject.next(cats);
    this.saveToStorage();
  }

  /** CSV aus aktuellem Zustand erzeugen (inkl. Noten). */
  exportNotesCsv(delimiter = ';'): string {
    const header = [
      'Gruppe',
      'Nachname',
      'Vorname',
      'JG',
      'Verein',
      'Kat',
      'D1',
      'END1',
      'D2',
      'END2',
      'D3',
      'END3',
      'D4',
      'END4',
      'D5',
      'END5',
      'D6',
      'END6'
    ];

    const lines = [header.join(delimiter)];
    const groups = this.groupsSubject.value;

    groups.forEach((group, gIndex) => {
      group.forEach(athlete => {
        const notes = this.ensureSixNotes(athlete.notes)
          .flatMap(n => [n.dNote ?? '', n.endNote ?? '']);

        lines.push([
          gIndex + 1,
          athlete.nachname,
          athlete.vorname,
          athlete.jg,
          athlete.verein,
          athlete.kat,
          ...notes
        ].map(v => (v ?? '').toString().replace(/\r?\n/g, ' ').trim()).join(delimiter));
      });
    });

    return lines.join('\n');
  }

  /**
   * CSV laden (inkl. Noten). Erwartet Header mit "Gruppe;Nachname;...;END6".
   * Notenfelder sind optional.
   */
  loadNotesCsvText(csvText: string, delimiter = ';') {
    const lines = csvText
      .split(/\r?\n/)
      .map(l => l.trim())
      .filter(l => !!l);

    if (!lines.length) return;

    const rows = lines.map(l => l.split(delimiter).map(p => p.trim()));
    // optional Header entfernen
    const first = rows[0];
    if (first?.[0]?.toLowerCase() === 'gruppe') {
      rows.shift();
    }

    const groups: Athlete[][] = [];

    for (const parts of rows) {
      if (parts.length < 6) continue;
      const [
        groupStr,
        nachname = '',
        vorname = '',
        jg = '',
        verein = '',
        kat = '',
        ...noteParts
      ] = parts;

      const groupIndex = Number.parseInt(groupStr, 10);
      const targetIdx =
        Number.isFinite(groupIndex) && groupIndex > 0
          ? groupIndex - 1
          : groups.length;

      while (groups.length <= targetIdx) {
        groups.push([]);
      }

      const notes: PerDurchgangNotes[] = [];
      for (let i = 0; i < 6; i++) {
        const dNote = noteParts[i * 2]?.trim() ?? '';
        const endNote = noteParts[i * 2 + 1]?.trim() ?? '';
        notes.push({
          ...(dNote ? { dNote } : {}),
          ...(endNote ? { endNote } : {})
        });
      }

      groups[targetIdx].push({
        nachname,
        vorname,
        jg,
        verein,
        kat,
        notes: this.ensureSixNotes(notes)
      });
    }

    this.rawCsv = this.buildRawAthleteCsv(groups, delimiter);
    this.setGroups(groups);
  }

  /**
   * CSV laden (ohne Header). Gruppen werden durch eine Zeile "-" getrennt.
   * Spalten: Nachname;Vorname;JG;Verein;Kat
   */
  loadCsvText(csvText: string, delimiter = ';') {
    this.rawCsv = csvText;

    const lines = csvText
      .split(/\r?\n/)
      .map(l => l.trim());

    const groups: Athlete[][] = [];
    let current: Athlete[] = [];

    const isDashOnly = (l: string) => /^-+$/.test(l);
    for (const line of lines) {
      if (!line) continue;
      if (isDashOnly(line)) {
        if (current.length) groups.push(current);
        current = [];
        continue;
      }

      const [nachname = '', vorname = '', jg = '', verein = '', kat = ''] =
        line.split(delimiter).map(p => p?.trim() ?? '');

      if (!nachname && !vorname && !jg && !verein && !kat) continue;

      current.push({
        nachname, vorname, jg, verein, kat,
        notes: Array.from({ length: 6 }, () => ({})), // D1..D6
      });
    }
    if (current.length) groups.push(current);

    this.setGroups(groups);     // setzt + speichert
    this.saveToStorage();       // speichert auch CSV
  }

  /** Bei Änderungen an Noten aufrufen (z. B. nach ngModelChange). */
  saveSnapshot() {
    this.saveToStorage();
  }

  getGroupsSnapshot() {
  return this.groupsSubject.value;
  }

  private categoriesSubject = new BehaviorSubject<string[]>([]);
  categories$ = this.categoriesSubject.asObservable();

  getCategoriesSnapshot(): string[] {
    return this.categoriesSubject.value;
  }

  private ensureSixNotes(notes: PerDurchgangNotes[]): PerDurchgangNotes[] {
    const result = Array.from({ length: 6 }, (_, i) => notes[i] ?? {});
    return result;
  }

  private buildRawAthleteCsv(groups: Athlete[][], delimiter = ';'): string {
    const lines: string[] = [];

    groups.forEach((group, idx) => {
      if (idx > 0) {
        lines.push('-');
      }

      group.forEach(a => {
        lines.push([
          a.nachname,
          a.vorname,
          a.jg,
          a.verein,
          a.kat
        ].map(v => v ?? '').join(delimiter));
      });
    });

    return lines.join('\n');
  }
}
