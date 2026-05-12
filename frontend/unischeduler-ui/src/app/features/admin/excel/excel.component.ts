import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { ApiService } from '../../../core/services/api.service';
import { Schedule, StudentGroup, Teacher } from '../../../core/models';

@Component({
  selector: 'app-excel',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatSelectModule, MatProgressBarModule,
    MatSnackBarModule, MatTableModule, MatChipsModule
  ],
  template: `
    <h1>Экспорт / Импорт Excel</h1>

    <!-- Export Section -->
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

    <!-- Import Section -->
    <mat-card class="section-card">
      <mat-card-header>
        <mat-card-title><mat-icon>upload</mat-icon> Импорт</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Расписание</mat-label>
            <mat-select [(ngModel)]="importScheduleId">
              <mat-option *ngFor="let s of schedules" [value]="s.id">{{ s.academicYear }}/{{ s.academicYear + 1 }} — {{ s.term === 'First' ? '1-й' : '2-й' }} сем.</mat-option>
            </mat-select>
          </mat-form-field>
          <div class="file-picker">
            <input #fileInput type="file" accept=".xlsx" style="display:none" (change)="onFileSelected($event)">
            <button mat-stroked-button (click)="fileInput.click()">
              <mat-icon>attach_file</mat-icon>
              {{ selectedFile ? selectedFile.name : 'Выбрать файл...' }}
            </button>
            <button mat-raised-button color="accent" [disabled]="!selectedFile || !importScheduleId || importing"
                    (click)="previewImport()">
              <mat-icon>preview</mat-icon> Предпросмотр
            </button>
          </div>
        </div>

        <mat-progress-bar *ngIf="importing" mode="indeterminate"></mat-progress-bar>

        <!-- Preview Results -->
        <div *ngIf="preview" class="preview-section">
          <h3>Предпросмотр импорта</h3>

          <div *ngIf="preview.errors?.length" class="error-list">
            <h4>Ошибки ({{ preview.errors.length }}):</h4>
            <div *ngFor="let e of preview.errors" class="error-item">
              Строка {{ e.row }}, Столбец {{ e.col }}: {{ e.message }}
            </div>
          </div>

          <div *ngIf="preview.warnings?.length" class="warning-list">
            <h4>Предупреждения ({{ preview.warnings.length }}):</h4>
            <div *ngFor="let w of preview.warnings" class="warning-item">{{ w }}</div>
          </div>

          <div class="valid-count">
            Корректных записей: <strong>{{ preview.validEntries?.length || 0 }}</strong>
          </div>

          <button mat-raised-button color="primary"
                  [disabled]="preview.errors?.length > 0"
                  (click)="confirmImport()">
            <mat-icon>check</mat-icon> Импортировать {{ preview.validEntries?.length || 0 }} записей
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
    .file-picker { display: flex; gap: 8px; align-items: center; }
    .preview-section { margin-top: 24px; border-top: 1px solid #e0e0e0; padding-top: 16px; }
    .error-list { background: #ffebee; border-radius: 4px; padding: 12px; margin-bottom: 12px; }
    .error-item { font-size: 13px; color: #c62828; margin: 4px 0; }
    .warning-list { background: #fff8e1; border-radius: 4px; padding: 12px; margin-bottom: 12px; }
    .warning-item { font-size: 13px; color: #e65100; margin: 4px 0; }
    .valid-count { margin: 12px 0; font-size: 15px; }
  `]
})
export class ExcelComponent implements OnInit {
  schedules: Schedule[] = [];
  groups: StudentGroup[] = [];
  teachers: Teacher[] = [];

  exportScheduleId: string | null = null;
  exportGroupId: string | null = null;
  exportTeacherId: string | null = null;

  importScheduleId: string | null = null;
  selectedFile: File | null = null;
  importing = false;
  preview: any = null;

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
      error: (e) => this.snackBar.open('Ошибка экспорта', 'OK', { duration: 4000 })
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.preview = null;
  }

  previewImport(): void {
    if (!this.selectedFile || !this.importScheduleId) return;
    this.importing = true;
    this.preview = null;
    this.api.previewImport(this.importScheduleId, this.selectedFile).subscribe({
      next: (data) => { this.preview = data; this.importing = false; },
      error: (e) => {
        this.importing = false;
        this.snackBar.open(e.error?.title || 'Ошибка разбора файла', 'OK', { duration: 4000 });
      }
    });
  }

  confirmImport(): void {
    if (!this.preview || !this.importScheduleId) return;
    this.importing = true;
    this.api.confirmImport(this.importScheduleId, this.preview).subscribe({
      next: (result) => {
        this.importing = false;
        this.preview = null;
        this.selectedFile = null;
        this.snackBar.open(`Импортировано: ${result.committed} занятий`, 'OK', { duration: 4000 });
      },
      error: (e) => {
        this.importing = false;
        this.snackBar.open(e.error?.title || 'Ошибка импорта', 'OK', { duration: 4000 });
      }
    });
  }
}
