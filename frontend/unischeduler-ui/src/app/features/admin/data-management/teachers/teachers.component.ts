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
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Teacher, Subject } from '../../../../core/models';
import { LessonTypePipe } from '../../../../shared/pipes/lesson-type.pipe';

@Component({
  selector: 'app-teachers',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSnackBarModule, MatTooltipModule, LessonTypePipe, MatProgressSpinnerModule
  ],
  template: `
    <div class="page-header">
      <h1>Преподаватели</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>
      <table mat-table [dataSource]="teachers" class="full-width" *ngIf="!loading">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>ФИО</th>
          <td mat-cell *matCellDef="let t">{{ t.displayName }}</td>
        </ng-container>
        <ng-container matColumnDef="email">
          <th mat-header-cell *matHeaderCellDef>Email</th>
          <td mat-cell *matCellDef="let t">{{ t.email }}</td>
        </ng-container>
        <ng-container matColumnDef="subjects">
          <th mat-header-cell *matHeaderCellDef>Дисциплин</th>
          <td mat-cell *matCellDef="let t">{{ t.subjects?.length || 0 }}</td>
        </ng-container>
        <ng-container matColumnDef="load">
          <th mat-header-cell *matHeaderCellDef>Нагрузка</th>
          <td mat-cell *matCellDef="let t">
            <span [matTooltip]="loadTooltip(t.loadHoursPerWeek)" class="load-cell">
              {{ t.loadHoursPerWeek ?? 0 }} ч/нед
            </span>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let t">
            <button mat-icon-button (click)="openDialog(t)" matTooltip="Редактировать">
              <mat-icon>edit</mat-icon>
            </button>
            <button mat-icon-button (click)="openSubjectsDialog(t)" matTooltip="Дисциплины">
              <mat-icon>book</mat-icon>
            </button>
            <button mat-icon-button (click)="openAvailabilityDialog(t)" matTooltip="Занятость">
              <mat-icon>event_busy</mat-icon>
            </button>
            <button mat-icon-button (click)="openInviteDialog(t)" matTooltip="Пригласить">
              <mat-icon>mail</mat-icon>
            </button>
            <button mat-icon-button color="warn" (click)="delete(t)" matTooltip="Удалить">
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
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
    .load-cell { font-weight: 500; }
  `]
})
export class TeachersComponent implements OnInit {
  teachers: Teacher[] = [];
  subjects: Subject[] = [];
  loading = true;
  columns = ['name', 'email', 'subjects', 'load', 'actions'];

  loadTooltip(hours: number | undefined): string {
    const h = hours ?? 0;
    if (h === 0) return 'Нет занятий в опубликованных расписаниях';
    return `${h} академических часов в неделю (опубликованные расписания)`;
  }

  openAvailabilityDialog(teacher: Teacher): void {
    this.dialog.open(TeacherAvailabilityViewDialogComponent, {
      data: teacher, width: '640px'
    });
  }

  openInviteDialog(teacher: Teacher): void {
    this.dialog.open(InviteTeacherDialogComponent, {
      data: teacher, width: '440px'
    });
  }

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    forkJoin({
      teachers: this.api.getTeachers(),
      subjects: this.api.getSubjects()
    }).subscribe({
      next: ({ teachers, subjects }) => {
        this.teachers = teachers;
        this.subjects = subjects;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  load(): void {
    this.loading = true;
    this.api.getTeachers().subscribe({
      next: data => { this.teachers = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  openDialog(teacher: Teacher | null): void {
    const ref = this.dialog.open(TeacherDialogComponent, { data: teacher, width: '440px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (teacher) {
        this.api.updateTeacher(teacher.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createTeacher(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  openSubjectsDialog(teacher: Teacher): void {
    const ref = this.dialog.open(TeacherSubjectsDialogComponent, {
      data: { teacher, subjects: this.subjects }, width: '560px'
    });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.api.updateTeacherSubjects(teacher.id, result).subscribe({
        next: () => { this.load(); this.snackBar.open('Дисциплины обновлены', 'OK', { duration: 2000 }); },
        error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  delete(teacher: Teacher): void {
    if (!confirm(`Удалить преподавателя "${teacher.displayName}"?`)) return;
    this.api.deleteTeacher(teacher.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}

@Component({
  selector: 'app-teacher-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Редактировать' : 'Добавить' }} преподавателя</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Фамилия</mat-label>
          <input matInput formControlName="lastName">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Имя</mat-label>
          <input matInput formControlName="firstName">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Отчество</mat-label>
          <input matInput formControlName="middleName">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.full-width { width: 100%; margin-bottom: 8px; } .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 320px; }`]
})
export class TeacherDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<TeacherDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Teacher | null,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      lastName: [data?.lastName ?? '', Validators.required],
      firstName: [data?.firstName ?? '', Validators.required],
      middleName: [data?.middleName ?? ''],
      email: [data?.email ?? '', [Validators.required, Validators.email]]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}

@Component({
  selector: 'app-teacher-subjects-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, MatButtonModule, MatSelectModule, MatFormFieldModule, MatIconModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Дисциплины: {{ data.teacher.displayName }}</h2>
    <mat-dialog-content>
      <div *ngFor="let row of rows; let i = index" class="subject-row">
        <mat-form-field appearance="outline" class="subject-field">
          <mat-label>Дисциплина</mat-label>
          <mat-select [(ngModel)]="row.subjectId">
            <mat-option *ngFor="let s of data.subjects" [value]="s.id">{{ s.name }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="type-field">
          <mat-label>Тип</mat-label>
          <mat-select [(ngModel)]="row.lessonType">
            <mat-option value="Lecture">Лекция</mat-option>
            <mat-option value="Practical">Практика</mat-option>
            <mat-option value="Lab">Лабораторная</mat-option>
            <mat-option value="Seminar">Семинар</mat-option>
          </mat-select>
        </mat-form-field>
        <button mat-icon-button color="warn" (click)="removeRow(i)"><mat-icon>remove_circle</mat-icon></button>
      </div>
      <button mat-button (click)="addRow()"><mat-icon>add</mat-icon> Добавить</button>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.subject-row { display: flex; align-items: center; gap: 8px; } .subject-field { flex: 2; } .type-field { flex: 1; }`]
})
export class TeacherSubjectsDialogComponent {
  rows: { subjectId: string; lessonType: string }[] = [];

  constructor(
    private dialogRef: MatDialogRef<TeacherSubjectsDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { teacher: Teacher; subjects: Subject[] }
  ) {
    this.rows = data.teacher.subjects?.map(s => ({ subjectId: s.subjectId, lessonType: s.lessonType })) ?? [];
  }

  addRow(): void {
    this.rows.push({ subjectId: '', lessonType: 'Lecture' });
  }

  removeRow(i: number): void {
    this.rows.splice(i, 1);
  }

  submit(): void {
    this.dialogRef.close(this.rows.filter(r => r.subjectId));
  }
}

@Component({
  selector: 'app-teacher-availability-view-dialog',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatDialogModule, MatIconModule, MatTooltipModule],
  template: `
    <h2 mat-dialog-title>Занятость: {{ data.displayName }}</h2>
    <mat-dialog-content>
      <p class="hint">
        <mat-icon>info</mat-icon>
        Просмотр заявок преподавателя о недоступности. Редактирование доступно преподавателю в личном кабинете.
      </p>
      <div *ngIf="!loading && slots.length === 0" class="empty">
        Нет заявок о недоступности.
      </div>
      <table class="grid" *ngIf="!loading && slots.length > 0">
        <thead>
          <tr>
            <th></th>
            <th *ngFor="let d of days">{{ d.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let p of pairs">
            <th>{{ p }} пара</th>
            <td *ngFor="let d of days"
                [class.blocked]="hasSlot(d.value, p)"
                [matTooltip]="slotTooltip(d.value, p)">
              <span *ngIf="hasSlot(d.value, p)">✕</span>
            </td>
          </tr>
        </tbody>
      </table>
      <div *ngIf="loading" class="loading">Загрузка…</div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Закрыть</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .hint { display: flex; align-items: center; gap: 6px; color: #666; font-size: 13px; margin: 0 0 12px; }
    .hint mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .empty { padding: 24px; text-align: center; color: #999; }
    .grid { width: 100%; border-collapse: collapse; }
    .grid th, .grid td { border: 1px solid #e0e0e0; padding: 8px 12px; text-align: center; min-width: 64px; }
    .grid thead th { background: #fafafa; font-weight: 600; }
    .grid .blocked { background: #ffebee; color: #b71c1c; font-weight: 700; cursor: help; }
    :host-context(body.dark-mode) .grid th, :host-context(body.dark-mode) .grid td { border-color: #444; }
    :host-context(body.dark-mode) .grid thead th { background: #2a2a2a; }
    :host-context(body.dark-mode) .grid .blocked { background: #4a1818; color: #ef9a9a; }
    .loading { padding: 24px; text-align: center; color: #999; }
  `]
})
export class TeacherAvailabilityViewDialogComponent implements OnInit {
  slots: any[] = [];
  loading = true;
  days = [
    { value: 'Monday', label: 'Пн' },
    { value: 'Tuesday', label: 'Вт' },
    { value: 'Wednesday', label: 'Ср' },
    { value: 'Thursday', label: 'Чт' },
    { value: 'Friday', label: 'Пт' },
    { value: 'Saturday', label: 'Сб' }
  ];
  pairs = [1, 2, 3, 4, 5, 6, 7];

  constructor(
    private api: ApiService,
    @Inject(MAT_DIALOG_DATA) public data: Teacher
  ) {}

  ngOnInit(): void {
    this.api.getAvailability(this.data.id).subscribe({
      next: s => { this.slots = s; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  hasSlot(day: string, pair: number): boolean {
    return this.slots.some(s => s.dayOfWeek === day && s.pairNumber === pair);
  }

  slotTooltip(day: string, pair: number): string {
    const matches = this.slots.filter(s => s.dayOfWeek === day && s.pairNumber === pair);
    return matches.map(m => `${m.weekType === 'Both' ? 'Кажд. нед.' : m.weekType}${m.reason ? ': ' + m.reason : ''}`).join('; ');
  }
}

@Component({
  selector: 'app-invite-teacher-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>Пригласить преподавателя</h2>
    <mat-dialog-content>
      <p class="teacher-info">
        <mat-icon>person</mat-icon> {{ data.displayName }}
      </p>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>E-mail</mat-label>
        <input matInput type="email" [(ngModel)]="email" required>
        <mat-hint>Письмо со ссылкой будет отправлено на этот адрес</mat-hint>
      </mat-form-field>
      <p class="note">
        После принятия приглашения аккаунт будет привязан к этому преподавателю.
        Если у пользователя уже есть аккаунт, он сможет принять приглашение, не создавая новый.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="!email || sending" (click)="send()">
        <mat-icon>send</mat-icon> Отправить
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .teacher-info { display: flex; align-items: center; gap: 6px; font-weight: 500; color: #555; margin: 0 0 12px; }
    .full-width { width: 100%; }
    .note { font-size: 12px; color: #888; margin: 4px 0 0; }
  `]
})
export class InviteTeacherDialogComponent {
  email: string;
  sending = false;

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private snackBar: MatSnackBar,
    private dialogRef: MatDialogRef<InviteTeacherDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Teacher
  ) {
    this.email = data.email ?? '';
  }

  send(): void {
    const uniId = this.auth.currentUniversity?.universityId;
    if (!uniId || !this.email) return;
    this.sending = true;
    this.api.createInvitation(uniId, this.email, 'Teacher', this.data.id).subscribe({
      next: () => {
        this.sending = false;
        this.snackBar.open('Приглашение отправлено (ссылка в консоли сервера)', 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: e => {
        this.sending = false;
        this.snackBar.open(e.error?.title || 'Не удалось отправить приглашение', 'OK', { duration: 4000 });
      }
    });
  }
}
