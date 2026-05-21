import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { debounceTime } from 'rxjs/operators';
import { Subject, Teacher, StudentGroup, Room, ScheduleEntry, CreateScheduleEntryDto, UpdateScheduleEntryDto } from '../../../core/models';
import { RussianDayOfWeek, WeekType, LessonType } from '../../../core/models/enums';
import { ValidationIssue, SplitEditBody, ValidateEditBody } from '../../../core/models';
import { DayOfWeekPipe } from '../../../shared/pipes/day-of-week.pipe';
import { ApiService } from '../../../core/services/api.service';

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
  | { mode: 'update'; entryId: string; dto: UpdateScheduleEntryDto }
  | { mode: 'split-edit'; entryId: string; dto: SplitEditBody };

@Component({
  selector: 'app-add-entry-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatSelectModule, MatCheckboxModule, MatRadioModule, MatChipsModule,
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

        <!-- Both-week split selector — only shown when editing a Both-week lesson -->
        <div class="apply-to" *ngIf="canSplit">
          <div class="apply-lbl">Применить изменения к:</div>
          <mat-radio-group formControlName="applyTo">
            <mat-radio-button value="Both">обеим неделям</mat-radio-button>
            <mat-radio-button value="Odd">только нечётной</mat-radio-button>
            <mat-radio-button value="Even">только чётной</mat-radio-button>
          </mat-radio-group>
          <div class="apply-hint" *ngIf="form.value.applyTo !== 'Both'">
            <mat-icon>call_split</mat-icon>
            Занятие будет разделено на две записи. Другая половина останется без изменений.
          </div>
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

        <!-- Validation warnings — live from backend -->
        <div class="issues" *ngIf="issues.length > 0">
          <div *ngFor="let i of issues" class="issue" [class.error]="i.severity === 'error'" [class.warn]="i.severity === 'warning'" [class.info]="i.severity === 'info'">
            <mat-icon>{{ i.severity === 'error' ? 'error' : i.severity === 'warning' ? 'warning' : 'info' }}</mat-icon>
            <span>{{ i.message }}</span>
          </div>
        </div>
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
    .form { display: flex; flex-direction: column; gap: 4px; min-width: 420px; }
    .full { width: 100%; }
    .row { display: flex; gap: 8px; }
    .flex1 { flex: 1; }
    .online-check { margin: 4px 0 8px; }
    .blocked-warn { background: #fff3e0; border: 1px solid #ffb300; border-radius: 4px; padding: 8px 12px; font-size: 13px; color: #e65100; margin: 4px 0; }
    .apply-to { background: #f3e5f5; border: 1px solid #ba68c8; border-radius: 4px; padding: 8px 12px; margin: 4px 0 12px; }
    .apply-lbl { font-size: 12px; color: #555; margin-bottom: 6px; }
    mat-radio-button { margin-right: 12px; }
    .apply-hint { display: flex; align-items: center; gap: 4px; font-size: 12px; color: #6a1b9a; margin-top: 6px; }
    .apply-hint mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .issues { display: flex; flex-direction: column; gap: 6px; margin: 8px 0 4px; }
    .issue { display: flex; align-items: center; gap: 6px; font-size: 13px; padding: 6px 10px; border-radius: 4px; }
    .issue.error { background: #ffebee; color: #b71c1c; }
    .issue.warn  { background: #fff3e0; color: #e65100; }
    .issue.info  { background: #e3f2fd; color: #1565c0; }
    .issue mat-icon { font-size: 18px; width: 18px; height: 18px; }
  `]
})
export class AddEntryDialogComponent implements OnInit {
  form: FormGroup;
  isEdit: boolean;
  canSplit: boolean;
  issues: ValidationIssue[] = [];

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
    private api: ApiService,
    public dialogRef: MatDialogRef<AddEntryDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AddEntryDialogData
  ) {
    const e = data.existingEntry;
    this.isEdit = !!e;
    this.canSplit = !!e && e.weekType === 'Both';
    this.form = this.fb.group({
      subjectId:  [e?.subjectId  ?? '',        Validators.required],
      lessonType: [e?.lessonType ?? 'Lecture', Validators.required],
      weekType:   [e?.weekType   ?? data.weekType, Validators.required],
      applyTo:    [this.canSplit ? 'Both' : (e?.weekType ?? data.weekType)],
      teacherId:  [e?.teacherId  ?? '',        Validators.required],
      groupIds:   [e?.studentGroups?.map(g => g.id) ?? [], Validators.required],
      isOnline:   [e?.isOnline   ?? false],
      roomId:     [e?.roomId     ?? null]
    });
  }

  ngOnInit(): void {
    // Live-validate on form changes (debounced)
    this.form.valueChanges.pipe(debounceTime(400)).subscribe(() => this.validate());
    this.validate();
  }

  private validate(): void {
    if (this.form.invalid) { this.issues = []; return; }
    const v = this.form.value;
    const body: ValidateEditBody = {
      entryId: this.data.existingEntry?.id ?? null,
      subjectId: v.subjectId,
      teacherId: v.teacherId,
      roomId: v.isOnline ? null : (v.roomId ?? null),
      groupIds: v.groupIds,
      dayOfWeek: this.data.day,
      pairNumber: this.data.pair,
      weekType: v.weekType,
      lessonType: v.lessonType,
      isOnline: v.isOnline
    };
    this.api.validateScheduleEdit(this.data.scheduleId, body).subscribe({
      next: list => this.issues = list,
      error: () => this.issues = []
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    const roomId = v.isOnline ? undefined : (v.roomId ?? undefined);

    // Both-week split-edit path
    if (this.isEdit && this.canSplit && v.applyTo !== 'Both') {
      const dto: SplitEditBody = {
        targetWeek: v.applyTo as 'Odd' | 'Even',
        subjectId: v.subjectId,
        teacherId: v.teacherId,
        roomId: roomId ?? null,
        groupIds: v.groupIds,
        dayOfWeek: this.data.day as unknown as string,
        pairNumber: this.data.pair,
        lessonType: v.lessonType,
        isOnline: v.isOnline
      };
      const result: AddEntryDialogResult = { mode: 'split-edit', entryId: this.data.existingEntry!.id, dto };
      this.dialogRef.close(result);
      return;
    }

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
