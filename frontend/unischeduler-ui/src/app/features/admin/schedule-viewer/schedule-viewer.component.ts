import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, ScheduleEntry, Room, Teacher } from '../../../core/models';
import { ScheduleTableComponent } from '../../../shared/components/schedule-table/schedule-table.component';
import { SearchSelectComponent } from '../../../shared/components/search-select.component';

@Component({
  selector: 'app-schedule-viewer',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    MatFormFieldModule, MatSelectModule, MatProgressSpinnerModule,
    MatIconModule, MatButtonModule,
    ScheduleTableComponent, SearchSelectComponent
  ],
  template: `
    <div class="viewer-header">
      <div class="header-left">
        <h1>Расписание</h1>
        <p class="subtitle" *ngIf="selectedSchedule">
          {{ selectedSchedule.academicYear }}/{{ selectedSchedule.academicYear + 1 }} —
          {{ selectedSchedule.term === 'First' ? '1-й' : '2-й' }} сем.
        </p>
      </div>
      <div class="header-filters">
        <mat-form-field appearance="outline" class="schedule-pick">
          <mat-label>Расписание</mat-label>
          <mat-select [(ngModel)]="selectedScheduleId" (ngModelChange)="onScheduleChange()">
            <mat-option *ngFor="let s of schedules" [value]="s.id">
              {{ s.academicYear }}/{{ s.academicYear + 1 }} — {{ s.term === 'First' ? '1-й' : '2-й' }} сем.
              <span class="sched-status" *ngIf="s.status === 'Draft'"> (черновик)</span>
            </mat-option>
          </mat-select>
        </mat-form-field>
        <app-search-select class="filter-field" label="Аудитория" [options]="rooms"
          [displayWith]="roomLabel" [allowNull]="true" nullLabel="Все"
          [(ngModel)]="selectedRoomId" (ngModelChange)="onFilterChange()"></app-search-select>
        <app-search-select class="filter-field" label="Преподаватель" [options]="teachers"
          displayField="displayName" [allowNull]="true" nullLabel="Все"
          [(ngModel)]="selectedTeacherId" (ngModelChange)="onFilterChange()"></app-search-select>
      </div>
    </div>

    <div *ngIf="loading" class="loading">
      <mat-spinner diameter="48"></mat-spinner>
    </div>

    <app-schedule-table
      *ngIf="!loading && entries.length > 0"
      [entries]="entries">
    </app-schedule-table>

    <div *ngIf="!loading && entries.length === 0 && selectedScheduleId" class="empty">
      <mat-icon>event_busy</mat-icon>
      <span>Нет занятий по заданным фильтрам.</span>
    </div>
    <div *ngIf="!selectedScheduleId && !loading" class="empty">
      <mat-icon>calendar_today</mat-icon>
      <span>Нет опубликованных расписаний.</span>
    </div>
  `,
  styles: [`
    .viewer-header {
      display: flex; justify-content: space-between; align-items: flex-start;
      margin-bottom: 16px; flex-wrap: wrap; gap: 12px;
    }
    h1 { margin: 0; }
    .subtitle { margin: 4px 0 0; color: #666; font-size: 13px; }
    .header-filters { display: flex; gap: 8px; flex-wrap: wrap; align-items: flex-start; }
    .schedule-pick { min-width: 200px; }
    .filter-field { min-width: 160px; }
    .sched-status { color: #e65100; font-size: 11px; }
    .loading, .empty { text-align: center; padding: 48px; color: #888;
      display: flex; flex-direction: column; align-items: center; gap: 8px; }
    .empty mat-icon { font-size: 40px; width: 40px; height: 40px; color: #bdbdbd; }
  `]
})
export class ScheduleViewerComponent implements OnInit {
  schedules: Schedule[] = [];
  entries: ScheduleEntry[] = [];
  rooms: Room[] = [];
  teachers: Teacher[] = [];
  loading = false;
  selectedScheduleId: string | null = null;
  selectedRoomId: string | null = null;
  selectedTeacherId: string | null = null;

  get selectedSchedule(): Schedule | null {
    return this.schedules.find(s => s.id === this.selectedScheduleId) ?? null;
  }

  roomLabel = (r: Room): string => r.buildingShortCode ? `${r.buildingShortCode}-${r.number}` : r.number;

  constructor(private api: ApiService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    const preTeacherId = qp.get('teacherId');
    const preRoomId = qp.get('roomId');

    forkJoin({
      schedules: this.api.getSchedules(),
      rooms: this.api.getRooms(),
      teachers: this.api.getTeachers()
    }).subscribe(({ schedules, rooms, teachers }) => {
      this.schedules = schedules.filter(s => s.status === 'Published' || s.status === 'Draft')
        .sort((a, b) => b.academicYear - a.academicYear || (a.term === 'First' ? 1 : -1));
      this.rooms = rooms;
      this.teachers = teachers;

      if (preTeacherId) this.selectedTeacherId = preTeacherId;
      if (preRoomId) this.selectedRoomId = preRoomId;

      if (this.schedules.length > 0) {
        const published = this.schedules.find(s => s.status === 'Published');
        this.selectedScheduleId = (published ?? this.schedules[0]).id;
        this.loadEntries();
      }
    });
  }

  onScheduleChange(): void { this.loadEntries(); }
  onFilterChange(): void { this.loadEntries(); }

  loadEntries(): void {
    if (!this.selectedScheduleId) return;
    this.loading = true;
    this.api.getScheduleEntries(this.selectedScheduleId, {
      teacherId: this.selectedTeacherId ?? undefined,
      roomId: this.selectedRoomId ?? undefined
    }).subscribe({
      next: data => { this.entries = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }
}
