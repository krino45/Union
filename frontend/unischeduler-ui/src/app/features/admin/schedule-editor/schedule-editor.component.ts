import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, ScheduleEntry, StudentGroup, Teacher } from '../../../core/models';
import { ScheduleGridComponent } from './schedule-grid/schedule-grid.component';

@Component({
  selector: 'app-schedule-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatButtonModule, MatIconModule, MatSelectModule,
    MatFormFieldModule, MatSnackBarModule, MatProgressSpinnerModule,
    ScheduleGridComponent
  ],
  template: `
    <div class="editor-header">
      <div>
        <h1>{{ schedule ? (schedule.academicYear + '/' + (schedule.academicYear + 1) + ' — ' + (schedule.term === 'First' ? '1-й' : '2-й') + ' сем.') : 'Редактор расписания' }}</h1>
        <p class="subtitle" *ngIf="schedule">
          {{ schedule.startDate | date:'dd.MM.yyyy' }} – {{ schedule.endDate | date:'dd.MM.yyyy' }}
        </p>
      </div>
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
      (entryMoved)="onEntryMoved($event)"
      (entryDeleted)="onEntryDeleted($event)">
    </app-schedule-grid>
  `,
  styles: [`
    .editor-header {
      display: flex; justify-content: space-between; align-items: flex-start;
      margin-bottom: 24px; flex-wrap: wrap; gap: 16px;
    }
    h1 { margin: 0; }
    .subtitle { margin: 4px 0 0; color: #666; font-size: 14px; }
    .header-filters { display: flex; gap: 12px; }
    .filter-field { min-width: 180px; }
    .loading-state { display: flex; justify-content: center; padding: 64px; }
  `]
})
export class ScheduleEditorComponent implements OnInit {
  schedule: Schedule | null = null;
  entries: ScheduleEntry[] = [];
  groups: StudentGroup[] = [];
  teachers: Teacher[] = [];
  loading = true;
  selectedGroupId: string | null = null;
  selectedTeacherId: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    Promise.all([
      this.api.getSchedule(id).toPromise(),
      this.api.getGroups().toPromise(),
      this.api.getTeachers().toPromise()
    ]).then(([schedule, groups, teachers]) => {
      this.schedule = schedule!;
      this.groups = groups!;
      this.teachers = teachers!;
      this.loadEntries();
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

  onFilterChange(): void {
    this.loadEntries();
  }

  onEntryMoved(event: { entryId: string; dto: any }): void {
    this.api.moveEntry(event.entryId, event.dto).subscribe({
      next: () => {
        this.snackBar.open('Занятие перенесено', 'OK', { duration: 2000 });
        this.loadEntries();
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
        this.loadEntries();
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка удаления', 'OK', { duration: 4000 })
    });
  }
}
