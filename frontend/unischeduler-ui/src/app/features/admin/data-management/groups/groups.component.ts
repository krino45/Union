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
import { StudentGroup, Faculty } from '../../../../core/models';
import { DegreeType, RussianDayOfWeek } from '../../../../core/models/enums';

@Component({
  selector: 'app-groups',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSnackBarModule, MatTooltipModule, MatProgressSpinnerModule
  ],
  template: `
    <div class="page-header">
      <h1>Группы</h1>
      <div class="header-actions">
        <button mat-stroked-button (click)="openPromoteDialog()" matTooltip="Перевести группы на следующий курс">
          <mat-icon>arrow_upward</mat-icon> Следующий курс
        </button>
        <button mat-raised-button color="primary" (click)="openDialog(null)">
          <mat-icon>add</mat-icon> Добавить
        </button>
      </div>
    </div>

    <mat-card>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>
      <table mat-table [dataSource]="groups" class="full-width" *ngIf="!loading">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Группа</th>
          <td mat-cell *matCellDef="let g">{{ g.name }}</td>
        </ng-container>
        <ng-container matColumnDef="faculty">
          <th mat-header-cell *matHeaderCellDef>Факультет</th>
          <td mat-cell *matCellDef="let g">{{ g.facultyName }}</td>
        </ng-container>
        <ng-container matColumnDef="year">
          <th mat-header-cell *matHeaderCellDef>Курс</th>
          <td mat-cell *matCellDef="let g">{{ g.year }}</td>
        </ng-container>
        <ng-container matColumnDef="degree">
          <th mat-header-cell *matHeaderCellDef>Уровень</th>
          <td mat-cell *matCellDef="let g">{{ degreeLabel(g.degreeType) }}</td>
        </ng-container>
        <ng-container matColumnDef="specialty">
          <th mat-header-cell *matHeaderCellDef>Специальность / Профиль</th>
          <td mat-cell *matCellDef="let g">{{ g.specialty }}</td>
        </ng-container>
        <ng-container matColumnDef="count">
          <th mat-header-cell *matHeaderCellDef>Студентов</th>
          <td mat-cell *matCellDef="let g">{{ g.studentCount }}</td>
        </ng-container>
        <ng-container matColumnDef="blockedDays">
          <th mat-header-cell *matHeaderCellDef>Заблок. дни</th>
          <td mat-cell *matCellDef="let g">
            <span *ngIf="!g.blockedDays?.length" class="no-block">—</span>
            <span *ngFor="let d of g.blockedDays" class="day-chip">{{ dayLabel(d) }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let g">
            <button mat-icon-button (click)="openDialog(g)" matTooltip="Редактировать"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button color="warn" (click)="delete(g)" matTooltip="Удалить"><mat-icon>delete</mat-icon></button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;"></tr>
      </table>
    </mat-card>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .header-actions { display: flex; gap: 8px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
    .no-block { color: #ccc; }
    .day-chip { display: inline-block; font-size: 11px; background: #fce4ec; color: #880e4f; border-radius: 4px; padding: 1px 5px; margin: 1px; }
  `]
})
export class GroupsComponent implements OnInit {
  groups: StudentGroup[] = [];
  faculties: Faculty[] = [];
  loading = true;
  columns = ['name', 'faculty', 'year', 'degree', 'specialty', 'count', 'blockedDays', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getFaculties().subscribe(f => { this.faculties = f; this.load(); });
  }

  load(): void {
    this.loading = true;
    this.api.getGroups().subscribe({
      next: data => { this.groups = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  dayLabel(day: RussianDayOfWeek): string {
    const map: Record<string, string> = {
      Monday: 'Пн', Tuesday: 'Вт', Wednesday: 'Ср',
      Thursday: 'Чт', Friday: 'Пт', Saturday: 'Сб'
    };
    return map[day] ?? day;
  }

  openDialog(group: StudentGroup | null): void {
    const ref = this.dialog.open(GroupDialogComponent, { data: { group, faculties: this.faculties }, width: '480px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (group) {
        this.api.updateGroup(group.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createGroup(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  openPromoteDialog(): void {
    const ref = this.dialog.open(PromoteGroupsDialogComponent, { data: this.faculties, width: '400px' });
    ref.afterClosed().subscribe(result => {
      if (result === undefined) return;
      this.api.promoteGroups(result).subscribe({
        next: (res) => {
          this.load();
          this.snackBar.open(`Переведено групп: ${res.promoted}`, 'OK', { duration: 3000 });
        },
        error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  delete(group: StudentGroup): void {
    if (!confirm(`Удалить группу "${group.name}"?`)) return;
    this.api.deleteGroup(group.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  degreeLabel(type: DegreeType): string {
    switch (type) {
      case DegreeType.Bachelor: return 'Бакалавриат';
      case DegreeType.Specialist: return 'Специалитет';
      case DegreeType.Master: return 'Магистратура';
      case DegreeType.Postgraduate: return 'Аспирантура';
      case DegreeType.SecondaryVocational: return 'СПО';
      default: return type;
    }
  }
}

@Component({
  selector: 'app-group-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.group ? 'Редактировать' : 'Добавить' }} группу</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Факультет</mat-label>
          <mat-select formControlName="facultyId">
            <mat-option *ngFor="let f of data.faculties" [value]="f.id">{{ f.shortCode }} — {{ f.name }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Название группы</mat-label>
          <input matInput formControlName="name" placeholder="ИВТ-21">
        </mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Уровень образования</mat-label>
            <mat-select formControlName="degreeType">
              <mat-option value="Bachelor">Бакалавриат</mat-option>
              <mat-option value="Specialist">Специалитет</mat-option>
              <mat-option value="Master">Магистратура</mat-option>
              <mat-option value="Postgraduate">Аспирантура</mat-option>
              <mat-option value="SecondaryVocational">СПО</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Курс</mat-label>
            <input matInput type="number" formControlName="year" min="1" max="6">
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Специальность / Профиль</mat-label>
          <input matInput formControlName="specialty">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Количество студентов</mat-label>
          <input matInput type="number" formControlName="studentCount" min="1">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Заблокированные дни (жёсткое ограничение)</mat-label>
          <mat-select formControlName="blockedDays" multiple>
            <mat-option value="Monday">Понедельник</mat-option>
            <mat-option value="Tuesday">Вторник</mat-option>
            <mat-option value="Wednesday">Среда</mat-option>
            <mat-option value="Thursday">Четверг</mat-option>
            <mat-option value="Friday">Пятница</mat-option>
            <mat-option value="Saturday">Суббота</mat-option>
          </mat-select>
          <mat-hint>Занятия не будут ставиться в выбранные дни</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.full-width { width: 100%; margin-bottom: 8px; } .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 380px; } .row { display: flex; gap: 8px; } .flex1 { flex: 1; }`]
})
export class GroupDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<GroupDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { group: StudentGroup | null; faculties: Faculty[] },
    private fb: FormBuilder
  ) {
    const g = data.group;
    this.form = this.fb.group({
      facultyId: [g?.facultyId ?? '', Validators.required],
      name: [g?.name ?? '', Validators.required],
      degreeType: [g?.degreeType ?? DegreeType.Bachelor, Validators.required],
      year: [g?.year ?? 1, [Validators.required, Validators.min(1), Validators.max(6)]],
      specialty: [g?.specialty ?? '', Validators.required],
      studentCount: [g?.studentCount ?? 25, [Validators.required, Validators.min(1)]],
      blockedDays: [g?.blockedDays ?? []]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}

@Component({
  selector: 'app-promote-groups-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatFormFieldModule, MatSelectModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Перевести на следующий курс</h2>
    <mat-dialog-content>
      <p>Все группы с курса N будут переведены на N+1. Группы на 6-м курсе не затрагиваются.</p>
      <mat-form-field appearance="outline" style="width:100%; margin-top:8px">
        <mat-label>Факультет</mat-label>
        <mat-select [(value)]="selectedFacultyId">
          <mat-option [value]="null">— Все факультеты —</mat-option>
          <mat-option *ngFor="let f of data" [value]="f.id">{{ f.shortCode }} — {{ f.name }}</mat-option>
        </mat-select>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" (click)="submit()">Перевести</button>
    </mat-dialog-actions>
  `,
  styles: []
})
export class PromoteGroupsDialogComponent {
  selectedFacultyId: string | null = null;

  constructor(
    private dialogRef: MatDialogRef<PromoteGroupsDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Faculty[]
  ) {}

  submit(): void {
    this.dialogRef.close(this.selectedFacultyId);
  }
}
