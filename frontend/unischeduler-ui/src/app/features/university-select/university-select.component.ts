import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';
import { UniversityAccess } from '../../core/models';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-university-select',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, ThemeToggleComponent],
  template: `
    <div class="select-container">
      <app-theme-toggle></app-theme-toggle>
      <div class="header">
        <mat-icon class="logo-icon">school</mat-icon>
        <h1>Юниан</h1>
        <p class="subtitle">Выберите университет</p>
      </div>
      <div class="cards">
        <mat-card class="uni-card" *ngFor="let u of auth.currentUser?.universities"
                  (click)="select(u)" matRipple>
          <mat-card-content>
            <mat-icon class="uni-icon">domain</mat-icon>
            <div class="uni-info">
              <div class="uni-name">{{ u.universityName }}</div>
              <div class="uni-short">{{ u.shortName }}</div>
              <div class="uni-role">{{ u.role === 'Admin' ? 'Администратор' : 'Преподаватель' }}</div>
            </div>
          </mat-card-content>
        </mat-card>
        <mat-card class="uni-card superadmin-card" *ngIf="auth.isSuperAdmin"
                  (click)="goSuperAdmin()" matRipple>
          <mat-card-content>
            <mat-icon class="uni-icon">admin_panel_settings</mat-icon>
            <div class="uni-info">
              <div class="uni-name">Управление системой</div>
              <div class="uni-role">Суперадминистратор</div>
            </div>
          </mat-card-content>
        </mat-card>
      </div>
      <button mat-button class="logout-link" (click)="auth.logout()">
        <mat-icon>logout</mat-icon> Выйти
      </button>
    </div>
  `,
  styles: [`
    .select-container {
      min-height: 100vh;
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      background: #f5f5f5; padding: 24px;
    }
    .header { text-align: center; margin-bottom: 32px; }
    .logo-icon { font-size: 56px; width: 56px; height: 56px; color: #1565c0; }
    h1 { font-size: 28px; font-weight: 700; margin: 8px 0 4px; }
    .subtitle { color: #666; margin: 0; }
    .cards {
      display: flex; flex-wrap: wrap; gap: 16px; justify-content: center; max-width: 800px;
    }
    .uni-card {
      width: 220px; cursor: pointer; transition: transform 0.15s, box-shadow 0.15s;
    }
    .uni-card:hover { transform: translateY(-2px); box-shadow: 0 8px 24px rgba(0,0,0,0.12); }
    mat-card-content {
      display: flex; align-items: center; gap: 12px; padding: 16px !important;
    }
    .uni-icon { font-size: 36px; width: 36px; height: 36px; color: #1565c0; }
    .superadmin-card .uni-icon { color: #7b1fa2; }
    .uni-name { font-weight: 600; font-size: 15px; }
    .uni-short { font-size: 12px; color: #888; }
    .uni-role { font-size: 12px; color: #1565c0; margin-top: 2px; }
    .superadmin-card .uni-role { color: #7b1fa2; }
    .logout-link { margin-top: 32px; color: #888; }
  `]
})
export class UniversitySelectComponent {
  constructor(public auth: AuthService, private router: Router) {}

  select(u: UniversityAccess): void {
    this.auth.selectUniversity(u);
    if (u.role === 'Admin') this.router.navigate(['/admin/schedules']);
    else this.router.navigate(['/teacher/my-schedule']);
  }

  goSuperAdmin(): void {
    this.router.navigate(['/superadmin']);
  }
}
