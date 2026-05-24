import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <button mat-icon-button class="theme-toggle" type="button"
            (click)="theme.toggle()"
            [matTooltip]="(theme.dark$ | async) ? 'Светлая тема' : 'Тёмная тема'">
      <mat-icon>{{ (theme.dark$ | async) ? 'light_mode' : 'dark_mode' }}</mat-icon>
    </button>
  `,
  styles: [`
    .theme-toggle {
      position: fixed; top: 16px; right: 16px; z-index: 10;
    }
  `]
})
export class ThemeToggleComponent {
  constructor(public theme: ThemeService) {}
}
