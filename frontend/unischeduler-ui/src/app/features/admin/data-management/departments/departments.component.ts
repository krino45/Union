import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../../core/services/api.service';
import { Department, Faculty } from '../../../../core/models';

@Component({
  selector: 'app-departments',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSnackBarModule, MatTooltipModule, MatProgressSpinnerModule
  ],
  template: `
    <div class="page-header">
      <h1>Кафедры</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>
      <table mat-table [dataSource]="departments" class="full-width" *ngIf="!loading">
        <ng-container matColumnDef="shortCode">
          <th mat-header-cell *matHeaderCellDef>Код</th>
          <td mat-cell *matCellDef="let d">{{ d.shortCode }}</td>
        </ng-container>
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let d">{{ d.name }}</td>
        </ng-container>
        <ng-container matColumnDef="faculty">
          <th mat-header-cell *matHeaderCellDef>Факультет</th>
          <td mat-cell *matCellDef="let d">{{ d.facultyName }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let d">
            <button mat-icon-button (click)="openDialog(d)" matTooltip="Редактировать"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button color="warn" (click)="delete(d)" matTooltip="Удалить"><mat-icon>delete</mat-icon></button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;"></tr>
      </table>
    </mat-card>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
  `]
})
export class DepartmentsComponent implements OnInit {
  departments: Department[] = [];
  faculties: Faculty[] = [];
  loading = true;
  columns = ['shortCode', 'name', 'faculty', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getFaculties().subscribe(f => { this.faculties = f; this.load(); });
  }

  load(): void {
    this.loading = true;
    this.api.getDepartments().subscribe({
      next: data => { this.departments = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  openDialog(dept: Department | null): void {
    const ref = this.dialog.open(DepartmentDialogComponent, { data: { dept, faculties: this.faculties }, width: '460px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (dept) {
        this.api.updateDepartment(dept.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createDepartment(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  delete(dept: Department): void {
    if (!confirm(`Удалить кафедру "${dept.name}"? Связанные аудитории и дисциплины потеряют привязку.`)) return;
    this.api.deleteDepartment(dept.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}

@Component({
  selector: 'app-department-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.dept ? 'Редактировать' : 'Добавить' }} кафедру</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Факультет</mat-label>
          <mat-select formControlName="facultyId">
            <mat-option *ngFor="let f of data.faculties" [value]="f.id">{{ f.shortCode }} — {{ f.name }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Код кафедры</mat-label>
          <input matInput formControlName="shortCode" placeholder="Каф. ИВТ">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Название кафедры</mat-label>
          <input matInput formControlName="name" placeholder="Кафедра информатики и вычислительной техники">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.full-width { width: 100%; margin-bottom: 8px; } .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 380px; }`]
})
export class DepartmentDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<DepartmentDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { dept: Department | null; faculties: Faculty[] },
    private fb: FormBuilder
  ) {
    const d = data.dept;
    this.form = this.fb.group({
      facultyId: [d?.facultyId ?? '', Validators.required],
      shortCode: [d?.shortCode ?? '', Validators.required],
      name: [d?.name ?? '', Validators.required]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
