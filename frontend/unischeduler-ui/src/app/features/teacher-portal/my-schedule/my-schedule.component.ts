import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { Schedule, ScheduleEntry } from '../../../core/models';
import { ScheduleTableComponent } from '../../../shared/components/schedule-table/schedule-table.component';

@Component({
  selector: 'app-my-schedule',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatFormFieldModule, MatSelectModule, MatProgressSpinnerModule,
    ScheduleTableComponent
  ],
  template: `
    <div class="page-header">
      <h1>Моё расписание</h1>
      <mat-form-field appearance="outline" class="schedule-picker">
        <mat-label>Расписание</mat-label>
        <mat-select [(ngModel)]="selectedScheduleId" (ngModelChange)="loadEntries()">
          <mat-option *ngFor="let s of schedules" [value]="s.id">{{ s.academicYear }}/{{ s.academicYear + 1 }} — {{ s.term === 'First' ? '1-й' : '2-й' }} сем.</mat-option>
        </mat-select>
      </mat-form-field>
    </div>

    <div *ngIf="loading" class="loading">
      <mat-spinner diameter="48"></mat-spinner>
    </div>

    <app-schedule-table
      *ngIf="!loading && entries.length > 0"
      [entries]="entries">
    </app-schedule-table>

    <div *ngIf="!loading && entries.length === 0 && selectedScheduleId" class="empty">
      Нет занятий в этом расписании.
    </div>
    <div *ngIf="!selectedScheduleId" class="empty">
      Выберите расписание.
    </div>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .schedule-picker { min-width: 220px; }
    .loading, .empty { text-align: center; padding: 48px; color: #888; }
  `]
})
export class MyScheduleComponent implements OnInit {
  schedules: Schedule[] = [];
  entries: ScheduleEntry[] = [];
  selectedScheduleId: string | null = null;
  loading = false;

  constructor(private api: ApiService, private auth: AuthService) {}

  ngOnInit(): void {
    this.api.getSchedules().subscribe(data => {
      this.schedules = data.filter(s => s.status === 'Published' || s.status === 'Draft');
      if (this.schedules.length > 0) {
        this.selectedScheduleId = this.schedules[0].id;
        this.loadEntries();
      }
    });
  }

  loadEntries(): void {
    if (!this.selectedScheduleId) return;
    this.loading = true;
    const teacherId = this.auth.currentUser?.teacherId;
    this.api.getScheduleEntries(this.selectedScheduleId, { teacherId }).subscribe({
      next: data => { this.entries = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }
}
