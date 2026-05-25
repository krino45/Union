import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from './core/services/auth.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    MatToolbarModule, MatSidenavModule, MatListModule,
    MatButtonModule, MatIconModule, MatDividerModule, MatTooltipModule
  ],
  template: `
    <mat-sidenav-container class="app-container">
      <mat-sidenav #sidenav mode="side" [opened]="auth.isAuthenticated && !!auth.currentUniversity" class="sidenav">
        <div class="sidenav-header" [class.clickable]="auth.canSwitchUniversity"
             (click)="auth.canSwitchUniversity ? switchUniversity() : null"
             [matTooltip]="auth.canSwitchUniversity ? 'Сменить университет' : ''">
          <mat-icon>school</mat-icon>
          <div class="header-text">
            <div class="app-name">Юниран</div>
            <div class="uni-name" *ngIf="auth.currentUniversity">{{ auth.currentUniversity.shortName }}</div>
          </div>
          <mat-icon class="switch-icon" *ngIf="auth.canSwitchUniversity">swap_horiz</mat-icon>
        </div>
        <mat-divider></mat-divider>

        <ng-container *ngIf="auth.isAdmin">
          <mat-nav-list>
            <h3 matSubheader>Расписание</h3>
            <a mat-list-item routerLink="/admin/schedules" routerLinkActive="active">
              <mat-icon matListItemIcon>calendar_today</mat-icon>
              <span matListItemTitle>Расписания</span>
            </a>
          </mat-nav-list>
          <mat-divider></mat-divider>
          <mat-nav-list>
            <h3 matSubheader>Данные</h3>
            <a mat-list-item routerLink="/admin/buildings" routerLinkActive="active">
              <mat-icon matListItemIcon>apartment</mat-icon>
              <span matListItemTitle>Корпуса</span>
            </a>
            <a mat-list-item routerLink="/admin/rooms" routerLinkActive="active">
              <mat-icon matListItemIcon>meeting_room</mat-icon>
              <span matListItemTitle>Аудитории</span>
            </a>
            <a mat-list-item routerLink="/admin/teachers" routerLinkActive="active">
              <mat-icon matListItemIcon>person</mat-icon>
              <span matListItemTitle>Преподаватели</span>
            </a>
            <a mat-list-item routerLink="/admin/subjects" routerLinkActive="active">
              <mat-icon matListItemIcon>book</mat-icon>
              <span matListItemTitle>Дисциплины</span>
            </a>
            <a mat-list-item routerLink="/admin/groups" routerLinkActive="active">
              <mat-icon matListItemIcon>groups</mat-icon>
              <span matListItemTitle>Группы</span>
            </a>
            <a mat-list-item routerLink="/admin/faculties" routerLinkActive="active">
              <mat-icon matListItemIcon>domain</mat-icon>
              <span matListItemTitle>Факультеты</span>
            </a>
            <a mat-list-item routerLink="/admin/departments" routerLinkActive="active">
              <mat-icon matListItemIcon>account_balance</mat-icon>
              <span matListItemTitle>Кафедры</span>
            </a>
          </mat-nav-list>
          <mat-divider></mat-divider>
          <mat-nav-list>
            <h3 matSubheader>Учебные планы</h3>
            <a mat-list-item routerLink="/admin/calendar-plans" routerLinkActive="active">
              <mat-icon matListItemIcon>event_note</mat-icon>
              <span matListItemTitle>Календарные планы</span>
            </a>
            <a mat-list-item routerLink="/admin/study-plans" routerLinkActive="active">
              <mat-icon matListItemIcon>assignment</mat-icon>
              <span matListItemTitle>Учебные планы</span>
            </a>
          </mat-nav-list>
          <mat-divider></mat-divider>
          <mat-nav-list>
            <h3 matSubheader>Управление</h3>
            <a mat-list-item routerLink="/admin/reschedule-requests" routerLinkActive="active">
              <mat-icon matListItemIcon>swap_horiz</mat-icon>
              <span matListItemTitle>Перенос занятий</span>
            </a>
            <a mat-list-item routerLink="/admin/excel" routerLinkActive="active">
              <mat-icon matListItemIcon>table_chart</mat-icon>
              <span matListItemTitle>Excel</span>
            </a>
          </mat-nav-list>
        </ng-container>

        <ng-container *ngIf="auth.isTeacher">
          <mat-nav-list>
            <h3 matSubheader>Личный кабинет</h3>
            <a mat-list-item routerLink="/teacher/my-schedule" routerLinkActive="active">
              <mat-icon matListItemIcon>calendar_today</mat-icon>
              <span matListItemTitle>Моё расписание</span>
            </a>
            <a mat-list-item routerLink="/teacher/availability" routerLinkActive="active">
              <mat-icon matListItemIcon>event_busy</mat-icon>
              <span matListItemTitle>Занятость</span>
            </a>
            <a mat-list-item routerLink="/teacher/reschedule" routerLinkActive="active">
              <mat-icon matListItemIcon>swap_horiz</mat-icon>
              <span matListItemTitle>Запрос переноса</span>
            </a>
          </mat-nav-list>
        </ng-container>

        <div class="sidenav-footer">
          <mat-divider></mat-divider>
          <div class="user-info" *ngIf="auth.currentUser">
            <mat-icon>account_circle</mat-icon>
            <span>{{ auth.currentUser.username }}</span>
          </div>
          <button mat-button (click)="auth.logout()" class="logout-btn">
            <mat-icon>logout</mat-icon>
            Выйти
          </button>
        </div>
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar color="primary" *ngIf="auth.isAuthenticated && auth.currentUniversity">
          <span class="uni-title">
            {{ auth.currentUniversity?.universityName || 'Юниран' }}
          </span>
          <button mat-button routerLink="/admin/floor-plan" routerLinkActive="active-header-link"
                  *ngIf="auth.isAdmin && auth.currentUniversity" class="header-floorplan-btn">
            <mat-icon>map</mat-icon>
            Редактор планировок
          </button>
          <button mat-icon-button (click)="theme.toggle()" [matTooltip]="(theme.dark$ | async) ? 'Светлая тема' : 'Тёмная тема'" class="dark-toggle">
            <mat-icon>{{ (theme.dark$ | async) ? 'light_mode' : 'dark_mode' }}</mat-icon>
          </button>
          <span class="role-badge" *ngIf="auth.isSuperAdmin">Суперадмин</span>
          <span class="role-badge" *ngIf="!auth.isSuperAdmin && auth.currentUniversity">
            {{ auth.isAdmin ? 'Администратор' : 'Преподаватель' }}
          </span>
        </mat-toolbar>
        <div class="content">
          <router-outlet></router-outlet>
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .app-container { height: 100vh; }
    .sidenav { width: 240px; display: flex; flex-direction: column; }
    .sidenav-header {
      display: flex; align-items: center; gap: 8px;
      padding: 16px; font-size: 18px; font-weight: 600;
    }
    .sidenav-header.clickable { cursor: pointer; transition: background 0.15s; }
    .sidenav-header.clickable:hover { background: rgba(0,0,0,0.04); }
    .header-text { flex: 1; min-width: 0; }
    .app-name { font-size: 16px; font-weight: 700; }
    .uni-name { font-size: 11px; font-weight: 400; color: #666; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .switch-icon { font-size: 16px; width: 16px; height: 16px; color: #888; }
    .sidenav-footer { margin-top: auto; padding: 8px; }
    .user-info { display: flex; align-items: center; gap: 8px; padding: 8px; font-size: 14px; }
    .logout-btn { width: 100%; }
    .role-badge { font-size: 13px; opacity: 0.85; }
    .uni-title { font-size: 15px; font-weight: 600; flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .header-floorplan-btn { color: #fff; margin-right: 12px; }
    .header-floorplan-btn.active-header-link { background: rgba(255,255,255,0.18); }
    .dark-toggle { color: #fff; margin-right: 8px; }
    .content { padding: 24px; }
    mat-nav-list a.active { background: rgba(25,118,210,0.12); }
    h3[matSubheader] { color: #888; font-size: 11px; font-weight: 600; letter-spacing: 0.5px; }
  `]
})
export class AppComponent {
  constructor(
    public auth: AuthService,
    public theme: ThemeService,
    private router: Router
  ) {
    let wasAuthenticated = this.auth.isAuthenticated;
    this.auth.currentUser$.subscribe(user => {
      const isAuthenticated = !!user;
      if (wasAuthenticated && !isAuthenticated) {
        this.router.navigate(['/login']);
      }
      wasAuthenticated = isAuthenticated;
    });
  }

  switchUniversity(): void {
    this.auth.clearUniversitySelection();
    this.router.navigate(['/select-university']);
  }
}
