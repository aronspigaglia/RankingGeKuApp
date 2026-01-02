import { Component, ElementRef, ViewChild } from '@angular/core';
import { NotesStateService } from '../../services/notes-state.service';
import { NotesheetsApiService } from '../../services/notesheets-api.service';
import { RankingAthleteDto, RankingRequestDto } from '../../models/ranking-request';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';



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

  readonly apparatus = ['Boden', 'Pferd', 'Ring', 'Sprung', 'Barren', 'Reck'];

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

  triggerFileDialog() {
    this.fileInput.nativeElement.click();
  }

  async onFileChange(ev: Event) {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const text = await file.text();
    this.state.loadCsvText(text, ';');
    this.errorMsg = '';
    input.value = '';
  }

  clearAll() {
    this.state.clear();
    this.errorMsg = '';
    this.selectedCategory = null;
  }
  onClearClick() {
  const confirmed = window.confirm(
    'Bist du sicher, dass alle Daten (CSV, Noten, Ranglisten) gelöscht werden sollen?'
  );

  if (!confirmed) {
    return;
  }

  this.clearAll();
}

  async generateNotesheets() {
    this.errorMsg = '';
    const csv = this.state.getRawCsv();
    if (!csv) return;

    this.busyNotesheets = true;
    try {
      const res = await firstValueFrom(
        this.api.uploadCsvAndGetMergedPdf(csv, ';')
      );
      const blob = res.body!;
      const cd = res.headers.get('Content-Disposition') || '';
      const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i.exec(cd);
      const filename = match?.[1] ?? 'Notenblaetter_merged.pdf';

      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (err: any) {
      this.errorMsg = err?.message ?? 'Fehler beim Erzeugen der Notenblätter.';
    } finally {
      this.busyNotesheets = false;
    }
  }
  private buildRankingPayload(): RankingRequestDto {
    const groups = this.state.getGroupsSnapshot();
    const selected = this.selectedCategory;

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
          notes: a.notes,
        });
      });
    });

    return {
      competitionName: 'GeKu Rangliste', // später dynamisch
      apparatus: this.apparatus,
      athletes,
    };
  }
  private getWholeRankingPayload(): RankingRequestDto {
    const groups = this.state.getGroupsSnapshot();
    const selected = this.selectedCategory;

    const athletes: RankingAthleteDto[] = [];
    groups.forEach((group, gIndex) => {
      group.forEach(a => {
        athletes.push({
          nachname: a.nachname,
          vorname: a.vorname,
          jg: a.jg,
          verein: a.verein,
          kat: a.kat,
          groupIndex: gIndex + 1,
          notes: a.notes,
        });
      });
    });

    return {
      competitionName: 'GeKu Rangliste',
      apparatus: this.apparatus,
      athletes,
    };
  }

  async generateRanking() {
    this.errorMsg = '';
    if (!this.imported) return;

    const payload = this.buildRankingPayload();
    
    this.busyRanking = true;
    try {
      const res = await firstValueFrom(this.api.generateRankingPdf(payload));
      const blob = res.body!;
      const cd = res.headers.get('Content-Disposition') || '';
      const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i.exec(cd);
      const filename = match?.[1] ?? 'Rangliste.pdf';
      
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (err: any) {
      this.errorMsg =
      err?.error?.title ||
      err?.message ||
      'Fehler beim Erzeugen der Rangliste (Backend noch nicht fertig?).';
    } finally {
      this.busyRanking = false;
    }
  }

  exportData() {
    this.busyExport = true;
    try {
      const payload = this.getWholeRankingPayload();
      const json = JSON.stringify(payload, null, 2);
      const blob = new Blob([json], { type: 'application/json;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const fileBase =
        payload.competitionName?.trim().replace(/[^a-z0-9_-]+/gi, '_') ||
        'noten_export';
      const a = document.createElement('a');
      a.href = url;
      a.download = `${fileBase}.json`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } finally {
      this.busyExport = false;
    }
  }

  importData() {
    this.busyImport = true;
    try {
      this.importFileInput.nativeElement.click();
    } finally {
      this.busyImport = false;
    }
  }

  async onImportFileChange(ev: Event) {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    try {
      const text = await file.text();
      const payload = JSON.parse(text) as RankingRequestDto;
      if (!payload?.athletes?.length) {
        throw new Error('Keine Athleten im Import gefunden.');
      }

      const delimiter = ';';
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
        'END6',
      ];

      const lines = [header.join(delimiter)];
      payload.athletes.forEach(a => {
        const notes = Array.from({ length: 6 }, (_, i) => a.notes?.[i] ?? {});
        const noteParts = notes.flatMap(n => [n.dNote ?? '', n.endNote ?? '']);

        lines.push(
          [
            a.groupIndex,
            a.nachname,
            a.vorname,
            a.jg,
            a.verein,
            a.kat,
            ...noteParts,
          ]
            .map(v => (v ?? '').toString().replace(/\r?\n/g, ' ').trim())
            .join(delimiter)
        );
      });

      const csv = lines.join('\n');
      this.state.loadNotesCsvText(csv, delimiter);
      this.errorMsg = '';
    } catch (err: any) {
      this.errorMsg =
        err?.message || 'Import fehlgeschlagen. Bitte gültige JSON-Datei wählen.';
    } finally {
      input.value = '';
    }
  }
}
