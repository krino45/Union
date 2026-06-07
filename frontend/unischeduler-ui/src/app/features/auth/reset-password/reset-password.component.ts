import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../core/services/api.service';
import { ThemeToggleComponent } from '../../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-reset-password',
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
          <mat-card-title>Новый пароль</mat-card-title>
          <mat-card-subtitle>Юниран</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <div *ngIf="!token" class="status">
            <mat-icon class="status-icon warn">error_outline</mat-icon>
            <p>Ссылка для сброса пароля недействительна.</p>
            <button mat-button color="primary" routerLink="/forgot-password">Запросить заново</button>
          </div>

          <div *ngIf="token && done" class="status">
            <mat-icon class="status-icon ok">check_circle</mat-icon>
            <p>Пароль изменён. Теперь вы можете войти с новым паролем.</p>
            <button mat-raised-button color="primary" routerLink="/login">Войти</button>
          </div>

          <form *ngIf="token && !done" [formGroup]="form" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Новый пароль</mat-label>
              <input matInput [type]="hide ? 'password' : 'text'" formControlName="password" autocomplete="new-password">
              <button mat-icon-button matSuffix type="button" (click)="hide = !hide">
                <mat-icon>{{ hide ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
              <mat-hint>Минимум 6 символов</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Повторите пароль</mat-label>
              <input matInput [type]="hide ? 'password' : 'text'" formControlName="confirm" autocomplete="new-password">
            </mat-form-field>
            <div *ngIf="form.hasError('mismatch') && form.get('confirm')?.touched" class="error-msg">Пароли не совпадают.</div>
            <div *ngIf="error" class="error-msg">{{ error }}</div>
            <button mat-raised-button color="primary" type="submit"
                    [disabled]="form.invalid || loading" class="full-width submit-btn">
              <mat-spinner *ngIf="loading" diameter="20"></mat-spinner>
              <span *ngIf="!loading">Сохранить пароль</span>
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .auth-container {
      display: flex; justify-content: center; align-items: center;
      min-height: 100vh; background: #f5f5f5; padding: 16px;
    }
    .auth-card { width: 100%; max-width: 380px; padding: 16px; }
    .full-width { width: 100%; margin-bottom: 12px; }
    .error-msg { color: #f44336; font-size: 13px; margin-bottom: 12px; }
    .submit-btn { height: 44px; }
    .status { text-align: center; padding: 16px 8px; display: flex; flex-direction: column; align-items: center; gap: 8px; }
    .status-icon { font-size: 48px; width: 48px; height: 48px; }
    .status-icon.ok { color: #2e7d32; }
    .status-icon.warn { color: #e65100; }
  `]
})
export class ResetPasswordComponent implements OnInit {
  form: FormGroup;
  token: string | null = null;
  loading = false;
  done = false;
  hide = true;
  error = '';

  constructor(private fb: FormBuilder, private api: ApiService, private route: ActivatedRoute, private router: Router) {
    this.form = this.fb.group({
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirm: ['', [Validators.required]]
    }, { validators: ResetPasswordComponent.matchPasswords });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token');
  }

  private static matchPasswords(group: AbstractControl): ValidationErrors | null {
    return group.get('password')?.value === group.get('confirm')?.value ? null : { mismatch: true };
  }

  onSubmit(): void {
    if (this.form.invalid || !this.token) return;
    this.loading = true;
    this.error = '';
    this.api.resetPassword(this.token, this.form.value.password).subscribe({
      next: () => { this.loading = false; this.done = true; },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.title || err.error?.message || 'Не удалось сменить пароль. Возможно, ссылка устарела.';
      }
    });
  }
}
