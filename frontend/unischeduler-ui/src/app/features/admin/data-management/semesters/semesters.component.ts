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
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../../../core/services/api.service';
import { Semester } from '../../../../core/models';

@Component({
  selector: 'app-semesters',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSnackBarModule, MatTooltipModule
  ],
  template: `
    <div class="page-header">
      <h1>Семестры</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <table mat-table [dataSource]="semesters" class="full-width">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let s" data-label="Название">{{ s.name }}</td>
        </ng-container>
        <ng-container matColumnDef="dates">
          <th mat-header-cell *matHeaderCellDef>Период</th>
          <td mat-cell *matCellDef="let s" data-label="Период">
            {{ s.startDate | date:'dd.MM.yyyy' }} – {{ s.endDate | date:'dd.MM.yyyy' }}
          </td>
        </ng-container>
        <ng-container matColumnDef="weeks">
          <th mat-header-cell *matHeaderCellDef>Недель</th>
          <td mat-cell *matCellDef="let s" data-label="Недель">{{ s.totalWeeks }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let s">
            <button mat-icon-button (click)="openDialog(s)" matTooltip="Редактировать"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button color="warn" (click)="delete(s)" matTooltip="Удалить"><mat-icon>delete</mat-icon></button>
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
  `]
})
export class SemestersComponent implements OnInit {
  semesters: Semester[] = [];
  columns = ['name', 'dates', 'weeks', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getSemesters().subscribe(data => this.semesters = data);
  }

  openDialog(semester: Semester | null): void {
    const ref = this.dialog.open(SemesterDialogComponent, { data: semester, width: '400px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (semester) {
        this.api.updateSemester(semester.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createSemester(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  delete(semester: Semester): void {
    if (!confirm(`Удалить семестр "${semester.name}"?`)) return;
    this.api.deleteSemester(semester.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}

@Component({
  selector: 'app-semester-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Редактировать' : 'Добавить' }} семестр</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Название</mat-label>
          <input matInput formControlName="name" placeholder="Осенний 2024-2025">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Дата начала</mat-label>
          <input matInput type="date" formControlName="startDate">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Дата окончания</mat-label>
          <input matInput type="date" formControlName="endDate">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Количество недель</mat-label>
          <input matInput type="number" formControlName="totalWeeks" min="1" max="52">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.full-width { width: 100%; margin-bottom: 8px; } .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 300px; }`]
})
export class SemesterDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<SemesterDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Semester | null,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      name: [data?.name ?? '', Validators.required],
      startDate: [data?.startDate?.substring(0, 10) ?? '', Validators.required],
      endDate: [data?.endDate?.substring(0, 10) ?? '', Validators.required],
      totalWeeks: [data?.totalWeeks ?? 18, [Validators.required, Validators.min(1)]]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
