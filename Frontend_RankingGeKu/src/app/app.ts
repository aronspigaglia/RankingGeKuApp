import { Component, signal } from '@angular/core';
import { HeaderComponent } from './core/header/header-component';
import { SidebarComponent } from './core/sidebar/sidebar-component';
import { ContentComponent } from "./features/content/content-component";
import { NotesStateService } from './services/notes-state.service';

@Component({
  selector: 'app-root',
  imports: [HeaderComponent, SidebarComponent, ContentComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = signal('Frontend_RankingGeKu');
  constructor(private state: NotesStateService) {
    this.state.loadFromStorage();   // <- lädt CSV + Gruppen + Noten, falls vorhanden
  }
}
