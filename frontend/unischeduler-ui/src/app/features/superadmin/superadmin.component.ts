import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSelectModule } from '@angular/material/select';
import { MatDividerModule } from '@angular/material/divider';
import { Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { UniversityAccess } from '../../core/models';

@Component({
  selector: 'app-superadmin',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSnackBarModule, MatTabsModule, MatSelectModule, MatDividerModule
  ],
  template: `
    <div class="superadmin-page">
      <div class="page-header">
        <div class="header-left">
          <mat-icon>admin_panel_settings</mat-icon>
          <h1>Управление системой</h1>
        </div>
        <button mat-stroked-button (click)="goToUniSelect()">
          <mat-icon>arrow_back</mat-icon> К выбору университета
        </button>
      </div>

      <mat-card>
        <mat-card-header>
          <mat-card-title>Университеты</mat-card-title>
          <span class="spacer"></span>
          <button mat-raised-button color="primary" (click)="openCreateDialog()">
            <mat-icon>add</mat-icon> Добавить
          </button>
        </mat-card-header>
        <mat-card-content>
          <table mat-table [dataSource]="universities" class="full-table">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Название</th>
              <td mat-cell *matCellDef="let u">{{ u.name }}</td>
            </ng-container>
            <ng-container matColumnDef="shortName">
              <th mat-header-cell *matHeaderCellDef>Краткое</th>
              <td mat-cell *matCellDef="let u">{{ u.shortName }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>Действия</th>
              <td mat-cell *matCellDef="let u">
                <button mat-icon-button (click)="enterUniversity(u)" matTooltip="Войти как администратор">
                  <mat-icon>login</mat-icon>
                </button>
                <button mat-icon-button (click)="openUsersDialog(u)" matTooltip="Управление доступом">
                  <mat-icon>people</mat-icon>
                </button>
                <button mat-icon-button (click)="openEditDialog(u)" matTooltip="Редактировать">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button color="warn" (click)="delete(u)" matTooltip="Удалить">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .superadmin-page { max-width: 960px; margin: 0 auto; }
    .page-header {
      display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px;
    }
    .header-left { display: flex; align-items: center; gap: 8px; }
    .header-left mat-icon { font-size: 28px; color: #7b1fa2; }
    h1 { margin: 0; }
    .spacer { flex: 1; }
    mat-card-header { display: flex; align-items: center; padding: 16px 16px 8px; }
    .full-table { width: 100%; }
  `]
})
export class SuperAdminComponent implements OnInit {
  universities: any[] = [];
  columns = ['name', 'shortName', 'actions'];

  constructor(
    private api: ApiService,
    public auth: AuthService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private router: Router
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getUniversities().subscribe(data => this.universities = data);
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(UniversityDialogComponent, { data: null, width: '400px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.api.createUniversity(result).subscribe({
        next: () => { this.load(); this.snackBar.open('Университет создан', 'OK', { duration: 2000 }); },
        error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  openEditDialog(u: any): void {
    const ref = this.dialog.open(UniversityDialogComponent, { data: u, width: '400px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.api.updateUniversity(u.id, result).subscribe({
        next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
        error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  openUsersDialog(u: any): void {
    this.dialog.open(UniversityUsersDialogComponent, { data: u, width: '600px' });
  }

  enterUniversity(u: any): void {
    const access: UniversityAccess = {
      universityId: u.id,
      universityName: u.name,
      shortName: u.shortName,
      logoUrl: u.logoUrl,
      role: 'Admin'
    };
    this.auth.selectUniversity(access);
    this.router.navigate(['/admin/schedules']);
  }

  delete(u: any): void {
    if (!confirm(`Удалить университет "${u.name}"? Все данные будут удалены.`)) return;
    this.api.deleteUniversity(u.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  goToUniSelect(): void {
    this.auth.clearUniversitySelection();
    this.router.navigate(['/select-university']);
  }
}

@Component({
  selector: 'app-university-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Редактировать университет' : 'Новый университет' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Название</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Краткое название</mat-label>
          <input matInput formControlName="shortName">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>URL логотипа (необязательно)</mat-label>
          <input matInput formControlName="logoUrl">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="save()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: ['.form { display: flex; flex-direction: column; min-width: 320px; } .full { width: 100%; }']
})
export class UniversityDialogComponent {
  form: FormGroup;
  constructor(
    private fb: FormBuilder,
    private ref: MatDialogRef<UniversityDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {
    this.form = this.fb.group({
      name: [data?.name ?? '', [Validators.required, Validators.maxLength(300)]],
      shortName: [data?.shortName ?? '', [Validators.required, Validators.maxLength(50)]],
      logoUrl: [data?.logoUrl ?? '']
    });
  }
  save(): void {
    if (this.form.valid) this.ref.close(this.form.value);
  }
}

@Component({
  selector: 'app-university-users-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatTableModule, MatIconModule,
    MatSelectModule, MatSnackBarModule
  ],
  template: `
    <h2 mat-dialog-title>Доступ: {{ data.name }}</h2>
    <mat-dialog-content>
      <table mat-table [dataSource]="users" class="full-table">
        <ng-container matColumnDef="username">
          <th mat-header-cell *matHeaderCellDef>Пользователь</th>
          <td mat-cell *matCellDef="let u">{{ u.username }}</td>
        </ng-container>
        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>Роль</th>
          <td mat-cell *matCellDef="let u">{{ u.universityRole }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let u">
            <button mat-icon-button color="warn" (click)="revoke(u)"><mat-icon>person_remove</mat-icon></button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="cols"></tr>
        <tr mat-row *matRowDef="let row; columns: cols;"></tr>
      </table>
      <div class="assign-form">
        <mat-form-field appearance="outline">
          <mat-label>ID пользователя</mat-label>
          <input matInput [(ngModel)]="assignUserId">
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Роль</mat-label>
          <mat-select [(ngModel)]="assignRole">
            <mat-option value="Admin">Администратор</mat-option>
            <mat-option value="Teacher">Преподаватель</mat-option>
          </mat-select>
        </mat-form-field>
        <button mat-raised-button color="primary" (click)="assign()">Добавить</button>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Закрыть</button>
    </mat-dialog-actions>
  `,
  styles: [
    '.full-table { width: 100%; min-width: 400px; }',
    '.assign-form { display: flex; gap: 8px; align-items: center; margin-top: 16px; flex-wrap: wrap; }'
  ]
})
export class UniversityUsersDialogComponent implements OnInit {
  users: any[] = [];
  cols = ['username', 'role', 'actions'];
  assignUserId = '';
  assignRole = 'Teacher';

  constructor(
    private api: ApiService,
    private snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getUniversityUsers(this.data.id).subscribe(u => this.users = u);
  }

  assign(): void {
    if (!this.assignUserId) return;
    this.api.assignUniversityUser(this.data.id, this.assignUserId, this.assignRole).subscribe({
      next: () => { this.load(); this.snackBar.open('Доступ предоставлен', 'OK', { duration: 2000 }); },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  revoke(u: any): void {
    this.api.revokeUniversityUser(this.data.id, u.userId).subscribe({
      next: () => { this.load(); this.snackBar.open('Доступ отозван', 'OK', { duration: 2000 }); },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}
