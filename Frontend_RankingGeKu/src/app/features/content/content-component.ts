import { Component } from '@angular/core';
import { NotesStateService } from '../../services/notes-state.service';
import { Athlete } from '../../models/athlete';
import { FormsModule } from '@angular/forms';


@Component({
  selector: 'app-content-component',
  imports: [FormsModule],
  templateUrl: './content-component.html',
  styleUrl: './content-component.scss',

  
})

export class ContentComponent {
  
  tabs = ['D1', 'D2', 'D3', 'D4', 'D5', 'D6'];
  activeTabIndex = 0;
  
  imported = false;
  groups: Athlete[][] = [];
  readonly apparatus = ['Boden', 'Pferd', 'Ring', 'Sprung', 'Barren', 'Reck']; // <— NEU

  constructor(public state: NotesStateService) {
    this.state.imported$.subscribe(v => this.imported = v);
    this.state.groups$.subscribe(g => this.groups = g);
  }

  setActive(i: number) { this.activeTabIndex = i; }

  getApparatusName(groupIndex: number): string {
    const len = this.apparatus.length;
    if (len === 0) return '';
    // groupIndex: 0.., activeTabIndex: 0..5 (D1..D6)
    const idx = (groupIndex + this.activeTabIndex) % len;
    return this.apparatus[idx];
  }

  // convenience for template
  trackByIndex = (_: number, __: any) => _;

}
