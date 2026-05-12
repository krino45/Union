import { Component, Input, Output, EventEmitter, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  CdkDragDrop, CdkDrag, CdkDropList, CdkDropListGroup,
  moveItemInArray, transferArrayItem
} from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ScheduleEntry, StudentGroup, Teacher, MoveEntryDto } from '../../../../core/models';
import { RussianDayOfWeek, WeekType } from '../../../../core/models/enums';
import { DayOfWeekPipe } from '../../../../shared/pipes/day-of-week.pipe';
import { WeekTypePipe } from '../../../../shared/pipes/week-type.pipe';
import { LessonTypePipe } from '../../../../shared/pipes/lesson-type.pipe';
import { PAIR_TIMES, DAYS, PAIRS } from '../../../../shared/constants/pairs';

@Component({
  selector: 'app-schedule-grid',
  standalone: true,
  imports: [
    CommonModule,
    CdkDrag, CdkDropList, CdkDropListGroup,
    MatButtonModule, MatIconModule, MatTooltipModule, MatDialogModule,
    DayOfWeekPipe, WeekTypePipe, LessonTypePipe
  ],
  template: `
    <div class="grid-wrapper">
      <div cdkDropListGroup>
        <table class="schedule-grid">
          <thead>
            <tr>
              <th class="pair-col">Пара / Время</th>
              <th *ngFor="let d of days">{{ d | dayOfWeek: 'short' }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of pairs">
              <td class="pair-cell">
                <div class="pair-number">{{ p }}</div>
                <div class="pair-time">{{ getPairTime(p) }}</div>
              </td>
              <td *ngFor="let d of days" class="drop-cell">
                <div class="split-weeks">
                  <!-- Нечётная (Numerator) -->
                  <div class="week-half">
                    <div class="week-label num-label">Неч.</div>
                    <div
                      cdkDropList
                      [id]="cellIdNum(d, p)"
                      [cdkDropListData]="getCellNum(d, p)"
                      [cdkDropListConnectedTo]="allCellIds"
                      (cdkDropListDropped)="onDrop($event, d, p, 'Numerator')"
                      class="drop-zone">
                      <div
                        *ngFor="let entry of getCellNum(d, p)"
                        cdkDrag
                        [cdkDragData]="entry"
                        class="entry-card"
                        [class.online]="entry.isOnline"
                        [class.both-weeks]="entry.weekType === 'Both'">
                        <div class="entry-header">
                          <span class="lesson-type-badge">{{ entry.lessonType | lessonType }}</span>
                          <button mat-icon-button class="delete-btn" (click)="deleteEntry(entry)" [matTooltip]="'Удалить'">
                            <mat-icon>close</mat-icon>
                          </button>
                        </div>
                        <div class="subject">{{ entry.subjectShortName || entry.subjectName }}</div>
                        <div class="teacher">{{ entry.teacherDisplayName }}</div>
                        <div class="room">
                          {{ entry.isOnline ? 'Онлайн' : (entry.buildingShortCode ? entry.buildingShortCode + '-' : '') + (entry.roomNumber || '—') }}
                        </div>
                        <div class="groups" *ngIf="entry.studentGroups?.length">{{ groupNames(entry) }}</div>
                        <div *cdkDragPlaceholder class="drag-placeholder"></div>
                      </div>
                      <div class="empty-slot" *ngIf="getCellNum(d, p).length === 0"></div>
                    </div>
                  </div>

                  <div class="week-divider"></div>

                  <!-- Чётная (Denominator) -->
                  <div class="week-half">
                    <div class="week-label den-label">Чёт.</div>
                    <div
                      cdkDropList
                      [id]="cellIdDen(d, p)"
                      [cdkDropListData]="getCellDen(d, p)"
                      [cdkDropListConnectedTo]="allCellIds"
                      (cdkDropListDropped)="onDrop($event, d, p, 'Denominator')"
                      class="drop-zone">
                      <div
                        *ngFor="let entry of getCellDen(d, p)"
                        cdkDrag
                        [cdkDragData]="entry"
                        class="entry-card den"
                        [class.online]="entry.isOnline"
                        [class.both-weeks]="entry.weekType === 'Both'">
                        <div class="entry-header">
                          <span class="lesson-type-badge">{{ entry.lessonType | lessonType }}</span>
                          <button mat-icon-button class="delete-btn" (click)="deleteEntry(entry)" [matTooltip]="'Удалить'">
                            <mat-icon>close</mat-icon>
                          </button>
                        </div>
                        <div class="subject">{{ entry.subjectShortName || entry.subjectName }}</div>
                        <div class="teacher">{{ entry.teacherDisplayName }}</div>
                        <div class="room">
                          {{ entry.isOnline ? 'Онлайн' : (entry.buildingShortCode ? entry.buildingShortCode + '-' : '') + (entry.roomNumber || '—') }}
                        </div>
                        <div class="groups" *ngIf="entry.studentGroups?.length">{{ groupNames(entry) }}</div>
                        <div *cdkDragPlaceholder class="drag-placeholder"></div>
                      </div>
                      <div class="empty-slot" *ngIf="getCellDen(d, p).length === 0"></div>
                    </div>
                  </div>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .grid-wrapper { overflow-x: auto; }
    .schedule-grid {
      border-collapse: collapse;
      width: 100%;
      min-width: 900px;
    }
    th, td {
      border: 1px solid #e0e0e0;
      vertical-align: top;
    }
    th {
      background: #f5f5f5;
      text-align: center;
      font-weight: 600;
      padding: 8px 4px;
    }
    .pair-col { width: 90px; min-width: 90px; }
    .pair-cell {
      text-align: center;
      background: #fafafa;
      padding: 8px 4px;
    }
    .pair-number { font-weight: 700; font-size: 18px; }
    .pair-time { font-size: 10px; color: #666; }
    .drop-cell { padding: 0; min-width: 150px; }
    .split-weeks { display: flex; flex-direction: column; }
    .week-half { flex: 1; min-width: 0; }
    .week-label {
      font-size: 9px; font-weight: 700; text-align: center;
      padding: 1px 2px;
    }
    .num-label { background: rgba(25,118,210,0.1); color: #1565c0; }
    .den-label { background: rgba(46,125,50,0.1); color: #2e7d32; }
    .week-divider { height: 1px; background: #e0e0e0; }
    .drop-zone {
      min-height: 40px;
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 2px;
      border-radius: 0;
      transition: background 0.2s;
    }
    .drop-zone.cdk-drop-list-dragging { background: #e3f2fd; }
    .drop-zone.cdk-drop-list-receiving { background: #fff8e1; }
    .entry-card {
      background: #e3f2fd;
      border-left: 3px solid #1976d2;
      border-radius: 3px;
      padding: 3px 5px 3px 4px;
      cursor: grab;
      position: relative;
      font-size: 12px;
    }
    .entry-card.den { background: #e8f5e9; border-left-color: #2e7d32; }
    .entry-card.online { background: #e8f5e9; border-left-color: #388e3c; }
    .entry-card.both-weeks { border-left-style: dashed; }
    .entry-card:active { cursor: grabbing; }
    .entry-card.cdk-drag-dragging { opacity: 0.85; box-shadow: 0 4px 12px rgba(0,0,0,0.2); }
    .entry-header {
      display: flex; align-items: center; gap: 2px; margin-bottom: 2px;
    }
    .lesson-type-badge { font-size: 9px; color: #666; flex: 1; }
    .delete-btn {
      width: 18px; height: 18px; line-height: 18px; font-size: 12px;
      margin-left: auto; opacity: 0.5;
    }
    .delete-btn:hover { opacity: 1; }
    .delete-btn mat-icon { font-size: 14px; }
    .subject { font-weight: 600; font-size: 12px; line-height: 1.3; }
    .teacher { font-size: 11px; color: #444; }
    .room { font-size: 10px; color: #666; }
    .groups { font-size: 10px; color: #888; font-style: italic; }
    .drag-placeholder {
      background: #bbdefb; border: 2px dashed #1976d2;
      border-radius: 3px; min-height: 30px;
    }
    .empty-slot { min-height: 20px; }
  `]
})
export class ScheduleGridComponent implements OnChanges {
  @Input() scheduleId!: string;
  @Input() entries: ScheduleEntry[] = [];
  @Input() groups: StudentGroup[] = [];
  @Input() teachers: Teacher[] = [];
  @Output() entryMoved = new EventEmitter<{ entryId: string; dto: MoveEntryDto }>();
  @Output() entryDeleted = new EventEmitter<string>();

  days = [...DAYS];
  pairs = [...PAIRS];
  pairTimes = PAIR_TIMES;

  allCellIds: string[] = [];
  private cellMapNum = new Map<string, ScheduleEntry[]>();
  private cellMapDen = new Map<string, ScheduleEntry[]>();

  constructor(private dialog: MatDialog) {}

  ngOnChanges(): void {
    this.buildCellMaps();
    this.allCellIds = this.days.flatMap(d =>
      this.pairs.flatMap(p => [this.cellIdNum(d, p), this.cellIdDen(d, p)])
    );
  }

  private buildCellMaps(): void {
    this.cellMapNum.clear();
    this.cellMapDen.clear();
    for (const entry of this.entries) {
      const key = `${entry.dayOfWeek}-${entry.pairNumber}`;
      if (entry.weekType === 'Numerator' || entry.weekType === 'Both') {
        if (!this.cellMapNum.has(key)) this.cellMapNum.set(key, []);
        this.cellMapNum.get(key)!.push(entry);
      }
      if (entry.weekType === 'Denominator' || entry.weekType === 'Both') {
        if (!this.cellMapDen.has(key)) this.cellMapDen.set(key, []);
        this.cellMapDen.get(key)!.push(entry);
      }
    }
  }

  cellIdNum(day: RussianDayOfWeek, pair: number): string {
    return `cell-${day}-${pair}-num`;
  }

  cellIdDen(day: RussianDayOfWeek, pair: number): string {
    return `cell-${day}-${pair}-den`;
  }

  getCellNum(day: RussianDayOfWeek, pair: number): ScheduleEntry[] {
    return this.cellMapNum.get(`${day}-${pair}`) ?? [];
  }

  getCellDen(day: RussianDayOfWeek, pair: number): ScheduleEntry[] {
    return this.cellMapDen.get(`${day}-${pair}`) ?? [];
  }

  getPairTime(pair: number): string {
    const pt = this.pairTimes.find(p => p.pair === pair);
    return pt ? `${pt.start}–${pt.end}` : '';
  }

  onDrop(event: CdkDragDrop<ScheduleEntry[]>, targetDay: RussianDayOfWeek, targetPair: number, targetWeekType: string): void {
    const entry: ScheduleEntry = event.item.data;
    if (event.previousContainer === event.container) return;

    const dto: MoveEntryDto = {
      dayOfWeek: targetDay,
      pairNumber: targetPair,
      weekType: targetWeekType as WeekType,
      roomId: entry.roomId
    };

    this.entryMoved.emit({ entryId: entry.id, dto });
  }

  groupNames(entry: ScheduleEntry): string {
    return entry.studentGroups?.map(g => g.name).join(', ') ?? '';
  }

  deleteEntry(entry: ScheduleEntry): void {
    if (!confirm(`Удалить занятие "${entry.subjectName}"?`)) return;
    this.entryDeleted.emit(entry.id);
  }
}
