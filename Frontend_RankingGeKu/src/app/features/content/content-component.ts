import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NotesStateService } from '../../services/notes-state.service';
import { Athlete } from '../../models/athlete';
import { APPARATUS, isEpa, isEpaExcludedApparatus } from '../../models/gymnastics';

@Component({
  selector: 'app-content-component',
  imports: [FormsModule],
  templateUrl: './content-component.html',
  styleUrl: './content-component.scss',
})
export class ContentComponent {
  readonly tabs = ['D1', 'D2', 'D3', 'D4', 'D5', 'D6'];
  activeTabIndex = 0;

  imported = false;
  groups: Athlete[][] = [];

  constructor(public state: NotesStateService) {
    this.state.imported$.subscribe(v => (this.imported = v));
    this.state.groups$.subscribe(g => (this.groups = g));
  }

  setActive(i: number): void {
    this.activeTabIndex = i;
  }

  /** Gerät der Gruppe im aktiven Durchgang (Geräte rotieren pro Gruppe). */
  getApparatusName(groupIndex: number): string {
    const idx = (groupIndex + this.activeTabIndex) % APPARATUS.length;
    return APPARATUS[idx];
  }

  /** Gruppe ausblenden, wenn alle Athleten EPA sind und das Gerät Pferd/Ring ist. */
  isGroupHidden(group: Athlete[], groupIndex: number): boolean {
    if (!isEpaExcludedApparatus(this.getApparatusName(groupIndex))) return false;
    return group.every(a => isEpa(a.kat));
  }

  /** Noten-Eingabe sperren, wenn ein EPA-Athlet am Pferd/Ring wäre. */
  isApparatusDisabledFor(kat: string | null | undefined, groupIndex: number): boolean {
    return isEpa(kat) && isEpaExcludedApparatus(this.getApparatusName(groupIndex));
  }
}
