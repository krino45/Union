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
import { Faculty } from '../../../../core/models';

@Component({
  selector: 'app-faculties',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSnackBarModule, MatTooltipModule
  ],
  template: `
    <div class="page-header">
      <h1>Факультеты</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <table mat-table [dataSource]="faculties" class="full-width">
        <ng-container matColumnDef="shortCode">
          <th mat-header-cell *matHeaderCellDef>Код</th>
          <td mat-cell *matCellDef="let f"><strong>{{ f.shortCode }}</strong></td>
        </ng-container>
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let f">{{ f.name }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let f">
            <button mat-icon-button (click)="openDialog(f)" matTooltip="Редактировать">
              <mat-icon>edit</mat-icon>
            </button>
            <button mat-icon-button color="warn" (click)="delete(f)" matTooltip="Удалить">
              <mat-icon>delete</mat-icon>
            </button>
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
export class FacultiesComponent implements OnInit {
  faculties: Faculty[] = [];
  columns = ['shortCode', 'name', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getFaculties().subscribe(data => this.faculties = data);
  }

  openDialog(faculty: Faculty | null): void {
    const ref = this.dialog.open(FacultyDialogComponent, { data: faculty, width: '400px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (faculty) {
        this.api.updateFaculty(faculty.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createFaculty(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  delete(faculty: Faculty): void {
    if (!confirm(`Удалить факультет "${faculty.name}"?`)) return;
    this.api.deleteFaculty(faculty.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}

@Component({
  selector: 'app-faculty-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Редактировать' : 'Добавить' }} факультет</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Код (сокращение)</mat-label>
          <input matInput formControlName="shortCode" placeholder="ФИВТ, ФЭМ...">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Название</mat-label>
          <input matInput formControlName="name" placeholder="Факультет информационных технологий">
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
export class FacultyDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<FacultyDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Faculty | null,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      shortCode: [data?.shortCode ?? '', Validators.required],
      name: [data?.name ?? '', Validators.required]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
