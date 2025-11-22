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

  imported = false;
  busyNotesheets = false;
  busyRanking = false;
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
}
