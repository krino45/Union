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
import { ApiService } from '../../../../core/services/api.service';
import { Subject } from '../../../../core/models';
import { Term } from '../../../../core/models/enums';

const CURRENT_YEAR = new Date().getFullYear();

@Component({
  selector: 'app-subjects',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSnackBarModule, MatTooltipModule
  ],
  template: `
    <div class="page-header">
      <h1>Дисциплины</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <table mat-table [dataSource]="subjects" class="full-width">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let s">{{ s.name }} <span class="short">({{ s.shortName }})</span></td>
        </ng-container>
        <ng-container matColumnDef="period">
          <th mat-header-cell *matHeaderCellDef>Год / Семестр</th>
          <td mat-cell *matCellDef="let s">{{ s.academicYear }} / {{ s.term === 'First' ? '1' : '2' }}</td>
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
    <p class="hint">Часы по дисциплинам задаются в <strong>Учебных планах</strong>.</p>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .short { color: #888; font-size: 12px; }
    .hint { color: #888; font-size: 12px; margin-top: 8px; }
  `]
})
export class SubjectsComponent implements OnInit {
  subjects: Subject[] = [];
  columns = ['name', 'period', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getSubjects().subscribe(data => this.subjects = data);
  }

  openDialog(subject: Subject | null): void {
    const ref = this.dialog.open(SubjectDialogComponent, { data: subject, width: '440px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (subject) {
        this.api.updateSubject(subject.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createSubject(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  delete(subject: Subject): void {
    if (!confirm(`Удалить дисциплину "${subject.name}"?`)) return;
    this.api.deleteSubject(subject.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}

@Component({
  selector: 'app-subject-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Редактировать' : 'Добавить' }} дисциплину</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <div class="row">
          <mat-form-field appearance="outline" class="flex2">
            <mat-label>Название</mat-label>
            <input matInput formControlName="name">
          </mat-form-field>
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Сокращение</mat-label>
            <input matInput formControlName="shortName">
          </mat-form-field>
        </div>
        <div class="row">
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Учебный год</mat-label>
            <input matInput type="number" formControlName="academicYear" [min]="2020" [max]="2040">
          </mat-form-field>
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Семестр</mat-label>
            <mat-select formControlName="term">
              <mat-option value="First">1-й (осенний)</mat-option>
              <mat-option value="Second">2-й (весенний)</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 380px; gap: 4px; } .row { display: flex; gap: 8px; } .flex1 { flex: 1; } .flex2 { flex: 2; }`]
})
export class SubjectDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<SubjectDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Subject | null,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      name: [data?.name ?? '', Validators.required],
      shortName: [data?.shortName ?? '', Validators.required],
      academicYear: [data?.academicYear ?? CURRENT_YEAR, [Validators.required, Validators.min(2020)]],
      term: [data?.term ?? Term.First, Validators.required]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
