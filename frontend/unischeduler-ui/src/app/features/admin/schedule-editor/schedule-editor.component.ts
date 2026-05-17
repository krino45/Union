import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, ScheduleEntry, StudentGroup, Teacher, Subject, Room, PlanProgressItem, StudyPlan } from '../../../core/models';
import { RussianDayOfWeek } from '../../../core/models/enums';
import { ScheduleGridComponent } from './schedule-grid/schedule-grid.component';
import { AddEntryDialogComponent, AddEntryDialogData } from './add-entry-dialog.component';

interface AuditResult {
  conflicts: { type: string; description: string }[];
  warnings: { type: string; description: string }[];
  generationNotes: string | null;
  totalEntries: number;
}

@Component({
  selector: 'app-schedule-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatButtonModule, MatButtonToggleModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatSnackBarModule,
    MatProgressSpinnerModule, MatExpansionModule, MatDialogModule,
    ScheduleGridComponent
  ],
  template: `
    <div class="editor-header">
      <div>
        <h1 class="title-row">
          {{ schedule ? (schedule.academicYear + '/' + (schedule.academicYear + 1) + ' — ' + (schedule.term === 'First' ? '1-й' : '2-й') + ' сем.') : 'Редактор расписания' }}
          <span class="status-chip" [class.draft]="schedule?.status === 'Draft'" [class.published]="schedule?.status === 'Published'" [class.archived]="schedule?.status === 'Archived'">
            {{ schedule?.status === 'Draft' ? 'Черновик' : schedule?.status === 'Published' ? 'Опубликовано' : schedule?.status === 'Archived' ? 'Архив' : schedule?.status }}
          </span>
        </h1>
        <p class="subtitle" *ngIf="schedule">
          {{ schedule.startDate | date:'dd.MM.yyyy' }} – {{ schedule.endDate | date:'dd.MM.yyyy' }}
          <span *ngIf="audit"> · {{ audit.totalEntries }} занятий</span>
          <span *ngIf="audit?.generationNotes && parseScore(audit!.generationNotes!)" class="score-note" [title]="audit!.generationNotes!">
            · {{ parseScore(audit!.generationNotes!) }}
          </span>
        </p>
      </div>
      <div class="header-right">
        <div class="header-filters">
          <mat-form-field appearance="outline" class="filter-field">
            <mat-label>Группа</mat-label>
            <mat-select [(ngModel)]="selectedGroupId" (ngModelChange)="onFilterChange()">
              <mat-option [value]="null">Все</mat-option>
              <mat-option *ngFor="let g of groups" [value]="g.id">{{ g.name }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="filter-field">
            <mat-label>Преподаватель</mat-label>
            <mat-select [(ngModel)]="selectedTeacherId" (ngModelChange)="onFilterChange()">
              <mat-option [value]="null">Все</mat-option>
              <mat-option *ngFor="let t of teachers" [value]="t.id">{{ t.displayName }}</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <mat-button-toggle-group [(ngModel)]="weekFilter" class="week-toggle">
          <mat-button-toggle value="Both">Обе</mat-button-toggle>
          <mat-button-toggle value="Odd">Нечётная</mat-button-toggle>
          <mat-button-toggle value="Even">Чётная</mat-button-toggle>
        </mat-button-toggle-group>
        <div class="action-buttons">
          <button mat-stroked-button (click)="exportJson()" [disabled]="!schedule" title="Скачать расписание как JSON">
            <mat-icon>download</mat-icon> JSON
          </button>
          <button mat-stroked-button (click)="jsonFileInput.click()" [disabled]="!schedule || isArchived" title="Загрузить расписание из JSON">
            <mat-icon>upload</mat-icon> JSON
          </button>
          <input #jsonFileInput type="file" accept=".json" style="display:none" (change)="importJson($event)">
          <button mat-stroked-button color="primary" (click)="publishSchedule()" *ngIf="schedule?.status === 'Draft'">
            <mat-icon>publish</mat-icon> Опубликовать
          </button>
          <button mat-stroked-button color="warn" (click)="archiveSchedule()" *ngIf="schedule?.status === 'Published'">
            <mat-icon>archive</mat-icon> Архивировать
          </button>
          <button mat-stroked-button (click)="unarchiveSchedule()" *ngIf="isArchived">
            <mat-icon>unarchive</mat-icon> Разархивировать
          </button>
        </div>
      </div>
    </div>

    <!-- Audit panel -->
    <mat-expansion-panel class="audit-panel" *ngIf="audit" [expanded]="conflictsExpanded">
      <mat-expansion-panel-header>
        <mat-panel-title class="conflict-title">
          <mat-icon [class.icon-ok]="audit.conflicts.length === 0" [class.icon-err]="audit.conflicts.length > 0">
            {{ audit.conflicts.length === 0 ? 'check_circle' : 'error' }}
          </mat-icon>
          <span [class.ok-text]="audit.conflicts.length === 0" [class.err-text]="audit.conflicts.length > 0">
            {{ audit.conflicts.length === 0 ? 'Конфликтов нет' : audit.conflicts.length + ' конфликт(а/ов)' }}
          </span>
          <ng-container *ngIf="audit.warnings.length > 0">
            <mat-icon class="icon-warn">warning</mat-icon>
            <span class="warn-text">{{ audit.warnings.length }} предупреждений</span>
          </ng-container>
        </mat-panel-title>
      </mat-expansion-panel-header>
      <div class="issue-section" *ngIf="audit.conflicts.length > 0">
        <div class="issue-section-label err-label">Конфликты (жёсткие)</div>
        <div *ngFor="let c of audit.conflicts" class="issue-item err-item">
          <mat-icon class="ii">{{ conflictIcon(c.type) }}</mat-icon>
          {{ c.description }}
        </div>
      </div>
      <div class="issue-section" *ngIf="audit.warnings.length > 0">
        <div class="issue-section-label warn-label">Предупреждения (СанПиН / нагрузка)</div>
        <div *ngFor="let w of audit.warnings" class="issue-item warn-item">
          <mat-icon class="ii warn-ii">{{ warningIcon(w.type) }}</mat-icon>
          {{ w.description }}
        </div>
      </div>
      <div *ngIf="audit.conflicts.length === 0 && audit.warnings.length === 0" class="no-issues">
        Расписание не содержит конфликтов и предупреждений.
      </div>
    </mat-expansion-panel>

    <!-- Plan progress panel -->
    <mat-expansion-panel class="progress-panel" *ngIf="planProgress.length > 0" [expanded]="progressExpanded">
      <mat-expansion-panel-header>
        <mat-panel-title class="conflict-title">
          <mat-icon [class.icon-ok]="unplacedCount === 0 && partialCount === 0" [class.icon-warn]="unplacedCount > 0 || partialCount > 0">
            {{ unplacedCount === 0 && partialCount === 0 ? 'assignment_turned_in' : 'assignment_late' }}
          </mat-icon>
          <ng-container *ngIf="unplacedCount > 0 || partialCount > 0">
            <span class="warn-text" *ngIf="unplacedCount > 0">{{ unplacedCount }} не размещено</span>
            <span class="warn-text" *ngIf="partialCount > 0"> · {{ partialCount }} частично</span>
          </ng-container>
          <span class="ok-text" *ngIf="unplacedCount === 0 && partialCount === 0">Все дисциплины размещены</span>
          <span class="plan-names" *ngIf="studyPlans.length > 0">{{ studyPlans.length }} уч. план(а/ов): {{ planNames }}</span>
        </mat-panel-title>
      </mat-expansion-panel-header>
      <div class="progress-table-wrap">
        <table class="progress-table">
          <thead><tr>
            <th>Группа</th><th>Дисциплина</th><th>Тип</th>
            <th>Ожид. (ак.ч.)</th><th>Пар/нед. факт/план</th><th>Статус</th>
          </tr></thead>
          <tbody>
            <tr *ngFor="let p of planProgress" [class.unplaced]="p.isUnplaced" [class.partial]="!p.isUnplaced && isPartial(p)">
              <td>{{ p.groupName }}</td>
              <td>{{ p.subjectShortName }}</td>
              <td>{{ ltLabel(p.lessonType) }}</td>
              <td>{{ p.expectedHours }}</td>
              <td>{{ p.actualPairsPerWeek | number:'1.0-1' }} / {{ (p.expectedHours / 2 / p.studyWeeks) | number:'1.0-1' }}</td>
              <td>
                <span *ngIf="p.isUnplaced" class="badge unplaced-badge">Не размещено</span>
                <span *ngIf="!p.isUnplaced && isPartial(p)" class="badge partial-badge">Частично</span>
                <span *ngIf="!p.isUnplaced && !isPartial(p)" class="badge ok-badge">✓</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </mat-expansion-panel>

    <div class="archive-banner" *ngIf="isArchived">
      <mat-icon>lock</mat-icon>
      Архивное расписание — просмотр только для чтения. Нажмите «Разархивировать» для редактирования.
    </div>

    <div *ngIf="loading" class="loading-state">
      <mat-spinner diameter="48"></mat-spinner>
    </div>

    <app-schedule-grid
      *ngIf="!loading && schedule"
      [scheduleId]="schedule.id"
      [entries]="entries"
      [groups]="groups"
      [teachers]="teachers"
      [weekFilter]="weekFilter"
      [readonly]="isArchived"
      (entryMoved)="onEntryMoved($event)"
      (entryDeleted)="onEntryDeleted($event)"
      (addRequested)="onAddRequested($event)">
    </app-schedule-grid>
  `,
  styles: [`
    .editor-header {
      display: flex; justify-content: space-between; align-items: flex-start;
      margin-bottom: 12px; flex-wrap: wrap; gap: 12px;
    }
    .title-row { margin: 0; display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .status-chip {
      font-size: 12px; font-weight: 500; padding: 2px 10px; border-radius: 12px;
    }
    .status-chip.draft { background: #fff3e0; color: #e65100; }
    .status-chip.published { background: #e8f5e9; color: #2e7d32; }
    .status-chip.archived { background: #f5f5f5; color: #757575; }
    .archive-banner {
      display: flex; align-items: center; gap: 8px;
      background: #f5f5f5; border: 1px solid #e0e0e0; border-radius: 6px;
      padding: 8px 14px; margin-bottom: 12px; font-size: 13px; color: #616161;
    }
    .archive-banner mat-icon { font-size: 18px; color: #9e9e9e; }
    .subtitle { margin: 4px 0 0; color: #666; font-size: 13px; }
    .score-note { color: #1976d2; cursor: help; }
    .header-right { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
    .header-filters { display: flex; gap: 8px; }
    .filter-field { min-width: 160px; }
    .week-toggle { height: 40px; }
    .action-buttons { display: flex; gap: 6px; align-items: center; }
    .loading-state { display: flex; justify-content: center; padding: 64px; }
    .audit-panel { margin-bottom: 12px; border-radius: 6px !important; }
    .conflict-title { display: flex; align-items: center; gap: 8px; width: 100%; cursor: pointer; }
    .icon-ok { color: #2e7d32; font-size: 18px; }
    .icon-err { color: #c62828; font-size: 18px; }
    .icon-warn { color: #e65100; font-size: 18px; }
    .ok-text { color: #2e7d32; font-weight: 500; }
    .err-text { color: #c62828; font-weight: 500; }
    .warn-text { color: #e65100; font-weight: 500; }
    .mat-expansion-panel-header { padding: 0 16px; }
    .issue-section { padding: 4px 0 6px 8px; }
    .issue-section-label { font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 4px; }
    .err-label { color: #c62828; }
    .warn-label { color: #e65100; }
    .issue-item { display: flex; align-items: flex-start; gap: 8px; font-size: 13px; color: #444; padding: 2px 0; }
    .ii { font-size: 15px; color: #c62828; flex-shrink: 0; margin-top: 1px; }
    .warn-ii { color: #e65100; }
    .no-issues { font-size: 13px; color: #2e7d32; padding: 4px 8px; }
    .plan-names { font-size: 12px; color: #888; margin-left: 8px; }
    .progress-panel { margin-bottom: 12px; border-radius: 6px !important; }
    .progress-table-wrap { overflow-x: auto; padding: 4px 0; }
    .progress-table { width: 100%; border-collapse: collapse; font-size: 12px; }
    .progress-table th { background: #f5f5f5; padding: 5px 8px; text-align: left; border-bottom: 2px solid #e0e0e0; }
    .progress-table td { padding: 3px 8px; border-bottom: 1px solid #f5f5f5; }
    .progress-table tr.unplaced td { background: #fff3e0; }
    .progress-table tr.partial td { background: #fffde7; }
    .badge { padding: 1px 8px; border-radius: 10px; font-size: 11px; font-weight: 500; }
    .unplaced-badge { background: #ffccbc; color: #bf360c; }
    .partial-badge  { background: #fff9c4; color: #f57f17; }
    .ok-badge       { background: #c8e6c9; color: #1b5e20; }
  `]
})
export class ScheduleEditorComponent implements OnInit {
  schedule: Schedule | null = null;
  entries: ScheduleEntry[] = [];
  groups: StudentGroup[] = [];
  teachers: Teacher[] = [];
  subjects: Subject[] = [];
  rooms: Room[] = [];
  loading = true;
  selectedGroupId: string | null = null;
  selectedTeacherId: string | null = null;
  weekFilter = 'Both';
  audit: AuditResult | null = null;
  conflictsExpanded = true;
  planProgress: PlanProgressItem[] = [];
  studyPlans: StudyPlan[] = [];
  progressExpanded = true;

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      schedule: this.api.getSchedule(id),
      groups: this.api.getGroups(),
      teachers: this.api.getTeachers(),
      rooms: this.api.getRooms()
    }).subscribe(({ schedule, groups, teachers, rooms }) => {
      this.schedule = schedule;
      this.groups = groups;
      this.teachers = teachers;
      this.rooms = rooms;
      this.api.getSubjects(schedule.academicYear, schedule.term).subscribe(s => this.subjects = s);
      this.loadEntries();
      this.loadAudit();
      this.loadPlanProgress();
    });
  }

  loadEntries(): void {
    if (!this.schedule) return;
    this.loading = true;
    this.api.getScheduleEntries(this.schedule.id, {
      groupId: this.selectedGroupId ?? undefined,
      teacherId: this.selectedTeacherId ?? undefined
    }).subscribe({
      next: data => { this.entries = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  loadAudit(): void {
    if (!this.schedule) return;
    this.api.getScheduleAudit(this.schedule.id).subscribe(a => {
      this.audit = a;
      this.conflictsExpanded = a.conflicts.length > 0;
    });
  }

  loadPlanProgress(): void {
    if (!this.schedule) return;
    this.api.getStudyPlans(this.schedule.academicYear, this.schedule.term).subscribe(plans => {
      this.studyPlans = plans;
    });
    this.api.getPlanProgress(this.schedule.id).subscribe(items => {
      this.planProgress = items;
      this.progressExpanded = items.some(i => i.isUnplaced || this.isPartial(i));
    });
  }

  get planNames(): string {
    return this.studyPlans.map(p => p.name).join(', ');
  }

  onFilterChange(): void { this.loadEntries(); }

  onEntryMoved(event: { entryId: string; dto: any }): void {
    this.api.moveEntry(event.entryId, event.dto).subscribe({
      next: () => {
        this.snackBar.open('Занятие перенесено', 'OK', { duration: 2000 });
        this.refreshAfterMutation();
      },
      error: (e) => {
        this.snackBar.open(e.error?.title || 'Конфликт при переносе', 'OK', { duration: 4000 });
        this.loadEntries();
      }
    });
  }

  onEntryDeleted(entryId: string): void {
    this.api.deleteEntry(entryId).subscribe({
      next: () => {
        this.snackBar.open('Занятие удалено', 'OK', { duration: 2000 });
        this.refreshAfterMutation();
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка удаления', 'OK', { duration: 4000 })
    });
  }

  onAddRequested(event: { day: RussianDayOfWeek; pair: number; weekType: string }): void {
    const data: AddEntryDialogData = {
      scheduleId: this.schedule!.id,
      day: event.day,
      pair: event.pair,
      weekType: event.weekType,
      subjects: this.subjects,
      teachers: this.teachers,
      groups: this.groups,
      rooms: this.rooms
    };
    this.dialog.open(AddEntryDialogComponent, { data, width: '480px' })
      .afterClosed().subscribe(dto => {
        if (!dto) return;
        this.api.createEntry(dto).subscribe({
          next: () => {
            this.snackBar.open('Занятие добавлено', 'OK', { duration: 2000 });
            this.refreshAfterMutation();
          },
          error: (e) => this.snackBar.open(e.error?.title || 'Конфликт при добавлении', 'OK', { duration: 4000 })
        });
      });
  }

  publishSchedule(): void {
    if (!this.schedule) return;
    this.api.publishSchedule(this.schedule.id).subscribe({
      next: () => {
        this.snackBar.open('Расписание опубликовано', 'OK', { duration: 2000 });
        this.api.getSchedule(this.schedule!.id).subscribe(s => this.schedule = s);
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка публикации', 'OK', { duration: 4000 })
    });
  }

  archiveSchedule(): void {
    if (!this.schedule) return;
    if (!confirm('Архивировать это расписание?')) return;
    this.api.archiveSchedule(this.schedule.id).subscribe({
      next: () => {
        this.snackBar.open('Расписание архивировано', 'OK', { duration: 2000 });
        this.api.getSchedule(this.schedule!.id).subscribe(s => this.schedule = s);
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка архивации', 'OK', { duration: 4000 })
    });
  }

  unarchiveSchedule(): void {
    if (!this.schedule) return;
    this.api.unarchiveSchedule(this.schedule.id).subscribe({
      next: () => {
        this.snackBar.open('Расписание возвращено в черновики', 'OK', { duration: 2000 });
        this.api.getSchedule(this.schedule!.id).subscribe(s => this.schedule = s);
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  get isArchived(): boolean { return this.schedule?.status === 'Archived'; }

  exportJson(): void {
    if (!this.schedule) return;
    this.api.exportJson(this.schedule.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `schedule-${this.schedule!.academicYear}-${this.schedule!.term}.json`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }

  importJson(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.schedule) return;
    (event.target as HTMLInputElement).value = '';

    const reader = new FileReader();
    reader.onload = () => {
      try {
        const parsed = JSON.parse(reader.result as string);
        const entries = parsed.entries ?? parsed;
        if (!Array.isArray(entries)) throw new Error('Ожидается массив entries');

        const replace = confirm('Заменить все текущие занятия? Нажмите ОК для замены, Отмена — для добавления.');
        this.api.importJson(this.schedule!.id, entries, replace).subscribe({
          next: (r) => {
            const msg = `Импортировано: ${r.committed}` + (r.errors.length ? `; ошибок: ${r.errors.length}` : '');
            this.snackBar.open(msg, 'OK', { duration: r.errors.length ? 6000 : 3000 });
            if (r.errors.length) console.warn('Import errors:', r.errors);
            this.refreshAfterMutation();
          },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка импорта', 'OK', { duration: 4000 })
        });
      } catch (e: any) {
        this.snackBar.open('Неверный формат JSON: ' + e.message, 'OK', { duration: 5000 });
      }
    };
    reader.readAsText(file);
  }

  parseScore(notes: string): string {
    const m = notes.match(/Objective:\s*([\d.]+)/);
    return m ? `Оценка: ${m[1]}` : '';
  }

  conflictIcon(type: string): string {
    if (type === 'RoomDoubleBooked') return 'meeting_room';
    if (type === 'TeacherDoubleBooked') return 'person';
    return 'group';
  }

  get unplacedCount(): number { return this.planProgress.filter(p => p.isUnplaced).length; }
  get partialCount(): number { return this.planProgress.filter(p => !p.isUnplaced && this.isPartial(p)).length; }

  isPartial(p: PlanProgressItem): boolean {
    const expectedPerWeek = p.expectedHours / 2 / p.studyWeeks;
    return Math.abs(p.actualPairsPerWeek - expectedPerWeek) > 0.1;
  }

  ltLabel(lt: string): string {
    if (lt === 'Lecture') return 'Лек.';
    if (lt === 'Practical') return 'Пр.';
    if (lt === 'Lab') return 'Лаб.';
    if (lt === 'Seminar') return 'Сем.';
    return lt;
  }

  warningIcon(type: string): string {
    if (type.startsWith('SanPin')) return 'health_and_safety';
    if (type === 'Window') return 'hourglass_empty';
    if (type === 'HoursUnderScheduled') return 'arrow_downward';
    if (type === 'HoursOverScheduled') return 'arrow_upward';
    return 'info';
  }

  private refreshAfterMutation(): void {
    this.loadEntries();
    this.loadAudit();
    this.loadPlanProgress();
    if (this.schedule) {
      this.api.getSchedule(this.schedule.id).subscribe(s => this.schedule = s);
    }
  }
}
