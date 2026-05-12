import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ScheduleEntry } from '../../../core/models';
import { RussianDayOfWeek } from '../../../core/models/enums';
import { DayOfWeekPipe } from '../../pipes/day-of-week.pipe';
import { WeekTypePipe } from '../../pipes/week-type.pipe';
import { LessonTypePipe } from '../../pipes/lesson-type.pipe';
import { PAIR_TIMES, DAYS, PAIRS } from '../../constants/pairs';

interface CellData {
  numerator: ScheduleEntry[];
  denominator: ScheduleEntry[];
  both: ScheduleEntry[];
}

@Component({
  selector: 'app-entry-card',
  standalone: true,
  imports: [CommonModule, MatTooltipModule, LessonTypePipe],
  template: `
    <div class="entry-card"
         [class.online]="entry.isOnline"
         [matTooltip]="tooltip">
      <div class="subject">{{ entry.subjectShortName || entry.subjectName }}</div>
      <div class="teacher">{{ entry.teacherDisplayName }}</div>
      <div class="room">{{ entry.isOnline ? 'Онлайн' : (entry.buildingShortCode ? entry.buildingShortCode + '-' : '') + (entry.roomNumber || '—') }}</div>
      <div class="groups" *ngIf="entry.studentGroups?.length">{{ groupNames() }}</div>
    </div>
  `,
  styles: [`
    .entry-card {
      background: #e3f2fd;
      border-left: 3px solid #1976d2;
      border-radius: 3px;
      padding: 3px 5px;
      margin: 2px 0;
      cursor: default;
    }
    .entry-card.online { background: #e8f5e9; border-left-color: #388e3c; }
    .subject { font-weight: 600; font-size: 12px; }
    .teacher { font-size: 11px; color: #444; }
    .room { font-size: 10px; color: #666; }
    .groups { font-size: 10px; color: #888; font-style: italic; }
  `]
})
export class EntryCardComponent {
  @Input() entry!: ScheduleEntry;
  @Input() weekLabel: string = '';

  groupNames(): string {
    return this.entry.studentGroups?.map(g => g.name).join(', ') ?? '';
  }

  get tooltip(): string {
    return [
      this.entry.subjectName,
      this.entry.teacherDisplayName,
      this.entry.lessonType,
      this.entry.isOnline ? 'Онлайн' : (this.entry.buildingShortCode ? `${this.entry.buildingShortCode}-` : '') + (this.entry.roomNumber || ''),
      this.groupNames()
    ].filter(Boolean).join(' | ');
  }
}

@Component({
  selector: 'app-schedule-table',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatTooltipModule, DayOfWeekPipe, WeekTypePipe, LessonTypePipe, EntryCardComponent],
  template: `
    <div class="schedule-table-wrapper">
      <table class="schedule-table">
        <thead>
          <tr>
            <th class="pair-col">Пара</th>
            <th *ngFor="let d of days">{{ d | dayOfWeek: 'short' }}</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let p of pairs">
            <td class="pair-cell">
              <div class="pair-number">{{ p }}</div>
              <div class="pair-time">{{ getPairTime(p) }}</div>
            </td>
            <td *ngFor="let d of days" class="entry-cell">
              <ng-container *ngIf="getCell(d, p) as cell">
                <app-entry-card
                  *ngFor="let e of cell.both"
                  [entry]="e"
                  weekLabel="">
                </app-entry-card>
                <ng-container *ngIf="cell.numerator.length || cell.denominator.length">
                  <div class="split-cell">
                    <div class="split-half numerator">
                      <span class="week-badge">Н</span>
                      <app-entry-card
                        *ngFor="let e of cell.numerator"
                        [entry]="e"
                        weekLabel="Н">
                      </app-entry-card>
                    </div>
                    <div class="split-half denominator">
                      <span class="week-badge">Ч</span>
                      <app-entry-card
                        *ngFor="let e of cell.denominator"
                        [entry]="e"
                        weekLabel="Ч">
                      </app-entry-card>
                    </div>
                  </div>
                </ng-container>
              </ng-container>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .schedule-table-wrapper { overflow-x: auto; }
    .schedule-table {
      border-collapse: collapse; width: 100%; min-width: 900px; font-size: 13px;
    }
    th, td { border: 1px solid #e0e0e0; padding: 4px; vertical-align: top; min-width: 120px; }
    th { background: #f5f5f5; text-align: center; font-weight: 600; }
    .pair-col { min-width: 80px; width: 80px; }
    .pair-cell { text-align: center; background: #fafafa; }
    .pair-number { font-weight: 700; font-size: 16px; }
    .pair-time { font-size: 10px; color: #666; }
    .entry-cell { min-height: 60px; }
    .split-cell { display: flex; flex-direction: column; gap: 2px; }
    .split-half { border-radius: 4px; padding: 2px; }
    .numerator { background: rgba(33,150,243,0.07); }
    .denominator { background: rgba(76,175,80,0.07); }
    .week-badge { font-size: 9px; font-weight: 700; color: #888; }
  `]
})
export class ScheduleTableComponent implements OnChanges {
  @Input() entries: ScheduleEntry[] = [];

  days = [...DAYS];
  pairs = [...PAIRS];
  pairTimes = PAIR_TIMES;

  private cellMap = new Map<string, CellData>();

  ngOnChanges(): void {
    this.buildCellMap();
  }

  private buildCellMap(): void {
    this.cellMap.clear();
    for (const entry of this.entries) {
      const key = `${entry.dayOfWeek}-${entry.pairNumber}`;
      if (!this.cellMap.has(key)) {
        this.cellMap.set(key, { numerator: [], denominator: [], both: [] });
      }
      const cell = this.cellMap.get(key)!;
      if (entry.weekType === 'Both') cell.both.push(entry);
      else if (entry.weekType === 'Numerator') cell.numerator.push(entry);
      else cell.denominator.push(entry);
    }
  }

  getCell(day: RussianDayOfWeek, pair: number): CellData {
    return this.cellMap.get(`${day}-${pair}`) ?? { numerator: [], denominator: [], both: [] };
  }

  getPairTime(pair: number): string {
    const pt = this.pairTimes.find(p => p.pair === pair);
    return pt ? `${pt.start}–${pt.end}` : '';
  }
}
