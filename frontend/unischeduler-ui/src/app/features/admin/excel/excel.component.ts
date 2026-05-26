import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, StudentGroup, Teacher } from '../../../core/models';

@Component({
  selector: 'app-excel',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatSelectModule, MatSnackBarModule
  ],
  template: `
    <h1>Экспорт Excel</h1>

    <mat-card class="section-card">
      <mat-card-header>
        <mat-card-title><mat-icon>download</mat-icon> Экспорт</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Расписание</mat-label>
            <mat-select [(ngModel)]="exportScheduleId">
              <mat-option *ngFor="let s of schedules" [value]="s.id">{{ s.academicYear }}/{{ s.academicYear + 1 }} — {{ s.term === 'First' ? '1-й' : '2-й' }} сем.</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Группа (опц.)</mat-label>
            <mat-select [(ngModel)]="exportGroupId">
              <mat-option [value]="null">Все группы</mat-option>
              <mat-option *ngFor="let g of groups" [value]="g.id">{{ g.name }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Преподаватель (опц.)</mat-label>
            <mat-select [(ngModel)]="exportTeacherId">
              <mat-option [value]="null">Все</mat-option>
              <mat-option *ngFor="let t of teachers" [value]="t.id">{{ t.displayName }}</mat-option>
            </mat-select>
          </mat-form-field>
          <button mat-raised-button color="primary" [disabled]="!exportScheduleId" (click)="exportExcel()">
            <mat-icon>download</mat-icon> Скачать
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    h1 { margin-bottom: 24px; }
    .section-card { margin-bottom: 24px; }
    mat-card-title { display: flex; align-items: center; gap: 8px; }
    .form-row { display: flex; align-items: flex-end; flex-wrap: wrap; gap: 12px; margin-top: 16px; }
  `]
})
export class ExcelComponent implements OnInit {
  schedules: Schedule[] = [];
  groups: StudentGroup[] = [];
  teachers: Teacher[] = [];

  exportScheduleId: string | null = null;
  exportGroupId: string | null = null;
  exportTeacherId: string | null = null;

  constructor(private api: ApiService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getSchedules().subscribe(s => this.schedules = s);
    this.api.getGroups().subscribe(g => this.groups = g);
    this.api.getTeachers().subscribe(t => this.teachers = t);
  }

  exportExcel(): void {
    if (!this.exportScheduleId) return;
    this.api.exportExcel(
      this.exportScheduleId,
      this.exportGroupId ?? undefined,
      this.exportTeacherId ?? undefined
    ).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'schedule.xlsx';
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.snackBar.open('Ошибка экспорта', 'OK', { duration: 4000 })
    });
  }
}
