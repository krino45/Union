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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subscription, timer, of, EMPTY, forkJoin } from 'rxjs';
import { exhaustMap, takeWhile, catchError, filter } from 'rxjs/operators';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, GenerationJobStatus, Faculty, SolverWeights, StudyPlan } from '../../../core/models';
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
          <span class="schedule-name">{{ s.name || (s.academicYear + '/' + (s.academicYear + 1) + ' — ' + termLabel(s.term) + ' семестр') }}</span>
          <button mat-icon-button *ngIf="s.isMine && s.status === 'Draft'" (click)="renameSchedule(s)" matTooltip="Переименовать">
            <mat-icon class="rename-icon">drive_file_rename_outline</mat-icon>
          </button>
          <span *ngIf="s.facultyName" class="faculty-tag"> | {{ s.facultyName }}</span>
        </mat-card-title>
        <mat-card-subtitle>
          {{ s.startDate | date:'dd.MM.yyyy' }} – {{ s.endDate | date:'dd.MM.yyyy' }}
          <span *ngIf="s.allowCrossFacultyLessons"> · межфакультетные разрешены</span>
        </mat-card-subtitle>
        <span class="spacer"></span>
        <mat-chip *ngIf="s.status === 'Draft' && !s.isMine && s.ownerUsername" class="owner-chip" matTooltip="Владелец черновика">
          <mat-icon>person</mat-icon> {{ s.ownerUsername }}
        </mat-chip>
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
        <button mat-button (click)="triggerGeneration(s)" [disabled]="isGenerating(s.id) || s.status === 'Archived' || (!s.isMine && s.status === 'Draft')">
          <mat-icon>auto_fix_high</mat-icon> Генерировать
        </button>
        <button mat-button *ngIf="s.status === 'Draft' && s.isMine" (click)="publishSchedule(s)" [disabled]="isGenerating(s.id)">
          <mat-icon>publish</mat-icon> Опубликовать
        </button>
        <button mat-button *ngIf="s.status === 'Published'" (click)="archiveSchedule(s)">
          <mat-icon>archive</mat-icon> Архивировать
        </button>
        <button mat-button *ngIf="s.status === 'Archived'" (click)="unarchiveSchedule(s)">
          <mat-icon>unarchive</mat-icon> Разархивировать
        </button>
        <span class="spacer"></span>
        <mat-slide-toggle *ngIf="s.status === 'Draft' && s.isMine"
                          [checked]="!!s.isOpenToAdmins"
                          (change)="toggleAccess(s, $event.checked)"
                          matTooltip="Разрешить другим админам редактировать"
                          class="access-toggle">
          {{ s.isOpenToAdmins ? 'открыт' : 'закрыт' }}
        </mat-slide-toggle>
        <button mat-button color="warn" *ngIf="s.isMine || s.status !== 'Draft'"
                (click)="deleteSchedule(s)" [disabled]="s.status === 'Published'">
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
    .schedule-name { font-weight: 500; }
    .rename-icon { font-size: 16px; width: 16px; height: 16px; opacity: 0.6; vertical-align: middle; }
    .owner-chip { background: #ede7f6; color: #4527a0; margin-right: 8px; font-size: 11px; }
    .owner-chip mat-icon { font-size: 14px; width: 14px; height: 14px; margin-right: 2px; }
    .access-toggle { font-size: 12px; margin-right: 8px; }
    mat-card-actions { display: flex; align-items: center; }
    mat-card-actions .spacer { flex: 1; }
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
      next: data => {
        this.schedules = data;
        this.loading = false;
        this.resumeActiveGenerations();
      },
      error: () => { this.loading = false; }
    });
  }

  private resumeActiveGenerations(): void {
    for (const s of this.schedules) {
      if (s.status !== ScheduleStatus.Draft) continue;
      if (this.pollingSubscriptions[s.id]) continue;
      this.api.getGenerationStatus(s.id).subscribe({
        next: status => {
          if (status.status === 'queued' || status.status === 'running') {
            this.generationStatus[s.id] = status;
            this.startPolling(s.id);
          }
        },
        error: () => { /* not_found */ }
      });
    }
  }

  private errMsg(e: any): string {
    if (typeof e?.error === 'string' && e.error.trim()) return e.error;
    return e?.error?.error
        || e?.error?.title
        || e?.error?.detail
        || e?.error?.message
        || e?.message
        || (e?.status ? `HTTP ${e.status}` : 'Неизвестная ошибка');
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
    const ref = this.dialog.open(SolverSettingsDialogComponent, {
      width: '560px',
      data: { academicYear: schedule.academicYear, term: schedule.term }
    });
    ref.afterClosed().subscribe((result: { timeoutSeconds: number; planIds: string[] | null } | null) => {
      if (!result) return;
      const { timeoutSeconds, planIds } = result;
      this.api.generateSchedule(schedule.id, { timeoutSeconds, planIds }).subscribe({
        next: () => {
          this.generationStatus[schedule.id] = { scheduleId: schedule.id, status: 'queued', entriesCreated: 0 };
          this.startPolling(schedule.id);
        },
        error: (e) => {
          const msg = e.status === 409
            ? (e.error?.error || 'Генерация уже выполняется для этого расписания')
            : `Ошибка запуска генерации: ${this.errMsg(e)}`;
          this.snackBar.open(msg, 'OK', { duration: 5000 });
          // If a job is already running, attach the poller so the UI catches up.
          if (e.status === 409) {
            this.api.getGenerationStatus(schedule.id).subscribe(s => {
              this.generationStatus[schedule.id] = s;
              this.startPolling(schedule.id);
            });
          }
        }
      });
    });
  }

  private startPolling(scheduleId: string): void {
    this.pollingSubscriptions[scheduleId]?.unsubscribe();
    this.pollingSubscriptions[scheduleId] = timer(0, 2500).pipe(
      exhaustMap(() => this.api.getGenerationStatus(scheduleId).pipe(
        catchError(() => EMPTY)
      )),
      filter(s => s.status !== 'not_found'),
      takeWhile(s => s.status === 'queued' || s.status === 'running', true)
    ).subscribe({
      next: status => {
        this.generationStatus[scheduleId] = status;
        if (status.status === 'completed') {
          this.snackBar.open(`Генерация завершена: ${status.entriesCreated} занятий`, 'OK', { duration: 5000 });
          this.loadSchedules();
        } else if (status.status === 'failed') {
          this.snackBar.open(`Генерация не удалась: ${status.message || 'без сообщения'}`, 'OK', { duration: 8000 });
        }
      },
      error: (e) => this.snackBar.open(`Опрос статуса прерван: ${this.errMsg(e)}`, 'OK', { duration: 5000 })
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
    const label = schedule.name || `${schedule.academicYear}/${schedule.academicYear + 1} ${this.termLabel(schedule.term)}`;
    if (!confirm(`Удалить расписание "${label}"?`)) return;
    this.api.deleteSchedule(schedule.id).subscribe({
      next: () => { this.snackBar.open('Расписание удалено', 'OK', { duration: 3000 }); this.loadSchedules(); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  renameSchedule(schedule: Schedule): void {
    const next = prompt('Новое название черновика', schedule.name || '');
    if (!next || next.trim() === '' || next === schedule.name) return;
    this.api.renameSchedule(schedule.id, next.trim()).subscribe({
      next: () => { this.snackBar.open('Переименовано', 'OK', { duration: 2000 }); this.loadSchedules(); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  toggleAccess(schedule: Schedule, isOpen: boolean): void {
    this.api.setScheduleAccess(schedule.id, isOpen).subscribe({
      next: () => {
        schedule.isOpenToAdmins = isOpen;
        this.snackBar.open(isOpen ? 'Открыт для админов' : 'Закрыт', 'OK', { duration: 2000 });
      },
      error: (e) => {
        schedule.isOpenToAdmins = !isOpen;
        this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 });
      }
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

@Component({
  selector: 'app-solver-settings-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule,
    MatProgressSpinnerModule, MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>Параметры генерации</h2>
    <mat-dialog-content>
      <div *ngIf="loading" class="spinner-wrap"><mat-spinner diameter="32"></mat-spinner></div>
      <form *ngIf="!loading" [formGroup]="form" class="settings-form">

        <section class="block">
          <h3 class="block-title">Запуск</h3>
          <p class="block-desc">Каждый план решается отдельно: расписания других планов сохраняются и блокируют свои аудитории.</p>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1" *ngIf="studyPlans.length > 0">
              <mat-label>Учебные планы</mat-label>
              <mat-select formControlName="planIds" multiple>
                <mat-option *ngFor="let sp of studyPlans" [value]="sp.id">
                  {{ sp.name || ('План ' + sp.id.slice(0, 8)) }}
                  <span class="plan-meta">— {{ sp.groups.length }} групп, {{ sp.entries.length }} дисциплин</span>
                </mat-option>
              </mat-select>
              <mat-hint>Пусто — все планы по умолчанию (крупные первыми).</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Таймаут на план, сек</mat-label>
              <input matInput type="number" formControlName="timeoutSeconds" min="10" max="3600">
              <mat-hint>Применяется к каждому плану отдельно (10–3600).</mat-hint>
            </mat-form-field>
          </div>
        </section>

        <section class="block">
          <h3 class="block-title">Окна и активность</h3>
          <p class="block-desc">Штраф за пустую пару в середине дня (S1–S2) и за каждый используемый день недели (S3). Чем больше — тем компактнее расписание.</p>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Окно студента</mat-label>
              <input matInput type="number" formControlName="studentWindow" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Окно преподавателя</mat-label>
              <input matInput type="number" formControlName="teacherWindow" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Активный день</mat-label>
              <input matInput type="number" formControlName="activeDay" min="0">
            </mat-form-field>
          </div>
        </section>

        <section class="block">
          <h3 class="block-title">Сверхнормативная нагрузка</h3>
          <p class="block-desc">Штраф СанПиН за каждую пару сверх четырёх в день (S5).</p>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Штраф за пару &gt; 4 в день</mat-label>
              <input matInput type="number" formControlName="sanPinOverload" min="0">
            </mat-form-field>
          </div>
        </section>

        <section class="block">
          <h3 class="block-title">Подряд идущие пары одного типа (S6)</h3>
          <p class="block-desc">Штраф за каждую пару того же предмета и типа сразу после предыдущей.</p>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Лекция</mat-label>
              <input matInput type="number" formControlName="consecLecture" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Семинар</mat-label>
              <input matInput type="number" formControlName="consecSeminar" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Практика</mat-label>
              <input matInput type="number" formControlName="consecPractical" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Лабораторная</mat-label>
              <input matInput type="number" formControlName="consecLab" min="0">
            </mat-form-field>
          </div>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Усиление за серии 3+ подряд</mat-label>
              <input matInput type="number" formControlName="consecRunScalar" min="1">
              <mat-hint>Множитель к штрафу выше для каждой пары в длинной серии.</mat-hint>
            </mat-form-field>
          </div>
        </section>

        <section class="block">
          <h3 class="block-title">Время дня (S7)</h3>
          <p class="block-desc">Штраф за каждую пару в соответствующем слоте. Предпочтительны пары 3–4.</p>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Ранние пары (1–2)</mat-label>
              <input matInput type="number" formControlName="earlyPair" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Средние пары (3–4)</mat-label>
              <input matInput type="number" formControlName="middlePair" min="0">
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Поздние пары (5+)</mat-label>
              <input matInput type="number" formControlName="latePair" min="0">
            </mat-form-field>
          </div>
        </section>

        <section class="block">
          <h3 class="block-title">Размещение (S8–S9)</h3>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Суббота</mat-label>
              <input matInput type="number" formControlName="saturdayPenalty" min="0">
              <mat-hint>0 — субботы без штрафа.</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Чужая кафедра</mat-label>
              <input matInput type="number" formControlName="departmentMismatchPenalty" min="0">
              <mat-hint>Штраф за аудиторию не своей кафедры.</mat-hint>
            </mat-form-field>
          </div>
        </section>

        <section class="block">
          <h3 class="block-title">Ходьба и этажи (S4)</h3>
          <p class="block-desc">Штраф за переход между парами растёт пропорционально длине пути. Лестницы считаются как метры на этаж.</p>
          <div class="row">
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Максимальный штраф за ходьбу</mat-label>
              <input matInput type="number" formControlName="walkingPenaltyMax" min="1">
              <mat-hint>Достигается когда путь занимает всю перемену.</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline" class="flex1">
              <mat-label>Метров за один этаж</mat-label>
              <input matInput type="number" formControlName="stairFloorMeters" min="0">
              <mat-hint>Используется как эквивалент при подъёме по лестнице.</mat-hint>
            </mat-form-field>
          </div>
        </section>

      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="loading || form.invalid" (click)="saveAndRun()">
        Сохранить и запустить
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .settings-form { display: flex; flex-direction: column; padding-top: 4px; min-width: 480px; }
    .block { margin-bottom: 14px; }
    .block-title { margin: 4px 0 2px; font-size: 13px; font-weight: 600; color: #2d2d2d; }
    .block-desc { margin: 0 0 10px; font-size: 12px; color: #666; line-height: 1.4; }
    .row { display: flex; gap: 8px; align-items: flex-start; }
    .flex1 { flex: 1; min-width: 0; }
    .plan-meta { color: #888; font-size: 12px; margin-left: 4px; }
    .spinner-wrap { display: flex; justify-content: center; padding: 32px; }
    /* Let multi-line hints push the field taller instead of overlapping the row below. */
    ::ng-deep .settings-form .mat-mdc-form-field-subscript-wrapper,
    ::ng-deep .settings-form .mat-mdc-form-field-hint-wrapper { position: static; }
  `]
})
export class SolverSettingsDialogComponent implements OnInit {
  form!: FormGroup;
  loading = true;
  studyPlans: StudyPlan[] = [];

  constructor(
    private dialogRef: MatDialogRef<SolverSettingsDialogComponent>,
    @Inject(MAT_DIALOG_DATA) private data: { academicYear: number; term: string },
    private api: ApiService,
    private fb: FormBuilder
  ) {}

  ngOnInit(): void {
    forkJoin({
      weights: this.api.getSolverSettings().pipe(catchError(() => of(null))),
      plans: this.api.getStudyPlans(this.data?.academicYear, this.data?.term).pipe(catchError(() => of([] as StudyPlan[])))
    }).subscribe(({ weights, plans }) => {
      this.studyPlans = plans;
      this.form = weights ? this.buildForm(weights) : this.buildDefaultForm();
      this.loading = false;
    });
  }

  saveAndRun(): void {
    if (this.form.invalid) return;
    this.loading = true;
    const { timeoutSeconds, planIds, ...weights } = this.form.value;
    const planIdsToReturn: string[] | null = (planIds && planIds.length > 0) ? planIds : null;
    this.api.updateSolverSettings(weights).subscribe({
      next: () => this.dialogRef.close({ timeoutSeconds, planIds: planIdsToReturn }),
      error: () => { this.loading = false; }
    });
  }

  private buildForm(w: SolverWeights): FormGroup {
    return this.fb.group({
      planIds:                  [[] as string[]],
      timeoutSeconds:           [120, [Validators.required, Validators.min(10), Validators.max(3600)]],
      studentWindow:            [w.studentWindow,            [Validators.required, Validators.min(0)]],
      teacherWindow:            [w.teacherWindow,            [Validators.required, Validators.min(0)]],
      activeDay:                [w.activeDay,                [Validators.required, Validators.min(0)]],
      sanPinOverload:           [w.sanPinOverload,           [Validators.required, Validators.min(0)]],
      consecLecture:            [w.consecLecture,            [Validators.required, Validators.min(0)]],
      consecSeminar:            [w.consecSeminar,            [Validators.required, Validators.min(0)]],
      consecPractical:          [w.consecPractical,          [Validators.required, Validators.min(0)]],
      consecLab:                [w.consecLab,                [Validators.required, Validators.min(0)]],
      earlyPair:                [w.earlyPair,                [Validators.required, Validators.min(0)]],
      middlePair:               [w.middlePair,               [Validators.required, Validators.min(0)]],
      latePair:                 [w.latePair,                 [Validators.required, Validators.min(0)]],
      consecRunScalar:          [w.consecRunScalar,          [Validators.required, Validators.min(1)]],
      saturdayPenalty:          [w.saturdayPenalty,          [Validators.required, Validators.min(0)]],
      departmentMismatchPenalty:[w.departmentMismatchPenalty,[Validators.required, Validators.min(0)]],
      walkingPenaltyMax:        [w.walkingPenaltyMax,        [Validators.required, Validators.min(1)]],
      stairFloorMeters:         [w.stairFloorMeters ?? 20,   [Validators.required, Validators.min(0)]],
    });
  }

  private buildDefaultForm(): FormGroup {
    return this.fb.group({
      planIds: [[] as string[]],
      timeoutSeconds: [120, [Validators.required, Validators.min(10), Validators.max(3600)]],
      studentWindow: [100], teacherWindow: [80], activeDay: [60], sanPinOverload: [300],
      consecLecture: [70], consecSeminar: [40], consecPractical: [30], consecLab: [10],
      earlyPair: [15], middlePair: [0], latePair: [25], consecRunScalar: [3],
      saturdayPenalty: [30], departmentMismatchPenalty: [50], walkingPenaltyMax: [120],
      stairFloorMeters: [20],
    });
  }
}
