import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { Subject, Teacher, StudentGroup, Room, ScheduleEntry, CreateScheduleEntryDto, UpdateScheduleEntryDto } from '../../../core/models';
import { RussianDayOfWeek, WeekType, LessonType } from '../../../core/models/enums';
import { DayOfWeekPipe } from '../../../shared/pipes/day-of-week.pipe';

export interface AddEntryDialogData {
  scheduleId: string;
  day: RussianDayOfWeek;
  pair: number;
  weekType: string;
  subjects: Subject[];
  teachers: Teacher[];
  groups: StudentGroup[];
  rooms: Room[];
  existingEntry?: ScheduleEntry;
}

export type AddEntryDialogResult =
  | { mode: 'create'; dto: CreateScheduleEntryDto }
  | { mode: 'update'; entryId: string; dto: UpdateScheduleEntryDto };

@Component({
  selector: 'app-add-entry-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatSelectModule, MatCheckboxModule,
    DayOfWeekPipe
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Редактировать занятие' : 'Добавить занятие' }}</h2>
    <mat-dialog-content>
      <div class="slot-info">
        {{ data.day | dayOfWeek: 'full' }}, пара {{ data.pair }}
        ({{ weekLabel }})
      </div>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Дисциплина</mat-label>
          <mat-select formControlName="subjectId">
            <mat-option *ngFor="let s of data.subjects" [value]="s.id">{{ s.name }}</mat-option>
          </mat-select>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Тип занятия</mat-label>
            <mat-select formControlName="lessonType">
              <mat-option value="Lecture">Лекция</mat-option>
              <mat-option value="Practical">Практика</mat-option>
              <mat-option value="Lab">Лаборатория</mat-option>
              <mat-option value="Seminar">Семинар</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="flex1">
            <mat-label>Тип недели</mat-label>
            <mat-select formControlName="weekType">
              <mat-option value="Both">Каждую</mat-option>
              <mat-option value="Odd">Нечётная</mat-option>
              <mat-option value="Even">Чётная</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Преподаватель</mat-label>
          <mat-select formControlName="teacherId">
            <mat-optgroup label="Назначены" *ngIf="primaryTeachers.length > 0">
              <mat-option *ngFor="let t of primaryTeachers" [value]="t.id">{{ t.displayName }}</mat-option>
            </mat-optgroup>
            <mat-optgroup label="Другой тип занятия" *ngIf="secondaryTeachers.length > 0">
              <mat-option *ngFor="let t of secondaryTeachers" [value]="t.id">{{ t.displayName }}</mat-option>
            </mat-optgroup>
            <mat-optgroup label="Без назначения" *ngIf="otherTeachers.length > 0">
              <mat-option *ngFor="let t of otherTeachers" [value]="t.id">{{ t.displayName }}</mat-option>
            </mat-optgroup>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Группы</mat-label>
          <mat-select formControlName="groupIds" multiple>
            <mat-option *ngFor="let g of data.groups" [value]="g.id">{{ g.name }}</mat-option>
          </mat-select>
        </mat-form-field>

        <div class="blocked-warn" *ngIf="hasBlockedDayWarning">
          ⚠ Одна или несколько выбранных групп заблокированы в этот день
        </div>

        <mat-checkbox formControlName="isOnline" class="online-check">Онлайн занятие</mat-checkbox>

        <mat-form-field appearance="outline" class="full" *ngIf="!form.value.isOnline">
          <mat-label>Аудитория (необязательно)</mat-label>
          <mat-select formControlName="roomId">
            <mat-option [value]="null">Не указано</mat-option>
            <mat-option *ngFor="let r of data.rooms" [value]="r.id">
              {{ r.buildingShortCode ? r.buildingShortCode + '-' : '' }}{{ r.number }} (вм. {{ r.capacity }})
            </mat-option>
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">
        {{ isEdit ? 'Сохранить' : 'Добавить' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .slot-info { font-size: 14px; color: #666; margin-bottom: 16px; }
    .form { display: flex; flex-direction: column; gap: 4px; min-width: 400px; }
    .full { width: 100%; }
    .row { display: flex; gap: 8px; }
    .flex1 { flex: 1; }
    .online-check { margin: 4px 0 8px; }
    .blocked-warn { background: #fff3e0; border: 1px solid #ffb300; border-radius: 4px; padding: 8px 12px; font-size: 13px; color: #e65100; margin: 4px 0; }
  `]
})
export class AddEntryDialogComponent {
  form: FormGroup;
  isEdit: boolean;

  get weekLabel(): string {
    const wt = this.form.value.weekType ?? this.data.weekType;
    if (wt === 'Odd') return 'нечётная';
    if (wt === 'Even') return 'чётная';
    return 'обе недели';
  }

  get primaryTeachers() {
    const { subjectId, lessonType } = this.form.value;
    if (!subjectId || !lessonType) return this.data.teachers;
    return this.data.teachers.filter(t =>
      t.subjects?.some(s => s.subjectId === subjectId && s.lessonType === lessonType)
    );
  }

  get secondaryTeachers() {
    const { subjectId, lessonType } = this.form.value;
    if (!subjectId || !lessonType) return [];
    const primaryIds = new Set(this.primaryTeachers.map(t => t.id));
    return this.data.teachers.filter(t =>
      !primaryIds.has(t.id) && t.subjects?.some(s => s.subjectId === subjectId)
    );
  }

  get otherTeachers() {
    const { subjectId } = this.form.value;
    if (!subjectId) return [];
    const primaryIds = new Set(this.primaryTeachers.map(t => t.id));
    const secondaryIds = new Set(this.secondaryTeachers.map(t => t.id));
    return this.data.teachers.filter(t => !primaryIds.has(t.id) && !secondaryIds.has(t.id));
  }

  get hasBlockedDayWarning(): boolean {
    const selectedIds: string[] = this.form.value.groupIds ?? [];
    return this.data.groups
      .filter(g => selectedIds.includes(g.id))
      .some(g => g.blockedDays?.includes(this.data.day));
  }

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<AddEntryDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AddEntryDialogData
  ) {
    const e = data.existingEntry;
    this.isEdit = !!e;
    this.form = this.fb.group({
      subjectId:  [e?.subjectId  ?? '',        Validators.required],
      lessonType: [e?.lessonType ?? 'Lecture', Validators.required],
      weekType:   [e?.weekType   ?? data.weekType, Validators.required],
      teacherId:  [e?.teacherId  ?? '',        Validators.required],
      groupIds:   [e?.studentGroups?.map(g => g.id) ?? [], Validators.required],
      isOnline:   [e?.isOnline   ?? false],
      roomId:     [e?.roomId     ?? null]
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    const roomId = v.isOnline ? undefined : (v.roomId ?? undefined);

    if (this.isEdit) {
      const dto: UpdateScheduleEntryDto = {
        subjectId:  v.subjectId,
        teacherId:  v.teacherId,
        roomId,
        groupIds:   v.groupIds,
        dayOfWeek:  this.data.day,
        pairNumber: this.data.pair,
        weekType:   v.weekType as WeekType,
        lessonType: v.lessonType as LessonType,
        isOnline:   v.isOnline
      };
      const result: AddEntryDialogResult = { mode: 'update', entryId: this.data.existingEntry!.id, dto };
      this.dialogRef.close(result);
    } else {
      const dto: CreateScheduleEntryDto = {
        scheduleId: this.data.scheduleId,
        subjectId:  v.subjectId,
        teacherId:  v.teacherId,
        roomId,
        groupIds:   v.groupIds,
        dayOfWeek:  this.data.day,
        pairNumber: this.data.pair,
        weekType:   v.weekType as WeekType,
        lessonType: v.lessonType as LessonType,
        isOnline:   v.isOnline
      };
      const result: AddEntryDialogResult = { mode: 'create', dto };
      this.dialogRef.close(result);
    }
  }
}
