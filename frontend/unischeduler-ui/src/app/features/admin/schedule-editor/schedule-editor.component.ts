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
import { forkJoin, switchMap } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, ScheduleEntry, MoveEntryDto, StudentGroup, Teacher, Subject, Room, PlanProgressItem, StudyPlan } from '../../../core/models';
import { RussianDayOfWeek, WeekType } from '../../../core/models/enums';
import { ScheduleGridComponent } from './schedule-grid/schedule-grid.component';
import { AddEntryDialogComponent, AddEntryDialogData, AddEntryDialogResult } from './add-entry-dialog.component';
import { BackfillDialogComponent } from './backfill-dialog.component';
import { SearchSelectComponent } from '../../../shared/components/search-select.component';

interface AuditResult {
  conflicts: { type: string; description: string }[];
  warnings: { type: string; description: string }[];
  generationNotes: string | null;
  totalEntries: number;
  currentScore: number;
  baseScore: number | null;
}

@Component({
  selector: 'app-schedule-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatButtonModule, MatButtonToggleModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatSnackBarModule,
    MatProgressSpinnerModule, MatExpansionModule, MatDialogModule,
    ScheduleGridComponent, SearchSelectComponent
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
          <span *ngIf="audit" class="score-note"
                [class.score-better]="audit.baseScore != null && audit.currentScore < audit.baseScore"
                [class.score-worse]="audit.baseScore != null && audit.currentScore > audit.baseScore"
                [title]="'Штрафные очки: меньше = лучше. Не включает штраф за время хода между корпусами.'">
            · {{ audit.baseScore != null ? (audit.baseScore + ' → ') : '' }}{{ audit.currentScore }}
          </span>
        </p>
      </div>
      <div class="header-right">
        <div class="header-filters">
          <app-search-select class="filter-field" label="Группа" [options]="groups"
            [allowNull]="true" nullLabel="Все"
            [(ngModel)]="selectedGroupId" (ngModelChange)="onFilterChange()"></app-search-select>
          <app-search-select class="filter-field" label="Преподаватель" [options]="teachers"
            displayField="displayName" [allowNull]="true" nullLabel="Все"
            [(ngModel)]="selectedTeacherId" (ngModelChange)="onFilterChange()"></app-search-select>
          <app-search-select class="filter-field" label="Аудитория" [options]="rooms"
            [displayWith]="roomLabel" [allowNull]="true" nullLabel="Все"
            [(ngModel)]="selectedRoomId" (ngModelChange)="onFilterChange()"></app-search-select>
        </div>
        <mat-button-toggle-group [(ngModel)]="weekFilter" class="week-toggle">
          <mat-button-toggle value="Both">Обе</mat-button-toggle>
          <mat-button-toggle value="Odd">Нечётная</mat-button-toggle>
          <mat-button-toggle value="Even">Чётная</mat-button-toggle>
        </mat-button-toggle-group>
        <div class="action-buttons">
          <button mat-stroked-button (click)="updateScore()" [disabled]="!schedule || isArchived" title="Пересчитать базовую оценку">
            <mat-icon>refresh</mat-icon> Оценка
          </button>
          <button mat-stroked-button (click)="exportJson()" [disabled]="!schedule" title="Скачать расписание как JSON">
            <mat-icon>download</mat-icon> JSON
          </button>
          <button mat-stroked-button (click)="jsonFileInput.click()" [disabled]="!schedule || isArchived" title="Загрузить расписание из JSON">
            <mat-icon>upload</mat-icon> JSON
          </button>
          <input #jsonFileInput type="file" accept=".json" style="display:none" (change)="importJson($event)">
          <button mat-stroked-button (click)="openBackfill()" [disabled]="!schedule || isArchived" title="Заполнить справочники (аудитории, дисциплины, часы) из этого расписания">
            <mat-icon>auto_fix_high</mat-icon> Заполнить
          </button>
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
      (entrySplit)="onEntrySplit($event)"
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
    .score-better { color: #2e7d32; }
    .score-worse  { color: #c62828; }
    .header-right { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
    .header-filters { display: flex; gap: 8px; }
    .filter-field { min-width: 160px; }
    .week-toggle { height: 40px; }
    .action-buttons { display: flex; gap: 6px; align-items: center; }
    .loading-state { display: flex; justify-content: center; padding: 64px; }
    .audit-panel { margin-bottom: 12px; border-radius: 6px !important; }
    .conflict-title { display: flex; align-items: center; gap: 8px; width: 100%; cursor: pointer; overflow: hidden; }
    .conflict-title > span { flex-shrink: 0; }
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
    .plan-names { font-size: 12px; color: #888; margin-left: 8px; flex: 1 1 auto; min-width: 0;
      white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
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
  selectedRoomId: string | null = null;
  weekFilter = 'Both';
  audit: AuditResult | null = null;
  conflictsExpanded = true;
  private pendingMergeCheck = false;
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
      teacherId: this.selectedTeacherId ?? undefined,
      roomId: this.selectedRoomId ?? undefined
    }).subscribe({
      next: data => {
        this.entries = data;
        this.loading = false;
        if (this.pendingMergeCheck) {
          this.pendingMergeCheck = false;
          this.checkAndMergePairs();
        }
      },
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

  roomLabel = (r: Room): string => r.buildingShortCode ? `${r.buildingShortCode}-${r.number}` : r.number;

  onFilterChange(): void { this.loadEntries(); }

  openBackfill(): void {
    if (!this.schedule) return;
    this.dialog.open(BackfillDialogComponent, {
      data: { scheduleId: this.schedule.id }, width: '660px'
    }).afterClosed().subscribe((applied: boolean | undefined) => {
      if (!applied || !this.schedule) return;
      // Reload reference data the backfill may have changed.
      this.api.getTeachers().subscribe(t => this.teachers = t);
      this.api.getRooms().subscribe(r => this.rooms = r);
      this.api.getSubjects(this.schedule.academicYear, this.schedule.term).subscribe(s => this.subjects = s);
      this.loadPlanProgress();
    });
  }

  onEntryMoved(event: { entryId: string; dto: MoveEntryDto }): void {
    const movedEntry = this.entries.find(e => e.id === event.entryId);
    if (movedEntry && movedEntry.weekType !== WeekType.Both) {
      const match = this.entries.find(e =>
        e.id !== event.entryId &&
        e.dayOfWeek === event.dto.dayOfWeek &&
        e.pairNumber === event.dto.pairNumber &&
        (e.weekType === WeekType.Odd || e.weekType === WeekType.Even) &&
        e.weekType !== event.dto.weekType &&
        e.subjectId === movedEntry.subjectId &&
        e.teacherId === movedEntry.teacherId &&
        (e.roomId ?? null) === ((event.dto.roomId ?? movedEntry.roomId) ?? null) &&
        e.lessonType === movedEntry.lessonType &&
        e.isOnline === movedEntry.isOnline &&
        e.studentGroups.length === movedEntry.studentGroups.length &&
        e.studentGroups.every(g => movedEntry.studentGroups.some(mg => mg.id === g.id))
      );
      if (match) {
        this.api.updateEntry(match.id, {
          subjectId: match.subjectId, teacherId: match.teacherId, roomId: match.roomId,
          dayOfWeek: match.dayOfWeek, pairNumber: match.pairNumber, weekType: WeekType.Both,
          lessonType: match.lessonType, isOnline: match.isOnline,
          groupIds: match.studentGroups.map(g => g.id)
        }).pipe(switchMap(() => this.api.deleteEntry(event.entryId)))
        .subscribe({
          next: () => {
            this.snackBar.open('Занятия объединены', 'OK', { duration: 2000 });
            this.refreshAfterMutation();
          },
          error: (e) => {
            this.snackBar.open(e.error?.title || 'Ошибка объединения', 'OK', { duration: 4000 });
            this.loadEntries();
          }
        });
        return;
      }
    }
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

  onAddRequested(event: { day: RussianDayOfWeek; pair: number; weekType: string; existingEntry?: ScheduleEntry }): void {
    const data: AddEntryDialogData = {
      scheduleId: this.schedule!.id,
      day: event.day,
      pair: event.pair,
      weekType: event.weekType,
      subjects: this.subjects,
      teachers: this.teachers,
      groups: this.groups,
      rooms: this.rooms,
      existingEntry: event.existingEntry
    };
    this.dialog.open(AddEntryDialogComponent, { data, width: '480px' })
      .afterClosed().subscribe((result: AddEntryDialogResult | undefined) => {
        if (!result) return;
        if (result.mode === 'create') {
          this.api.createEntry(result.dto).subscribe({
            next: () => {
              this.snackBar.open('Занятие добавлено', 'OK', { duration: 2000 });
              this.refreshAfterMutation();
            },
            error: (e) => this.snackBar.open(e.error?.title || 'Конфликт при добавлении', 'OK', { duration: 4000 })
          });
        } else if (result.mode === 'create-parallel') {
          this.api.createParallelEntries(result.dto).subscribe({
            next: (entries) => {
              this.snackBar.open(`Добавлено параллельных сессий: ${entries.length}`, 'OK', { duration: 2500 });
              this.refreshAfterMutation();
            },
            error: (e) => this.snackBar.open(e.error?.title || 'Конфликт при добавлении', 'OK', { duration: 4000 })
          });
        } else if (result.mode === 'update') {
          this.api.updateEntry(result.entryId, result.dto).subscribe({
            next: () => {
              this.snackBar.open('Занятие обновлено', 'OK', { duration: 2000 });
              this.refreshAfterMutation();
            },
            error: (e) => this.snackBar.open(e.error?.title || 'Конфликт при изменении', 'OK', { duration: 4000 })
          });
        } else if (result.mode === 'split-edit') {
          this.api.splitEditEntry(result.entryId, result.dto).subscribe({
            next: () => {
              this.snackBar.open('Занятие разделено и обновлено', 'OK', { duration: 2500 });
              this.refreshAfterMutation();
            },
            error: (e) => this.snackBar.open(e.error?.title || 'Не удалось разделить занятие', 'OK', { duration: 4000 })
          });
        }
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
            const msg = `Импортировано занятий: ${r.committed}` + (r.errors.length ? `; примечаний: ${r.errors.length} (см. консоль)` : '');
            this.snackBar.open(msg, 'OK', { duration: r.errors.length ? 6000 : 3000 });
            if (r.errors.length) console.warn('Import notes:', r.errors);
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
    if (lt === 'Language') return 'Ин.яз';
    return lt;
  }

  warningIcon(type: string): string {
    if (type.startsWith('SanPin')) return 'health_and_safety';
    if (type === 'Window') return 'hourglass_empty';
    if (type === 'HoursUnderScheduled') return 'arrow_downward';
    if (type === 'HoursOverScheduled') return 'arrow_upward';
    return 'info';
  }

  onEntrySplit(event: { entry: ScheduleEntry; sourceWeekType: WeekType; dto: MoveEntryDto }): void {
    const oppositeWeekType = event.sourceWeekType === WeekType.Odd ? WeekType.Even : WeekType.Odd;
    this.api.moveEntry(event.entry.id, { ...event.dto, weekType: event.sourceWeekType }).pipe(
      switchMap(() => this.api.createEntry({
        scheduleId: event.entry.scheduleId,
        subjectId: event.entry.subjectId,
        teacherId: event.entry.teacherId,
        roomId: event.entry.roomId,
        dayOfWeek: event.entry.dayOfWeek,
        pairNumber: event.entry.pairNumber,
        weekType: oppositeWeekType,
        lessonType: event.entry.lessonType,
        isOnline: event.entry.isOnline,
        groupIds: event.entry.studentGroups.map(g => g.id)
      }))
    ).subscribe({
      next: () => {
        this.snackBar.open('Занятие разделено', 'OK', { duration: 2000 });
        this.refreshAfterMutation();
      },
      error: (e) => {
        this.snackBar.open(e.error?.title || 'Ошибка разделения', 'OK', { duration: 4000 });
        this.loadEntries();
      }
    });
  }

  private checkAndMergePairs(): void {
    const odd = this.entries.filter(e => e.weekType === WeekType.Odd);
    const even = this.entries.filter(e => e.weekType === WeekType.Even);
    const merges: { keep: ScheduleEntry; remove: ScheduleEntry }[] = [];

    for (const oddEntry of odd) {
      const match = even.find(e =>
        e.dayOfWeek === oddEntry.dayOfWeek &&
        e.pairNumber === oddEntry.pairNumber &&
        e.subjectId === oddEntry.subjectId &&
        e.teacherId === oddEntry.teacherId &&
        (e.roomId ?? null) === (oddEntry.roomId ?? null) &&
        e.lessonType === oddEntry.lessonType &&
        e.isOnline === oddEntry.isOnline &&
        e.studentGroups.length === oddEntry.studentGroups.length &&
        e.studentGroups.every(g => oddEntry.studentGroups.some(og => og.id === g.id))
      );
      if (match) merges.push({ keep: oddEntry, remove: match });
    }

    if (merges.length === 0) return;

    forkJoin(merges.map(m => this.api.deleteEntry(m.remove.id))).pipe(
      switchMap(() => forkJoin(merges.map(m => this.api.updateEntry(m.keep.id, {
        subjectId: m.keep.subjectId,
        teacherId: m.keep.teacherId,
        roomId: m.keep.roomId,
        dayOfWeek: m.keep.dayOfWeek,
        pairNumber: m.keep.pairNumber,
        weekType: WeekType.Both,
        lessonType: m.keep.lessonType,
        isOnline: m.keep.isOnline,
        groupIds: m.keep.studentGroups.map(g => g.id)
      }))))
    ).subscribe({
      next: () => {
        this.snackBar.open('Занятия объединены', 'OK', { duration: 2000 });
        this.loadEntries();
        this.loadAudit();
      },
      error: () => this.loadEntries()
    });
  }

  updateScore(): void {
    if (!this.schedule) return;
    this.api.updateScore(this.schedule.id).subscribe({
      next: () => {
        this.snackBar.open('Базовая оценка обновлена', 'OK', { duration: 2000 });
        this.loadAudit();
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 3000 })
    });
  }

  private refreshAfterMutation(): void {
    this.pendingMergeCheck = true;
    this.loadEntries();
    this.loadAudit();
    this.loadPlanProgress();
    if (this.schedule) {
      this.api.getSchedule(this.schedule.id).subscribe(s => this.schedule = s);
    }
  }
}
