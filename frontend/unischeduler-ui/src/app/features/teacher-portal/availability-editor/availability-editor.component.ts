import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { TeacherAvailability } from '../../../core/models';
import { RussianDayOfWeek, WeekType } from '../../../core/models/enums';
import { DayOfWeekPipe } from '../../../shared/pipes/day-of-week.pipe';
import { PAIR_TIMES, DAYS, PAIRS } from '../../../shared/constants/pairs';

@Component({
  selector: 'app-availability-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatInputModule,
    MatTooltipModule, MatSnackBarModule, DayOfWeekPipe
  ],
  template: `
    <div class="page-header">
      <h1>Моя занятость</h1>
      <div class="legend">
        <span class="legend-blocked">Заблокировано</span>
        <span class="legend-free">Свободно</span>
      </div>
    </div>

    <mat-card>
      <div class="week-selector">
        <mat-form-field appearance="outline">
          <mat-label>Тип недели</mat-label>
          <mat-select [(ngModel)]="selectedWeekType" (ngModelChange)="updateView()">
            <mat-option value="Both">Каждую неделю</mat-option>
            <mat-option value="Numerator">Нечётная</mat-option>
            <mat-option value="Denominator">Чётная</mat-option>
          </mat-select>
        </mat-form-field>
        <span class="hint">Нажмите на ячейку, чтобы заблокировать/разблокировать слот</span>
      </div>

      <table class="avail-table">
        <thead>
          <tr>
            <th>Пара</th>
            <th *ngFor="let d of days">{{ d | dayOfWeek: 'short' }}</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let p of pairs">
            <td class="pair-label">
              <div>{{ p }}</div>
              <div class="time">{{ getPairTime(p) }}</div>
            </td>
            <td *ngFor="let d of days"
                class="avail-cell"
                [class.blocked]="isBlocked(d, p)"
                (click)="toggleBlock(d, p)"
                [matTooltip]="isBlocked(d, p) ? 'Нажмите для разблокировки' : 'Нажмите для блокировки'">
              <mat-icon *ngIf="isBlocked(d, p)">block</mat-icon>
            </td>
          </tr>
        </tbody>
      </table>
    </mat-card>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .legend { display: flex; gap: 16px; align-items: center; }
    .legend-blocked { display: inline-block; width: 20px; height: 20px; background: #ffcdd2; border-radius: 3px; vertical-align: middle; }
    .legend-free { display: inline-block; width: 20px; height: 20px; background: #e8f5e9; border-radius: 3px; vertical-align: middle; }
    .week-selector { display: flex; align-items: center; gap: 16px; margin-bottom: 16px; }
    .hint { font-size: 12px; color: #888; }
    .avail-table { border-collapse: collapse; width: 100%; }
    .avail-table th, .avail-table td { border: 1px solid #e0e0e0; text-align: center; padding: 4px; min-width: 80px; }
    .avail-table th { background: #f5f5f5; font-weight: 600; }
    .pair-label { font-weight: 600; font-size: 15px; }
    .time { font-size: 10px; color: #888; }
    .avail-cell { cursor: pointer; height: 50px; background: #e8f5e9; transition: background 0.15s; }
    .avail-cell:hover { background: #c8e6c9; }
    .avail-cell.blocked { background: #ffcdd2; }
    .avail-cell.blocked:hover { background: #ef9a9a; }
    mat-icon { color: #c62828; font-size: 20px; }
  `]
})
export class AvailabilityEditorComponent implements OnInit {
  days = [...DAYS];
  pairs = [...PAIRS];
  pairTimes = PAIR_TIMES;
  selectedWeekType: string = WeekType.Both;
  availabilities: TeacherAvailability[] = [];
  blockedSet = new Set<string>();

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    const teacherId = this.auth.currentUser?.teacherId;
    if (!teacherId) return;
    this.api.getAvailability(teacherId).subscribe(data => {
      this.availabilities = data;
      this.updateView();
    });
  }

  updateView(): void {
    this.blockedSet.clear();
    for (const a of this.availabilities) {
      if (
        a.weekType === this.selectedWeekType ||
        a.weekType === WeekType.Both ||
        this.selectedWeekType === WeekType.Both
      ) {
        this.blockedSet.add(`${a.dayOfWeek}-${a.pairNumber}`);
      }
    }
  }

  isBlocked(day: RussianDayOfWeek, pair: number): boolean {
    return this.blockedSet.has(`${day}-${pair}`);
  }

  toggleBlock(day: RussianDayOfWeek, pair: number): void {
    const teacherId = this.auth.currentUser?.teacherId;
    if (!teacherId) return;

    const existing = this.availabilities.find(
      a => a.dayOfWeek === day && a.pairNumber === pair &&
           (a.weekType === this.selectedWeekType || a.weekType === WeekType.Both)
    );

    if (existing) {
      this.api.deleteAvailability(existing.id).subscribe({
        next: () => {
          this.availabilities = this.availabilities.filter(a => a.id !== existing.id);
          this.updateView();
        },
        error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 3000 })
      });
    } else {
      this.api.createAvailability({
        teacherId,
        dayOfWeek: day,
        pairNumber: pair,
        weekType: this.selectedWeekType as WeekType,
        isRecurring: true
      }).subscribe({
        next: (a) => {
          this.availabilities.push(a);
          this.updateView();
        },
        error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 3000 })
      });
    }
  }

  getPairTime(pair: number): string {
    const pt = this.pairTimes.find(p => p.pair === pair);
    return pt ? `${pt.start}–${pt.end}` : '';
  }
}
