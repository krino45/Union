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
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../../core/services/api.service';
import { Subject, Department, Room } from '../../../../core/models';
import { Term, LessonType, RoomType } from '../../../../core/models/enums';
import { SearchSelectComponent } from '../../../../shared/components/search-select.component';
import { forkJoin } from 'rxjs';

const CURRENT_YEAR = new Date().getFullYear();

@Component({
  selector: 'app-subjects',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSnackBarModule, MatTooltipModule, MatProgressSpinnerModule
  ],
  template: `
    <div class="page-header">
      <h1>Дисциплины</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <mat-form-field appearance="outline" class="search-field" *ngIf="!loading">
        <mat-icon matPrefix>search</mat-icon>
        <mat-label>Поиск по названию</mat-label>
        <input matInput [(ngModel)]="search" placeholder="Матанализ...">
        <button mat-icon-button matSuffix *ngIf="search" (click)="search = ''"><mat-icon>close</mat-icon></button>
      </mat-form-field>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>
      <table mat-table [dataSource]="filteredSubjects" class="full-width" *ngIf="!loading">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let s" data-label="Название">{{ s.name }} <span class="short">({{ s.shortName }})</span>
            <span *ngIf="s.allowsSubgroups" class="sg-badge" matTooltip="Лабораторные по подгруппам">×{{ s.subgroupCount }} подгр.</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="period">
          <th mat-header-cell *matHeaderCellDef>Год / Семестр</th>
          <td mat-cell *matCellDef="let s" data-label="Год / Семестр">{{ s.academicYear }} / {{ s.term === 'First' ? '1' : '2' }}</td>
        </ng-container>
        <ng-container matColumnDef="department">
          <th mat-header-cell *matHeaderCellDef>Кафедра</th>
          <td mat-cell *matCellDef="let s" data-label="Кафедра">
            <span *ngIf="s.departmentName" class="dept-name">{{ s.departmentName }}</span>
            <span *ngIf="!s.departmentName" class="no-dept">—</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let s">
            <button mat-icon-button (click)="openRoomBindings(s)" matTooltip="Закрепить аудитории"><mat-icon>meeting_room</mat-icon></button>
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
    .search-field { width: 100%; max-width: 420px; margin-top: 8px; margin-bottom: 8px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .short { color: #888; font-size: 12px; }
    .hint { color: #888; font-size: 12px; margin-top: 8px; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
    .dept-name { font-size: 12px; color: #555; }
    .no-dept { color: #ccc; }
    .sg-badge { font-size: 11px; color: #5c6bc0; background: #e8eaf6; border-radius: 4px; padding: 1px 6px; margin-left: 6px; }
  `]
})
export class SubjectsComponent implements OnInit {
  subjects: Subject[] = [];
  departments: Department[] = [];
  loading = true;
  search = '';
  columns = ['name', 'period', 'department', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  get filteredSubjects(): Subject[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.subjects;
    return this.subjects.filter(s =>
      (s.name ?? '').toLowerCase().includes(q) ||
      (s.shortName ?? '').toLowerCase().includes(q) ||
      (s.departmentName ?? '').toLowerCase().includes(q));
  }

  ngOnInit(): void {
    this.api.getDepartments().subscribe(d => { this.departments = d; this.load(); });
  }

  load(): void {
    this.loading = true;
    this.api.getSubjects().subscribe({
      next: data => { this.subjects = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  openDialog(subject: Subject | null): void {
    const ref = this.dialog.open(SubjectDialogComponent, { data: { subject, departments: this.departments }, width: '480px' });
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

  openRoomBindings(subject: Subject): void {
    this.dialog.open(SubjectRoomBindingsDialogComponent, { data: { subject }, width: '560px' });
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
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, MatDialogModule, SearchSelectComponent],
  template: `
    <h2 mat-dialog-title>{{ data.subject ? 'Редактировать' : 'Добавить' }} дисциплину</h2>
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
        <app-search-select class="full-width" label="Кафедра (необязательно)" formControlName="departmentId"
          [options]="data.departments" [displayWith]="departmentLabel"
          [allowNull]="true" nullLabel="— Без кафедры —"></app-search-select>
        <div class="subgroup-row">
          <mat-checkbox formControlName="allowsSubgroups">Лабораторные по подгруппам</mat-checkbox>
          <mat-form-field appearance="outline" class="sub-count" *ngIf="form.value.allowsSubgroups">
            <mat-label>Подгрупп</mat-label>
            <input matInput type="number" formControlName="subgroupCount" [min]="2" [max]="6">
          </mat-form-field>
        </div>
        <p class="subgroup-hint">Группа делится на подгруппы, каждая с отдельным преподавателем и аудиторией (нужно ≥2 преподавателя на лаб.).</p>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 380px; gap: 4px; } .row { display: flex; gap: 8px; } .flex1 { flex: 1; } .flex2 { flex: 2; } .full-width { width: 100%; } .subgroup-row { display: flex; align-items: center; gap: 16px; } .sub-count { width: 110px; } .subgroup-hint { color: #888; font-size: 12px; margin: 0 0 4px; }`]
})
export class SubjectDialogComponent {
  form: FormGroup;

  departmentLabel = (d: Department): string => `${d.shortCode} — ${d.name}`;

  constructor(
    private dialogRef: MatDialogRef<SubjectDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { subject: Subject | null; departments: Department[] },
    private fb: FormBuilder
  ) {
    const s = data.subject;
    this.form = this.fb.group({
      name: [s?.name ?? '', Validators.required],
      shortName: [s?.shortName ?? '', Validators.required],
      academicYear: [s?.academicYear ?? CURRENT_YEAR, [Validators.required, Validators.min(2020)]],
      term: [s?.term ?? Term.First, Validators.required],
      departmentId: [s?.departmentId ?? null],
      allowsSubgroups: [s?.allowsSubgroups ?? false],
      subgroupCount: [s?.subgroupCount ?? 2, [Validators.min(2), Validators.max(6)]]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}

// Manage the hard (subject, lessonType) -> allowed rooms binding. Empty selection = no restriction.
@Component({
  selector: 'app-subject-room-bindings-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatSelectModule,
    MatDialogModule, MatIconModule, MatProgressSpinnerModule, MatSnackBarModule, SearchSelectComponent
  ],
  template: `
    <h2 mat-dialog-title>Аудитории: {{ data.subject.name }}</h2>
    <mat-dialog-content>
      <p class="hint">Если для типа занятия выбраны аудитории — это занятие можно ставить только в них.
        Пусто = без ограничений (по типу аудитории). Показаны только аудитории, подходящие по типу занятия.</p>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="32"></mat-spinner></div>
      <div *ngIf="!loading">
        <app-search-select class="full" *ngFor="let lt of lessonTypes"
          [label]="lt.label" [multiple]="true"
          [options]="compatibleRooms(lt.value)" [displayWith]="roomLabel"
          searchPlaceholder="Поиск аудитории..."
          [(ngModel)]="selected[lt.value]"></app-search-select>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="loading || saving" (click)="save()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`.full { width: 100%; } .hint { color: #888; font-size: 12px; margin: 0 0 8px; } .loading-wrap { display: flex; justify-content: center; padding: 24px; }`]
})
export class SubjectRoomBindingsDialogComponent implements OnInit {
  loading = true;
  saving = false;
  rooms: Room[] = [];
  lessonTypes = [
    { value: LessonType.Lecture, label: 'Лекция' },
    { value: LessonType.Practical, label: 'Практика' },
    { value: LessonType.Lab, label: 'Лабораторная' },
    { value: LessonType.Seminar, label: 'Семинар' },
  ];
  selected: Record<string, string[]> = {};

  constructor(
    private api: ApiService,
    private dialogRef: MatDialogRef<SubjectRoomBindingsDialogComponent>,
    private snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public data: { subject: Subject }
  ) {}

  roomLabel = (r: Room): string => (r.buildingShortCode ? r.buildingShortCode + '-' : '') + r.number;

  compatibleRooms(lt: LessonType): Room[] {
    const sel = new Set(this.selected[lt] ?? []);
    return this.rooms.filter(r => sel.has(r.id) || this.isCompatible(r, lt));
  }

  private isCompatible(room: Room, lt: LessonType): boolean {
    if (room.allowedLessonTypes?.length) return room.allowedLessonTypes.includes(lt);
    switch (room.roomType) {
      case RoomType.LectureHall:    return lt === LessonType.Lecture || lt === LessonType.Practical;
      case RoomType.RegularCabinet: return lt === LessonType.Lecture || lt === LessonType.Practical || lt === LessonType.Seminar;
      case RoomType.Lab:            return lt === LessonType.Lab;
      case RoomType.ComputerLab:    return lt === LessonType.Practical || lt === LessonType.Lab;
      case RoomType.SportsHall:     return lt === LessonType.PhysicalEducation;
      default:                      return true; // Virtual / unknown — don't filter out
    }
  }

  ngOnInit(): void {
    for (const lt of this.lessonTypes) this.selected[lt.value] = [];
    forkJoin({
      rooms: this.api.getRooms(),
      bindings: this.api.getSubjectRoomBindings(this.data.subject.id),
    }).subscribe({
      next: ({ rooms, bindings }) => {
        this.rooms = rooms.filter(r => !r.isOnline && !r.isDistributed);
        for (const b of bindings) this.selected[b.lessonType] = b.roomIds;
        this.loading = false;
      },
      error: () => { this.loading = false; this.snackBar.open('Ошибка загрузки', 'OK', { duration: 4000 }); }
    });
  }

  save(): void {
    this.saving = true;
    const calls = this.lessonTypes.map(lt =>
      this.api.updateSubjectRoomBinding(this.data.subject.id, lt.value, this.selected[lt.value] ?? []));
    forkJoin(calls).subscribe({
      next: () => { this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); this.dialogRef.close(true); },
      error: e => { this.saving = false; this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 }); }
    });
  }
}
