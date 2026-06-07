import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatExpansionModule } from '@angular/material/expansion';
import { ApiService } from '../../../core/services/api.service';
import { BackfillPreview, BackfillTargets } from '../../../core/models';
import { LessonTypePipe } from '../../../shared/pipes/lesson-type.pipe';

@Component({
  selector: 'app-backfill-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatIconModule, MatCheckboxModule,
    MatDialogModule, MatProgressSpinnerModule, MatChipsModule, MatSnackBarModule,
    MatExpansionModule, LessonTypePipe
  ],
  template: `
    <h2 mat-dialog-title>Заполнить настройки из расписания</h2>
    <mat-dialog-content>
      <p class="hint">
        Анализирует занятия текущего расписания и достаёт из них информацию.
        Аудитории и дисциплины дополняются; часы учебных планов приводятся
        к расписанию (могут быть перезаписаны).
      </p>

      <div class="targets">
        <mat-checkbox [(ngModel)]="targets.rooms">Разрешенные типы занятий в аудиториях</mat-checkbox>
        <mat-checkbox [(ngModel)]="targets.teachers">Дисциплины преподавателей</mat-checkbox>
        <mat-checkbox [(ngModel)]="targets.studyPlans">Часы учебных планов</mat-checkbox>
        <mat-checkbox [(ngModel)]="targets.roomBindings">Закрепление аудиторий за лабораторными</mat-checkbox>
      </div>

      <div class="note" *ngIf="targets.rooms">
        <mat-icon class="note-icon">info</mat-icon>
        Аудитории без ограничений («Любые») остаются без изменений — иначе их бы пришлось ограничить.
      </div>

      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="36"></mat-spinner></div>

      <ng-container *ngIf="preview && !loading">
        <div class="empty" *ngIf="isEmpty()">
          <mat-icon>check_circle</mat-icon> Нечего добавлять.
        </div>

        <mat-accordion multi *ngIf="!isEmpty()">
          <mat-expansion-panel *ngIf="preview.rooms.length" expanded>
            <mat-expansion-panel-header>
              <mat-panel-title>Аудитории ({{ preview.rooms.length }})</mat-panel-title>
            </mat-expansion-panel-header>
            <div class="change-row" *ngFor="let r of preview.rooms">
              <span class="entity">{{ r.roomLabel }}</span>
              <span class="adds">
                <mat-chip *ngFor="let t of r.addedTypes" class="add-chip">+ {{ t | lessonType }}</mat-chip>
              </span>
            </div>
          </mat-expansion-panel>

          <mat-expansion-panel *ngIf="preview.teachers.length" expanded>
            <mat-expansion-panel-header>
              <mat-panel-title>Преподаватели ({{ preview.teachers.length }})</mat-panel-title>
            </mat-expansion-panel-header>
            <div class="change-row" *ngFor="let t of preview.teachers">
              <span class="entity">{{ t.teacherName }}</span>
              <span class="adds">
                <mat-chip *ngFor="let a of t.added" class="add-chip">
                  + {{ a.subjectName }} · {{ a.lessonType | lessonType }}
                </mat-chip>
              </span>
            </div>
          </mat-expansion-panel>

          <mat-expansion-panel *ngIf="preview.roomBindings.length" expanded>
            <mat-expansion-panel-header>
              <mat-panel-title>Закрепление аудиторий (лаб.) ({{ preview.roomBindings.length }})</mat-panel-title>
            </mat-expansion-panel-header>
            <div class="change-row" *ngFor="let b of preview.roomBindings">
              <span class="entity">{{ b.subjectName }} · {{ b.lessonType | lessonType }}</span>
              <span class="adds">
                <mat-chip *ngFor="let r of b.roomLabels" class="add-chip">+ {{ r }}</mat-chip>
              </span>
            </div>
          </mat-expansion-panel>

          <mat-expansion-panel *ngIf="preview.studyPlans.length" expanded>
            <mat-expansion-panel-header>
              <mat-panel-title>Учебные планы ({{ preview.studyPlans.length }})</mat-panel-title>
            </mat-expansion-panel-header>
            <div *ngFor="let p of preview.studyPlans" class="plan-block">
              <div class="plan-name">{{ p.planName }}</div>
              <div class="change-row" *ngFor="let c of p.changes">
                <span class="entity">{{ c.subjectName }}</span>
                <span class="adds">
                  <mat-chip class="add-chip">
                    {{ c.fieldLabel }}:
                    <span class="old-h" *ngIf="c.oldHours > 0">{{ c.oldHours }} →</span>
                    {{ c.newHours }} ч
                  </mat-chip>
                </span>
              </div>
            </div>
          </mat-expansion-panel>
        </mat-accordion>
      </ng-container>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Закрыть</button>
      <button mat-stroked-button (click)="loadPreview()" [disabled]="loading || !anyTarget()">
        <mat-icon>visibility</mat-icon> Предпросмотр
      </button>
      <button mat-raised-button color="primary" (click)="apply()"
              [disabled]="loading || !preview || isEmpty()">
        <mat-icon>save</mat-icon> Применить
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .hint { color: #666; font-size: 13px; margin: 0 0 12px; }
    .old-h { color: #b71c1c; text-decoration: line-through; opacity: 0.7; margin-right: 2px; }
    .targets { display: flex; flex-direction: column; gap: 6px; margin-bottom: 12px; }
    .note { display: flex; align-items: flex-start; gap: 6px; font-size: 12px; color: #1565c0;
      background: #e3f2fd; border-radius: 4px; padding: 6px 8px; margin-bottom: 12px; }
    .note-icon { font-size: 16px; height: 16px; width: 16px; flex-shrink: 0; margin-top: 1px; }
    .loading-wrap { display: flex; justify-content: center; padding: 24px; }
    .empty { display: flex; align-items: center; gap: 8px; color: #2e7d32; padding: 16px 0; }
    .change-row { display: flex; gap: 12px; align-items: baseline; padding: 3px 0; border-bottom: 1px solid #f0f0f0; }
    .entity { font-weight: 500; min-width: 160px; flex-shrink: 0; }
    .adds { display: flex; flex-wrap: wrap; gap: 4px; }
    .add-chip { background: #e8f5e9; color: #1b5e20; font-size: 11px; }
    .plan-block { margin-bottom: 10px; }
    .plan-name { font-weight: 600; margin: 6px 0 2px; }
    mat-dialog-content { min-width: 520px; max-width: 640px; }
    :host-context(body.dark-mode) .add-chip { background: #1b3a1d; color: #a5d6a7; }
    :host-context(body.dark-mode) .note { background: #16304a; color: #90caf9; }
  `]
})
export class BackfillDialogComponent {
  targets: BackfillTargets = { rooms: true, teachers: true, studyPlans: true, roomBindings: true };
  preview: BackfillPreview | null = null;
  loading = false;

  constructor(
    private dialogRef: MatDialogRef<BackfillDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { scheduleId: string },
    private api: ApiService,
    private snackBar: MatSnackBar
  ) {}

  anyTarget(): boolean {
    return this.targets.rooms || this.targets.teachers || this.targets.studyPlans || this.targets.roomBindings;
  }

  isEmpty(): boolean {
    return !!this.preview
      && this.preview.rooms.length === 0
      && this.preview.teachers.length === 0
      && this.preview.studyPlans.length === 0
      && this.preview.roomBindings.length === 0;
  }

  loadPreview(): void {
    this.loading = true;
    this.preview = null;
    this.api.previewBackfill(this.data.scheduleId, this.targets).subscribe({
      next: p => { this.preview = p; this.loading = false; },
      error: e => { this.loading = false; this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 }); }
    });
  }

  apply(): void {
    this.loading = true;
    this.api.applyBackfill(this.data.scheduleId, this.targets).subscribe({
      next: r => {
        this.loading = false;
        this.snackBar.open(
          `Аудитории: ${r.roomsUpdated}, дисциплины: ${r.teacherLinksAdded}, часы планов: ${r.studyPlanFieldsUpdated}, закрепл. аудиторий: ${r.roomBindingsAdded}`,
          'OK', { duration: 5000 });
        this.dialogRef.close(true);
      },
      error: e => { this.loading = false; this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 }); }
    });
  }
}
