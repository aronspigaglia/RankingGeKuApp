import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Athlete, PerDurchgangNotes } from '../models/athlete';
import { RankingAthleteDto } from '../models/ranking-request';
import { NOTE_COUNT } from '../models/gymnastics';

interface PersistedState {
  csv: string | null;
  groups: Athlete[][];
}

/**
 * Hält die importierten Athleten-Gruppen samt Noten und
 * persistiert jeden Stand in localStorage.
 */
@Injectable({ providedIn: 'root' })
export class NotesStateService {
  private readonly STORAGE_KEY = 'ranking-geku-state-v1';

  private readonly groupsSubject = new BehaviorSubject<Athlete[][]>([]);
  readonly groups$ = this.groupsSubject.asObservable();

  private readonly importedSubject = new BehaviorSubject<boolean>(false);
  readonly imported$ = this.importedSubject.asObservable();

  private readonly categoriesSubject = new BehaviorSubject<string[]>([]);
  readonly categories$ = this.categoriesSubject.asObservable();

  /** Original-CSV (ohne Noten), wird fürs Erzeugen der Notenblätter gebraucht. */
  private rawCsv: string | null = null;

  getRawCsv(): string | null {
    return this.rawCsv;
  }

  getGroupsSnapshot(): Athlete[][] {
    return this.groupsSubject.value;
  }

  /** Beim App-Start aufrufen: lädt vorhandenen Zustand aus localStorage. */
  loadFromStorage(): void {
    try {
      const raw = localStorage.getItem(this.STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as PersistedState;
      this.rawCsv = parsed.csv;
      this.setGroups(parsed.groups ?? []);
    } catch {
      // falls etwas korrupt ist: ignorieren
    }
  }

  /** Alles löschen + Speicher aufräumen. */
  clear(): void {
    this.rawCsv = null;
    this.groupsSubject.next([]);
    this.importedSubject.next(false);
    this.categoriesSubject.next([]);
    localStorage.removeItem(this.STORAGE_KEY);
  }

  /** Gruppen setzen: aktualisiert alle abgeleiteten Streams und speichert. */
  setGroups(groups: Athlete[][]): void {
    this.groupsSubject.next(groups);
    this.importedSubject.next(groups.length > 0);
    this.categoriesSubject.next(this.collectCategories(groups));
    this.saveToStorage();
  }

  /**
   * Athleten-CSV laden (ohne Header). Gruppen werden durch eine Zeile "-" getrennt.
   * Spalten: Nachname;Vorname;JG;Verein;Kat
   */
  loadCsvText(csvText: string, delimiter = ';'): void {
    this.rawCsv = csvText;

    const groups: Athlete[][] = [];
    let current: Athlete[] = [];

    const isDashOnly = (l: string) => /^-+$/.test(l);
    for (const line of csvText.split(/\r?\n/).map(l => l.trim())) {
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
        notes: this.emptyNotes(),
      });
    }
    if (current.length) groups.push(current);

    this.setGroups(groups);
  }

  /** Exportierten Noten-Stand (JSON-Import) wieder in Gruppen laden. */
  loadFromImportedAthletes(athletes: RankingAthleteDto[]): void {
    const groups: Athlete[][] = [];

    for (const a of athletes) {
      const targetIdx =
        Number.isFinite(a.groupIndex) && a.groupIndex > 0
          ? a.groupIndex - 1
          : groups.length;

      while (groups.length <= targetIdx) {
        groups.push([]);
      }

      groups[targetIdx].push({
        nachname: (a.nachname ?? '').trim(),
        vorname: (a.vorname ?? '').trim(),
        jg: (a.jg ?? '').trim(),
        verein: (a.verein ?? '').trim(),
        kat: (a.kat ?? '').trim(),
        notes: this.ensureNoteCount(a.notes ?? []),
      });
    }

    this.rawCsv = this.buildRawAthleteCsv(groups);
    this.setGroups(groups);
  }

  /** Bei Änderungen an Noten aufrufen (z. B. nach ngModelChange). */
  saveSnapshot(): void {
    this.saveToStorage();
  }

  private saveToStorage(): void {
    const snapshot: PersistedState = {
      csv: this.rawCsv,
      groups: this.groupsSubject.value,
    };
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(snapshot));
  }

  /** Eindeutige, sortierte Kategorien aller Athleten. */
  private collectCategories(groups: Athlete[][]): string[] {
    const cats = groups
      .flat()
      .map(a => a.kat?.trim())
      .filter((k): k is string => !!k);
    return [...new Set(cats)].sort((a, b) => a.localeCompare(b));
  }

  private emptyNotes(): PerDurchgangNotes[] {
    return Array.from({ length: NOTE_COUNT }, () => ({}));
  }

  /** Stellt sicher, dass genau NOTE_COUNT Einträge vorhanden sind (D1..D6). */
  private ensureNoteCount(notes: PerDurchgangNotes[]): PerDurchgangNotes[] {
    return Array.from({ length: NOTE_COUNT }, (_, i) => {
      const n = notes[i] ?? {};
      const dNote = n.dNote?.toString().trim();
      const endNote = n.endNote?.toString().trim();
      return {
        ...(dNote ? { dNote } : {}),
        ...(endNote ? { endNote } : {}),
      };
    });
  }

  /** Athleten-CSV (ohne Noten) aus Gruppen rekonstruieren – Format wie beim Erst-Import. */
  private buildRawAthleteCsv(groups: Athlete[][], delimiter = ';'): string {
    const lines: string[] = [];

    groups.forEach((group, idx) => {
      if (idx > 0) {
        lines.push('-');
      }

      group.forEach(a => {
        lines.push(
          [a.nachname, a.vorname, a.jg, a.verein, a.kat]
            .map(v => v ?? '')
            .join(delimiter)
        );
      });
    });

    return lines.join('\n');
  }
}
