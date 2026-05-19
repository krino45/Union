import { Component, Input, Output, EventEmitter, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  CdkDragDrop, CdkDrag, CdkDropList, CdkDropListGroup
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

                  <!-- Нечётная (Odd) -->
                  <div class="week-half" *ngIf="weekFilter === 'Both' || weekFilter === 'Odd'">
                    <div class="half-header">
                      <span class="week-label num-label" *ngIf="weekFilter === 'Both'">Неч.</span>
                      <button class="add-btn num-add" *ngIf="!readonly" (click)="requestAdd(d, p, 'Odd', getCellNum(d, p)[0])" [title]="getCellNum(d, p).length > 0 ? 'Изменить занятие' : 'Добавить занятие'">{{ getCellNum(d, p).length > 0 ? '✎' : '+' }}</button>
                    </div>
                    <div
                      cdkDropList
                      [id]="cellIdNum(d, p)"
                      [cdkDropListData]="getCellNum(d, p)"
                      [cdkDropListConnectedTo]="allCellIds"
                      (cdkDropListDropped)="onDrop($event, d, p, 'Odd')"
                      class="drop-zone">
                      <div
                        *ngFor="let entry of getCellNum(d, p)"
                        cdkDrag
                        [cdkDragData]="entry"
                        [cdkDragDisabled]="readonly"
                        class="entry-card"
                        [class.online]="entry.isOnline"
                        [class.both-weeks]="entry.weekType === 'Both'"
                        [class.readonly]="readonly">
                        <div class="entry-header">
                          <span class="lt-badge">{{ entry.lessonType | lessonType }}</span>
                          <button class="delete-btn" *ngIf="!readonly" (click)="$event.stopPropagation(); deleteEntry(entry)" title="Удалить">×</button>
                        </div>
                        <div class="subject">{{ entry.subjectShortName || entry.subjectName }}</div>
                        <div class="teacher">{{ entry.teacherDisplayName }}</div>
                        <div class="room">{{ roomLabel(entry) }}</div>
                        <div class="groups" *ngIf="entry.studentGroups?.length">{{ groupNames(entry) }}</div>
                        <div *cdkDragPlaceholder class="drag-placeholder"></div>
                      </div>
                      <div class="empty-slot" *ngIf="getCellNum(d, p).length === 0"></div>
                    </div>
                  </div>

                  <div class="week-divider" *ngIf="weekFilter === 'Both'"></div>

                  <!-- Чётная (Even) -->
                  <div class="week-half" *ngIf="weekFilter === 'Both' || weekFilter === 'Even'">
                    <div class="half-header">
                      <span class="week-label den-label" *ngIf="weekFilter === 'Both'">Чёт.</span>
                      <button class="add-btn den-add" *ngIf="!readonly" (click)="requestAdd(d, p, 'Even', getCellDen(d, p)[0])" [title]="getCellDen(d, p).length > 0 ? 'Изменить занятие' : 'Добавить занятие'">{{ getCellDen(d, p).length > 0 ? '✎' : '+' }}</button>
                    </div>
                    <div
                      cdkDropList
                      [id]="cellIdDen(d, p)"
                      [cdkDropListData]="getCellDen(d, p)"
                      [cdkDropListConnectedTo]="allCellIds"
                      (cdkDropListDropped)="onDrop($event, d, p, 'Even')"
                      class="drop-zone">
                      <div
                        *ngFor="let entry of getCellDen(d, p)"
                        cdkDrag
                        [cdkDragData]="entry"
                        [cdkDragDisabled]="readonly"
                        class="entry-card den"
                        [class.online]="entry.isOnline"
                        [class.both-weeks]="entry.weekType === 'Both'"
                        [class.readonly]="readonly">
                        <div class="entry-header">
                          <span class="lt-badge">{{ entry.lessonType | lessonType }}</span>
                          <button class="delete-btn" *ngIf="!readonly" (click)="$event.stopPropagation(); deleteEntry(entry)" title="Удалить">×</button>
                        </div>
                        <div class="subject">{{ entry.subjectShortName || entry.subjectName }}</div>
                        <div class="teacher">{{ entry.teacherDisplayName }}</div>
                        <div class="room">{{ roomLabel(entry) }}</div>
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
    .schedule-grid { border-collapse: collapse; width: 100%; min-width: 900px; }
    th, td { border: 1px solid #e0e0e0; vertical-align: top; }
    th { background: #f5f5f5; text-align: center; font-weight: 600; padding: 8px 4px; }
    .pair-col { width: 90px; min-width: 90px; }
    .pair-cell { text-align: center; background: #fafafa; padding: 8px 4px; }
    .pair-number { font-weight: 700; font-size: 18px; }
    .pair-time { font-size: 10px; color: #666; }
    .drop-cell { padding: 0; min-width: 150px; }
    .split-weeks { display: flex; flex-direction: column; height: 100%; }
    .week-half { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .half-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 1px 3px; min-height: 20px; gap: 2px;
    }
    .week-label {
      font-size: 9px; font-weight: 700; padding: 1px 4px; border-radius: 3px; flex: 1;
    }
    .num-label { background: rgba(25,118,210,0.12); color: #1565c0; }
    .den-label { background: rgba(46,125,50,0.12); color: #2e7d32; }
    .add-btn {
      border: none; border-radius: 3px; cursor: pointer;
      font-size: 14px; font-weight: 600; line-height: 1;
      padding: 0 4px; height: 18px; min-width: 18px;
      opacity: 0.45; transition: opacity 0.15s, background 0.15s;
      flex-shrink: 0;
    }
    .add-btn:hover { opacity: 1; }
    .num-add { background: rgba(25,118,210,0.15); color: #1565c0; }
    .den-add { background: rgba(46,125,50,0.15); color: #2e7d32; }
    .week-divider { height: 1px; background: #e0e0e0; flex-shrink: 0; }
    .drop-zone {
      flex: 1; min-height: 32px; display: flex; flex-direction: column;
      gap: 2px; padding: 2px; transition: background 0.2s;
    }
    .drop-zone.cdk-drop-list-dragging { background: #e3f2fd; }
    .drop-zone.cdk-drop-list-receiving { background: #fff8e1; }
    .entry-card {
      background: #e3f2fd; border-left: 3px solid #1976d2;
      border-radius: 3px; padding: 3px 5px 3px 4px;
      cursor: grab; position: relative; font-size: 12px;
    }
    .entry-card.den { background: #e8f5e9; border-left-color: #2e7d32; }
    .entry-card.online { background: #e8f5e9; border-left-color: #388e3c; }
    .entry-card.both-weeks { border-left-style: dashed; }
    .entry-card:active { cursor: grabbing; }
    .entry-card.readonly { cursor: default; }
    .entry-card.cdk-drag-dragging { opacity: 0.85; box-shadow: 0 4px 12px rgba(0,0,0,0.2); }
    .entry-header { display: flex; align-items: center; margin-bottom: 2px; }
    .lt-badge { font-size: 9px; color: #666; flex: 1; }
    .delete-btn {
      border: none; background: transparent; cursor: pointer;
      font-size: 14px; line-height: 1; padding: 0 2px;
      color: #888; opacity: 0.5; transition: opacity 0.15s;
    }
    .delete-btn:hover { opacity: 1; color: #c62828; }
    .subject { font-weight: 600; font-size: 12px; line-height: 1.3; }
    .teacher { font-size: 11px; color: #444; }
    .room { font-size: 10px; color: #666; }
    .groups { font-size: 10px; color: #888; font-style: italic; }
    .drag-placeholder { background: #bbdefb; border: 2px dashed #1976d2; border-radius: 3px; min-height: 28px; }
    .empty-slot { min-height: 16px; }
  `]
})
export class ScheduleGridComponent implements OnChanges {
  @Input() scheduleId!: string;
  @Input() entries: ScheduleEntry[] = [];
  @Input() groups: StudentGroup[] = [];
  @Input() teachers: Teacher[] = [];
  @Input() weekFilter: string = 'Both';
  @Input() readonly = false;
  @Output() entryMoved = new EventEmitter<{ entryId: string; dto: MoveEntryDto }>();
  @Output() entrySplit = new EventEmitter<{ entry: ScheduleEntry; sourceWeekType: WeekType; dto: MoveEntryDto }>();
  @Output() entryDeleted = new EventEmitter<string>();
  @Output() addRequested = new EventEmitter<{ day: RussianDayOfWeek; pair: number; weekType: string; existingEntry?: ScheduleEntry }>();

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
      if (entry.weekType === 'Odd' || entry.weekType === 'Both') {
        if (!this.cellMapNum.has(key)) this.cellMapNum.set(key, []);
        this.cellMapNum.get(key)!.push(entry);
      }
      if (entry.weekType === 'Even' || entry.weekType === 'Both') {
        if (!this.cellMapDen.has(key)) this.cellMapDen.set(key, []);
        this.cellMapDen.get(key)!.push(entry);
      }
    }
  }

  cellIdNum(day: RussianDayOfWeek, pair: number): string { return `cell-${day}-${pair}-num`; }
  cellIdDen(day: RussianDayOfWeek, pair: number): string { return `cell-${day}-${pair}-den`; }

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

  roomLabel(entry: ScheduleEntry): string {
    if (entry.isOnline) return 'Онлайн';
    return (entry.buildingShortCode ? entry.buildingShortCode + '-' : '') + (entry.roomNumber || '—');
  }

  groupNames(entry: ScheduleEntry): string {
    return entry.studentGroups?.map(g => g.name).join(', ') ?? '';
  }

  onDrop(event: CdkDragDrop<ScheduleEntry[]>, targetDay: RussianDayOfWeek, targetPair: number, targetWeekType: string): void {
    const entry: ScheduleEntry = event.item.data;
    if (event.previousContainer === event.container) return;
    const sourceWeekType: WeekType = event.previousContainer.id.endsWith('-num') ? WeekType.Odd : WeekType.Even;
    const dto: MoveEntryDto = { dayOfWeek: targetDay, pairNumber: targetPair, weekType: targetWeekType as WeekType, roomId: entry.roomId };
    if (entry.weekType === WeekType.Both) {
      this.entrySplit.emit({ entry, sourceWeekType, dto });
    } else {
      this.entryMoved.emit({ entryId: entry.id, dto });
    }
  }

  requestAdd(day: RussianDayOfWeek, pair: number, weekType: string, existingEntry?: ScheduleEntry): void {
    this.addRequested.emit({ day, pair, weekType, existingEntry });
  }

  deleteEntry(entry: ScheduleEntry): void {
    if (!confirm(`Удалить занятие "${entry.subjectName}"?`)) return;
    this.entryDeleted.emit(entry.id);
  }
}
