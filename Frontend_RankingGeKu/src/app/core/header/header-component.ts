import { Component } from '@angular/core';

@Component({
  selector: 'app-header-component',
  imports: [],
  templateUrl: './header-component.html',
  styleUrl: './header-component.scss',
})
export class HeaderComponent {
  showEasterEgg = false;

  easterEgg(): void {
    this.showEasterEgg = true;
  }

  closeEasterEgg(): void {
    this.showEasterEgg = false;
  }
}
