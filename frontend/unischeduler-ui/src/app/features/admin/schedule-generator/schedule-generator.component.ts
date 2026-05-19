import { Component, OnInit, OnDestroy, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { interval, Subscription } from 'rxjs';
import { switchMap, takeWhile } from 'rxjs/operators';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, GenerationJobStatus, Faculty } from '../../../core/models';
import { ScheduleStatus, Term } from '../../../core/models/enums';

const CURRENT_YEAR = new Date().getFullYear();

@Component({
  selector: 'app-schedule-generator',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, RouterLink,
    MatButtonModule, MatIconModule, MatCardModule, MatSlideToggleModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatProgressBarModule, MatChipsModule, MatTooltipModule, MatSnackBarModule,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="page-header">
      <h1>Расписания</h1>
      <div class="header-right">
        <mat-slide-toggle [(ngModel)]="showArchived" class="archive-toggle">
          Архивные
        </mat-slide-toggle>
        <button mat-raised-button color="primary" (click)="openCreateDialog()">
          <mat-icon>add</mat-icon> Создать расписание
        </button>
      </div>
    </div>

    <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>

    <ng-container *ngIf="!loading">
    <mat-card *ngFor="let s of visibleSchedules" class="schedule-card">
      <mat-card-header>
        <mat-card-title>
          {{ s.academicYear }}/{{ s.academicYear + 1 }} — {{ termLabel(s.term) }} семестр
          <span *ngIf="s.facultyName" class="faculty-tag"> | {{ s.facultyName }}</span>
        </mat-card-title>
        <mat-card-subtitle>
          {{ s.startDate | date:'dd.MM.yyyy' }} – {{ s.endDate | date:'dd.MM.yyyy' }}
          <span *ngIf="s.allowCrossFacultyLessons"> · межфакультетные разрешены</span>
        </mat-card-subtitle>
        <span class="spacer"></span>
        <mat-chip [class]="'status-' + s.status.toLowerCase()">{{ statusLabel(s.status) }}</mat-chip>
      </mat-card-header>
      <mat-card-content>
        <div *ngIf="generationStatus[s.id] as gs" class="generation-status">
          <mat-progress-bar
            *ngIf="gs.status === 'queued' || gs.status === 'running'"
            mode="indeterminate">
          </mat-progress-bar>
          <div class="status-text" [class]="'gen-' + gs.status">
            {{ genStatusLabel(gs) }}
            <span *ngIf="gs.status === 'completed'"> — {{ gs.entriesCreated }} занятий</span>
          </div>
        </div>
      </mat-card-content>
      <mat-card-actions>
        <button mat-button color="primary" [routerLink]="['/admin/schedules', s.id, 'editor']">
          <mat-icon>edit_calendar</mat-icon> Редактор
        </button>
        <button mat-button (click)="triggerGeneration(s)" [disabled]="isGenerating(s.id) || s.status === 'Archived'">
          <mat-icon>auto_fix_high</mat-icon> Генерировать
        </button>
        <button mat-button *ngIf="s.status === 'Draft'" (click)="publishSchedule(s)" [disabled]="isGenerating(s.id)">
          <mat-icon>publish</mat-icon> Опубликовать
        </button>
        <button mat-button *ngIf="s.status === 'Published'" (click)="archiveSchedule(s)">
          <mat-icon>archive</mat-icon> Архивировать
        </button>
        <button mat-button *ngIf="s.status === 'Archived'" (click)="unarchiveSchedule(s)">
          <mat-icon>unarchive</mat-icon> Разархивировать
        </button>
        <button mat-button color="warn" (click)="deleteSchedule(s)" [disabled]="s.status === 'Published'">
          <mat-icon>delete</mat-icon>
        </button>
      </mat-card-actions>
    </mat-card>

    <div *ngIf="schedules.length === 0" class="empty-state">
      Расписаний нет. Создайте первое.
    </div>
    </ng-container>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .header-right { display: flex; align-items: center; gap: 16px; }
    .archive-toggle { font-size: 14px; }
    h1 { margin: 0; }
    .schedule-card { margin-bottom: 16px; }
    mat-card-header { display: flex; align-items: center; }
    .spacer { flex: 1; }
    .faculty-tag { font-weight: normal; color: #555; }
    .generation-status { margin-top: 8px; }
    .status-text { font-size: 13px; margin-top: 4px; }
    .gen-completed { color: #388e3c; }
    .gen-failed { color: #d32f2f; }
    .gen-queued, .gen-running { color: #1976d2; }
    .status-draft { background: #fff3e0; color: #e65100; }
    .status-published { background: #e8f5e9; color: #1b5e20; }
    .status-archived { background: #f3e5f5; color: #4a148c; }
    .empty-state { text-align: center; padding: 48px; color: #888; }
    mat-card-actions button { margin-right: 4px; }
    .loading-wrap { display: flex; justify-content: center; padding: 48px; }
  `]
})
export class ScheduleGeneratorComponent implements OnInit, OnDestroy {
  schedules: Schedule[] = [];
  faculties: Faculty[] = [];
  generationStatus: Record<string, GenerationJobStatus> = {};
  loading = true;
  showArchived = false;
  private pollingSubscriptions: Record<string, Subscription> = {};

  get visibleSchedules(): Schedule[] {
    return this.showArchived ? this.schedules : this.schedules.filter(s => s.status !== ScheduleStatus.Archived);
  }

  constructor(
    private api: ApiService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.api.getFaculties().subscribe(f => { this.faculties = f; this.loadSchedules(); });
  }

  ngOnDestroy(): void {
    Object.values(this.pollingSubscriptions).forEach(s => s.unsubscribe());
  }

  loadSchedules(): void {
    this.loading = true;
    this.api.getSchedules().subscribe({
      next: data => { this.schedules = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(CreateScheduleDialogComponent, {
      data: this.faculties, width: '480px'
    });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.api.createSchedule(result).subscribe({
        next: () => { this.loadSchedules(); this.snackBar.open('Расписание создано', 'OK', { duration: 3000 }); },
        error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  triggerGeneration(schedule: Schedule): void {
    this.api.generateSchedule(schedule.id, { timeoutSeconds: 120 }).subscribe({
      next: () => {
        this.generationStatus[schedule.id] = { scheduleId: schedule.id, status: 'queued', entriesCreated: 0 };
        this.startPolling(schedule.id);
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка запуска генерации', 'OK', { duration: 4000 })
    });
  }

  private startPolling(scheduleId: string): void {
    this.pollingSubscriptions[scheduleId]?.unsubscribe();
    this.pollingSubscriptions[scheduleId] = interval(2000).pipe(
      switchMap(() => this.api.getGenerationStatus(scheduleId)),
      takeWhile(s => s.status === 'queued' || s.status === 'running', true)
    ).subscribe(status => {
      this.generationStatus[scheduleId] = status;
      if (status.status === 'completed') {
        this.snackBar.open(`Генерация завершена: ${status.entriesCreated} занятий`, 'OK', { duration: 5000 });
        this.loadSchedules();
      } else if (status.status === 'failed') {
        this.snackBar.open(`Генерация не удалась: ${status.message}`, 'OK', { duration: 6000 });
      }
    });
  }

  isGenerating(scheduleId: string): boolean {
    const s = this.generationStatus[scheduleId];
    return s?.status === 'queued' || s?.status === 'running';
  }

  publishSchedule(schedule: Schedule): void {
    this.api.publishSchedule(schedule.id).subscribe({
      next: () => { this.snackBar.open('Расписание опубликовано', 'OK', { duration: 3000 }); this.loadSchedules(); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  archiveSchedule(schedule: Schedule): void {
    const label = `${schedule.academicYear}/${schedule.academicYear + 1} ${this.termLabel(schedule.term)}`;
    if (!confirm(`Архивировать расписание "${label}"?`)) return;
    this.api.archiveSchedule(schedule.id).subscribe({
      next: () => { this.snackBar.open('Расписание архивировано', 'OK', { duration: 3000 }); this.loadSchedules(); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  unarchiveSchedule(schedule: Schedule): void {
    this.api.unarchiveSchedule(schedule.id).subscribe({
      next: () => { this.snackBar.open('Расписание возвращено в черновики', 'OK', { duration: 3000 }); this.loadSchedules(); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  deleteSchedule(schedule: Schedule): void {
    const label = `${schedule.academicYear}/${schedule.academicYear + 1} ${this.termLabel(schedule.term)}`;
    if (!confirm(`Удалить расписание "${label}"?`)) return;
    this.api.deleteSchedule(schedule.id).subscribe({
      next: () => { this.snackBar.open('Расписание удалено', 'OK', { duration: 3000 }); this.loadSchedules(); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  termLabel(term: Term): string {
    return term === Term.First ? '1-й' : '2-й';
  }

  statusLabel(status: string): string {
    switch (status) {
      case ScheduleStatus.Draft: return 'Черновик';
      case ScheduleStatus.Published: return 'Опубликовано';
      case ScheduleStatus.Archived: return 'Архив';
      default: return status;
    }
  }

  genStatusLabel(gs: GenerationJobStatus): string {
    switch (gs.status) {
      case 'queued':    return 'В очереди...';
      case 'running':   return gs.stage ?? 'Генерация...';
      case 'completed': return 'Готово';
      case 'failed':    return `Ошибка: ${gs.message}`;
      default:          return '';
    }
  }
}

@Component({
  selector: 'app-create-schedule-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatCheckboxModule, MatDialogModule
  ],
  template: `
    <h2 mat-dialog-title>Новое расписание</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <div class="row">
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Учебный год</mat-label>
            <input matInput type="number" formControlName="academicYear" min="2020" max="2040">
          </mat-form-field>
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Семестр</mat-label>
            <mat-select formControlName="term">
              <mat-option value="First">1-й (осенний)</mat-option>
              <mat-option value="Second">2-й (весенний)</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <div class="row">
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Начало семестра</mat-label>
            <input matInput type="date" formControlName="startDate">
          </mat-form-field>
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Конец семестра</mat-label>
            <input matInput type="date" formControlName="endDate">
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Факультет (необязательно)</mat-label>
          <mat-select formControlName="facultyId">
            <mat-option [value]="null">— Все факультеты —</mat-option>
            <mat-option *ngFor="let f of data" [value]="f.id">{{ f.shortCode }} — {{ f.name }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-checkbox formControlName="allowCrossFacultyLessons">
          Разрешить межфакультетные занятия
        </mat-checkbox>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Создать</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-form { display: flex; flex-direction: column; min-width: 400px; padding-top: 8px; gap: 4px; }
    .row { display: flex; gap: 8px; }
    .flex1 { flex: 1; }
    .full-width { width: 100%; }
  `]
})
export class CreateScheduleDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<CreateScheduleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Faculty[],
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      academicYear: [CURRENT_YEAR, [Validators.required, Validators.min(2020)]],
      term: [Term.First, Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      facultyId: [null],
      allowCrossFacultyLessons: [false]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
