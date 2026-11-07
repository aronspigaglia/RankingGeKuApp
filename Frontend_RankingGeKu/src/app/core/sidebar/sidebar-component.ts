import { Component, ElementRef, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { HttpResponse } from '@angular/common/http';
import { NotesStateService } from '../../services/notes-state.service';
import { NotesheetsApiService } from '../../services/notesheets-api.service';
import { RankingAthleteDto, RankingRequestDto } from '../../models/ranking-request';
import { APPARATUS, NOTE_COUNT } from '../../models/gymnastics';
import { downloadBlob, filenameFromContentDisposition } from '../../shared/file-download';

@Component({
  imports: [FormsModule],
  selector: 'app-sidebar-component',
  templateUrl: './sidebar-component.html',
  styleUrl: './sidebar-component.scss',
})
export class SidebarComponent {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  @ViewChild('importFileInput') importFileInput!: ElementRef<HTMLInputElement>;

  imported = false;
  busyNotesheets = false;
  busyRanking = false;
  busyExport = false;
  busyImport = false;
  errorMsg = '';

  categories: string[] = [];
  selectedCategory: string | null = null;

  constructor(
    private state: NotesStateService,
    private api: NotesheetsApiService,
  ) {
    this.state.imported$.subscribe(v => (this.imported = v));
    this.state.categories$.subscribe(cats => {
      this.categories = cats;
      if (cats.length > 0 && !this.selectedCategory) {
        this.selectedCategory = cats[0]; // erste Kat als Default
      }
    });
  }

  // --- Athleten-CSV importieren ------------------------------------------

  triggerFileDialog(): void {
    this.fileInput.nativeElement.click();
  }

  async onFileChange(ev: Event): Promise<void> {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const text = await file.text();
    this.state.loadCsvText(text, ';');
    this.errorMsg = '';
    input.value = '';
  }

  // --- PDFs erzeugen ------------------------------------------------------

  async generateNotesheets(): Promise<void> {
    this.errorMsg = '';
    const csv = this.state.getRawCsv();
    if (!csv) return;

    this.busyNotesheets = true;
    try {
      const res = await firstValueFrom(this.api.uploadCsvAndGetMergedPdf(csv, ';'));
      this.downloadPdfResponse(res, 'Notenblaetter_merged.pdf');
    } catch (err: any) {
      this.errorMsg = err?.message ?? 'Fehler beim Erzeugen der Notenblätter.';
    } finally {
      this.busyNotesheets = false;
    }
  }

  async generateRanking(): Promise<void> {
    this.errorMsg = '';
    if (!this.imported) return;

    const payload = this.buildRankingPayload({ onlySelectedCategory: true, normalizeNotes: true });

    this.busyRanking = true;
    try {
      const res = await firstValueFrom(this.api.generateRankingPdf(payload));
      this.downloadPdfResponse(res, 'Rangliste.pdf');
    } catch (err: any) {
      this.errorMsg =
        err?.error?.title ||
        err?.message ||
        'Fehler beim Erzeugen der Rangliste.';
    } finally {
      this.busyRanking = false;
    }
  }

  // --- Zwischenstand exportieren / importieren ----------------------------

  exportData(): void {
    this.busyExport = true;
    try {
      const payload = this.buildRankingPayload({ onlySelectedCategory: false, normalizeNotes: false });
      const json = JSON.stringify(payload, null, 2);
      const blob = new Blob([json], { type: 'application/json;charset=utf-8;' });
      const fileBase =
        payload.competitionName?.trim().replace(/[^a-z0-9_-]+/gi, '_') || 'noten_export';
      downloadBlob(blob, `${fileBase}.json`);
    } finally {
      this.busyExport = false;
    }
  }

  importData(): void {
    this.busyImport = true;
    try {
      this.importFileInput.nativeElement.click();
    } finally {
      this.busyImport = false;
    }
  }

  async onImportFileChange(ev: Event): Promise<void> {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    try {
      const text = await file.text();
      const payload = JSON.parse(text) as RankingRequestDto;
      if (!payload?.athletes?.length) {
        throw new Error('Keine Athleten im Import gefunden.');
      }

      this.state.loadFromImportedAthletes(payload.athletes);
      this.errorMsg = '';
    } catch (err: any) {
      this.errorMsg =
        err?.message || 'Import fehlgeschlagen. Bitte gültige JSON-Datei wählen.';
    } finally {
      input.value = '';
    }
  }

  // --- Alles löschen --------------------------------------------------------

  onClearClick(): void {
    const confirmed = window.confirm(
      'Bist du sicher, dass alle Daten (CSV, Noten, Ranglisten) gelöscht werden sollen?'
    );
    if (!confirmed) return;

    this.state.clear();
    this.errorMsg = '';
    this.selectedCategory = null;
  }

  // --- Helfer ---------------------------------------------------------------

  /**
   * Baut den Request aus den aktuellen Gruppen.
   * - onlySelectedCategory: nur Athleten der gewählten Kategorie (für die Rangliste).
   * - normalizeNotes: Noten von D1..D6 auf Geräte-Position drehen (für die Rangliste);
   *   der Export behält die rohe D1..D6-Reihenfolge.
   */
  private buildRankingPayload(options: {
    onlySelectedCategory: boolean;
    normalizeNotes: boolean;
  }): RankingRequestDto {
    const groups = this.state.getGroupsSnapshot();
    const selected = options.onlySelectedCategory ? this.selectedCategory : null;

    const athletes: RankingAthleteDto[] = [];
    groups.forEach((group, gIndex) => {
      group.forEach(a => {
        if (selected && a.kat !== selected) {
          return; // andere Kategorie ignorieren
        }

        athletes.push({
          nachname: a.nachname,
          vorname: a.vorname,
          jg: a.jg,
          verein: a.verein,
          kat: a.kat,
          groupIndex: gIndex + 1,
          notes: options.normalizeNotes
            ? this.normalizeNotesForApparatus(a.notes, gIndex)
            : a.notes,
        });
      });
    });

    return {
      competitionName: 'GeKu Rangliste', // später dynamisch
      apparatus: APPARATUS,
      athletes,
    };
  }

  /** Dreht die Noten so, dass Index 0 immer "Boden", 1 "Pferd", ... ist.
   *  Hintergrund: Gruppe 2 startet am Pferd, deshalb muss deren D1-Note
   *  an Position "Pferd" (Index 1) landen. */
  private normalizeNotesForApparatus(
    notes: RankingAthleteDto['notes'],
    groupOffset: number
  ): RankingAthleteDto['notes'] {
    const padded = Array.from({ length: NOTE_COUNT }, (_, i) => notes?.[i] ?? {});
    const shift = ((groupOffset % NOTE_COUNT) + NOTE_COUNT) % NOTE_COUNT; // sicher positiv

    const normalized = Array.from({ length: NOTE_COUNT }, () => ({}));
    for (let i = 0; i < NOTE_COUNT; i++) {
      const apparatusIdx = (shift + i) % NOTE_COUNT;
      normalized[apparatusIdx] = padded[i];
    }
    return normalized;
  }

  /** Speichert die PDF-Antwort als Download; Dateiname aus Content-Disposition, sonst Fallback. */
  private downloadPdfResponse(res: HttpResponse<Blob>, fallbackFilename: string): void {
    const blob = res.body!;
    const filename = filenameFromContentDisposition(
      res.headers.get('Content-Disposition'),
      fallbackFilename
    );
    downloadBlob(blob, filename);
  }
}
