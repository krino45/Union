import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../core/services/api.service';
import { ThemeToggleComponent } from '../../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    ThemeToggleComponent
  ],
  template: `
    <div class="auth-container">
      <app-theme-toggle></app-theme-toggle>
      <mat-card class="auth-card">
        <mat-card-header>
          <mat-icon mat-card-avatar>lock_reset</mat-icon>
          <mat-card-title>Сброс пароля</mat-card-title>
          <mat-card-subtitle>Юниран</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <div *ngIf="sent" class="status">
            <mat-icon class="status-icon ok">mark_email_read</mat-icon>
            <p>Если аккаунт с таким e-mail существует, мы отправили на него ссылку для сброса пароля.</p>
            <p class="muted">Проверьте почту (ссылка действует 1 час).</p>
            <button mat-button color="primary" routerLink="/login">К входу</button>
          </div>

          <form *ngIf="!sent" [formGroup]="form" (ngSubmit)="onSubmit()">
            <p class="muted small">Укажите e-mail, привязанный к аккаунту — пришлём ссылку для смены пароля.</p>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="email">
              <mat-icon matSuffix>mail</mat-icon>
            </mat-form-field>
            <div *ngIf="error" class="error-msg">{{ error }}</div>
            <button mat-raised-button color="primary" type="submit"
                    [disabled]="form.invalid || loading" class="full-width submit-btn">
              <mat-spinner *ngIf="loading" diameter="20"></mat-spinner>
              <span *ngIf="!loading">Отправить ссылку</span>
            </button>
            <button mat-button type="button" routerLink="/login" class="full-width back-btn">Назад ко входу</button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .auth-container {
      display: flex; justify-content: center; align-items: center;
      min-height: 100vh; background: #f5f5f5;
    }
    .auth-card { width: 380px; padding: 16px; }
    .full-width { width: 100%; margin-bottom: 12px; }
    .error-msg { color: #f44336; font-size: 13px; margin-bottom: 12px; }
    .submit-btn { height: 44px; }
    .back-btn { margin-top: 4px; }
    .status { text-align: center; padding: 16px 8px; display: flex; flex-direction: column; align-items: center; gap: 8px; }
    .status-icon { font-size: 48px; width: 48px; height: 48px; }
    .status-icon.ok { color: #2e7d32; }
    .muted { color: #888; }
    .small { font-size: 12px; margin: 0 0 12px; }
  `]
})
export class ForgotPasswordComponent {
  form: FormGroup;
  loading = false;
  sent = false;
  error = '';

  constructor(private fb: FormBuilder, private api: ApiService) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    this.error = '';
    this.api.forgotPassword(this.form.value.email).subscribe({
      next: () => { this.loading = false; this.sent = true; },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.title || err.error?.message || 'Не удалось отправить ссылку. Попробуйте позже.';
      }
    });
  }
}
