import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule
  ],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-header>
          <mat-icon mat-card-avatar>school</mat-icon>
          <mat-card-title>UniScheduler</mat-card-title>
          <mat-card-subtitle>Вход в систему</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Логин</mat-label>
              <input matInput formControlName="username" autocomplete="username">
              <mat-icon matSuffix>person</mat-icon>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Пароль</mat-label>
              <input matInput [type]="hidePassword ? 'password' : 'text'"
                     formControlName="password" autocomplete="current-password">
              <button mat-icon-button matSuffix type="button" (click)="hidePassword = !hidePassword">
                <mat-icon>{{ hidePassword ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
            </mat-form-field>
            <div *ngIf="error" class="error-msg">{{ error }}</div>
            <button mat-raised-button color="primary" type="submit"
                    [disabled]="form.invalid || loading" class="full-width">
              <mat-spinner *ngIf="loading" diameter="20"></mat-spinner>
              <span *ngIf="!loading">Войти</span>
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex; justify-content: center; align-items: center;
      min-height: 100vh; background: #f5f5f5;
    }
    .login-card { width: 360px; padding: 16px; }
    .full-width { width: 100%; margin-bottom: 12px; }
    .error-msg { color: #f44336; font-size: 13px; margin-bottom: 12px; }
    button[type=submit] { height: 44px; }
  `]
})
export class LoginComponent {
  form: FormGroup;
  loading = false;
  hidePassword = true;
  error = '';

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router) {
    this.form = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required]
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    this.error = '';
    this.auth.login(this.form.value).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.role === 'SuperAdmin') {
          this.router.navigate(['/superadmin']);
        } else if (res.universities?.length === 1) {
          this.auth.selectUniversity(res.universities[0]);
          if (res.universities[0].role === 'Admin') this.router.navigate(['/admin/schedules']);
          else this.router.navigate(['/teacher/my-schedule']);
        } else {
          this.router.navigate(['/select-university']);
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.title || err.error?.message || 'Неверный логин или пароль';
      }
    });
  }
}
