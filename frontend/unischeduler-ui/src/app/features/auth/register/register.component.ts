import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { InvitationInfo } from '../../../core/models';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatChipsModule
  ],
  template: `
    <div class="register-container">
      <mat-card class="register-card">
        <mat-card-header>
          <mat-icon mat-card-avatar>school</mat-icon>
          <mat-card-title>Юниан</mat-card-title>
          <mat-card-subtitle>{{ subtitle }}</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <!-- No token -->
          <div *ngIf="!token" class="status">
            <mat-icon class="status-icon">lock</mat-icon>
            <p>Регистрация в системе доступна только по приглашению.</p>
            <button mat-button color="primary" routerLink="/login">К входу</button>
          </div>

          <!-- Loading invitation info -->
          <div *ngIf="token && loadingInfo" class="status">
            <mat-spinner diameter="32"></mat-spinner>
            <p>Проверка приглашения…</p>
          </div>

          <!-- Invalid / expired -->
          <div *ngIf="token && !loadingInfo && info?.mode === 'invalid'" class="status">
            <mat-icon class="status-icon warn">error_outline</mat-icon>
            <p>Приглашение не найдено, истёк срок действия или оно уже использовано.</p>
            <button mat-button color="primary" routerLink="/login">К входу</button>
          </div>

          <!-- Wrong account -->
          <div *ngIf="token && !loadingInfo && info?.mode === 'wrong-account'" class="status">
            <mat-icon class="status-icon warn">block</mat-icon>
            <p *ngIf="auth.isAuthenticated">
              Это приглашение не предназначено для текущего аккаунта (<strong>{{ auth.currentUser?.username }}</strong>).
            </p>
            <p *ngIf="auth.isAuthenticated && info?.teacherAlreadyLinked">
              К указанному преподавателю уже привязан другой аккаунт. Выйдите и войдите под тем логином, чтобы принять приглашение.
            </p>
            <p *ngIf="!auth.isAuthenticated">
              Для этого приглашения требуется вход под существующим аккаунтом.
            </p>
            <div class="actions">
              <button mat-button (click)="auth.logout()" *ngIf="auth.isAuthenticated">
                <mat-icon>logout</mat-icon> Выйти
              </button>
              <button mat-button routerLink="/login" *ngIf="!auth.isAuthenticated">К входу</button>
            </div>
          </div>

          <!-- Accept (existing user) -->
          <div *ngIf="token && !loadingInfo && info?.mode === 'accept'" class="status">
            <mat-icon class="status-icon ok">how_to_reg</mat-icon>
            <p>
              Вы приглашены в университет <strong>{{ info?.universityName }}</strong>
              <span *ngIf="info?.universityRole"> как
                <mat-chip>{{ info!.universityRole === 'Admin' ? 'Администратор' : 'Преподаватель' }}</mat-chip>
              </span>
              <span *ngIf="info?.teacherDisplayName">
                ({{ info?.teacherDisplayName }})
              </span>.
            </p>
            <p class="muted">Вы вошли как <strong>{{ auth.currentUser?.username }}</strong>.</p>
            <div *ngIf="error" class="error-msg">{{ error }}</div>
            <div class="actions">
              <button mat-stroked-button (click)="auth.logout()">
                <mat-icon>logout</mat-icon> Не я
              </button>
              <button mat-raised-button color="primary" [disabled]="accepting" (click)="accept()">
                <mat-spinner *ngIf="accepting" diameter="20"></mat-spinner>
                <span *ngIf="!accepting">Принять приглашение</span>
              </button>
            </div>
          </div>

          <!-- Register (new user) -->
          <ng-container *ngIf="token && !loadingInfo && info?.mode === 'register'">
            <div class="invite-summary">
              <p>
                Создание аккаунта для <strong>{{ info?.universityName }}</strong>
                <span *ngIf="info?.universityRole"> —
                  <mat-chip>{{ info!.universityRole === 'Admin' ? 'Администратор' : 'Преподаватель' }}</mat-chip>
                </span>
                <span *ngIf="info?.teacherDisplayName">
                  ({{ info?.teacherDisplayName }})
                </span>
              </p>
            </div>
            <form [formGroup]="form" (ngSubmit)="onSubmit()">
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Логин</mat-label>
                <input matInput formControlName="username" autocomplete="username">
                <mat-icon matSuffix>person</mat-icon>
                <mat-hint>Минимум 3 символа</mat-hint>
              </mat-form-field>
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Пароль</mat-label>
                <input matInput [type]="hidePassword ? 'password' : 'text'"
                       formControlName="password" autocomplete="new-password">
                <button mat-icon-button matSuffix type="button" (click)="hidePassword = !hidePassword">
                  <mat-icon>{{ hidePassword ? 'visibility_off' : 'visibility' }}</mat-icon>
                </button>
                <mat-hint>Минимум 6 символов</mat-hint>
              </mat-form-field>
              <div *ngIf="error" class="error-msg">{{ error }}</div>
              <button mat-raised-button color="primary" type="submit"
                      [disabled]="form.invalid || loading" class="full-width submit-btn">
                <mat-spinner *ngIf="loading" diameter="20"></mat-spinner>
                <span *ngIf="!loading">Создать аккаунт</span>
              </button>
            </form>
          </ng-container>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .register-container {
      display: flex; justify-content: center; align-items: center;
      min-height: 100vh; background: #f5f5f5;
    }
    .register-card { width: 420px; padding: 16px; }
    .full-width { width: 100%; margin-bottom: 12px; }
    .error-msg { color: #f44336; font-size: 13px; margin-bottom: 12px; }
    .submit-btn { height: 44px; }
    .status { text-align: center; padding: 16px 8px; display: flex; flex-direction: column; align-items: center; gap: 8px; }
    .status-icon { font-size: 48px; width: 48px; height: 48px; color: #9e9e9e; }
    .status-icon.warn { color: #e65100; }
    .status-icon.ok { color: #2e7d32; }
    .status p { margin: 4px 0; color: #555; }
    .status .muted { color: #888; font-size: 12px; }
    .actions { display: flex; gap: 8px; margin-top: 8px; justify-content: center; flex-wrap: wrap; }
    .invite-summary { background: #e3f2fd; border-radius: 4px; padding: 10px 14px; margin: 0 0 16px; font-size: 13px; }
    .invite-summary p { margin: 0; }
    mat-chip { font-size: 11px; }
  `]
})
export class RegisterComponent implements OnInit {
  form: FormGroup;
  token: string | null = null;
  info: InvitationInfo | null = null;
  loadingInfo = false;
  loading = false;
  accepting = false;
  hidePassword = true;
  error = '';

  get subtitle(): string {
    if (!this.token) return 'Регистрация по приглашению';
    if (this.info?.mode === 'accept') return 'Принять приглашение';
    if (this.info?.mode === 'wrong-account') return 'Неверный аккаунт';
    if (this.info?.mode === 'invalid') return 'Приглашение недоступно';
    return 'Регистрация по приглашению';
  }

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    public auth: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      username: ['', [Validators.required, Validators.minLength(3)]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token');
    if (!this.token) return;

    this.loadingInfo = true;
    this.api.getInvitationInfo(this.token).subscribe({
      next: info => { this.info = info; this.loadingInfo = false; },
      error: () => { this.info = { isValid: false, universityName: null, universityShortName: null, universityRole: null, teacherDisplayName: null, teacherAlreadyLinked: false, mode: 'invalid' }; this.loadingInfo = false; }
    });
  }

  accept(): void {
    if (!this.token) return;
    this.accepting = true;
    this.error = '';
    this.auth.acceptInvitation(this.token).subscribe({
      next: res => {
        this.accepting = false;
        const access = res.universities?.find((u: any) => u.universityName === this.info?.universityName)
          ?? res.universities?.[0];
        if (access) {
          this.auth.selectUniversity(access);
          if (access.role === 'Admin') this.router.navigate(['/admin/schedules']);
          else this.router.navigate(['/teacher/my-schedule']);
        } else {
          this.router.navigate(['/select-university']);
        }
      },
      error: err => {
        this.accepting = false;
        this.error = err.error?.title || err.error?.message || 'Не удалось принять приглашение.';
      }
    });
  }

  onSubmit(): void {
    if (this.form.invalid || !this.token) return;
    this.loading = true;
    this.error = '';
    this.auth.registerFromInvitation(this.token, this.form.value.username, this.form.value.password).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.universities?.length === 1) {
          this.auth.selectUniversity(res.universities[0]);
          if (res.universities[0].role === 'Admin') this.router.navigate(['/admin/schedules']);
          else this.router.navigate(['/teacher/my-schedule']);
        } else {
          this.router.navigate(['/select-university']);
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.title || err.error?.message || 'Не удалось зарегистрироваться.';
      }
    });
  }
}
