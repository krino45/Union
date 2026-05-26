import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { debounceTime } from 'rxjs/operators';
import { Subject, Teacher, StudentGroup, Room, ScheduleEntry, CreateScheduleEntryDto, UpdateScheduleEntryDto, CreateParallelEntriesDto } from '../../../core/models';
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
  | { mode: 'create-parallel'; dto: CreateParallelEntriesDto }
  | { mode: 'update'; entryId: string; dto: UpdateScheduleEntryDto }
  | { mode: 'split-edit'; entryId: string; dto: SplitEditBody };

@Component({
  selector: 'app-add-entry-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, MatRadioModule, MatChipsModule,
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
              <mat-option value="Language">Ин. язык</mat-option>
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

        <!-- Parallel sessions toggle — create mode only (language streams / lab subgroups) -->
        <mat-checkbox formControlName="parallelMode" class="parallel-check" *ngIf="!isEdit">
          Параллельные сессии (языковые потоки / подгруппы)
        </mat-checkbox>

        <!-- Single-teacher mode -->
        <mat-form-field appearance="outline" class="full" *ngIf="!parallelMode">
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

        <!-- Single-teacher room -->
        <mat-form-field appearance="outline" class="full" *ngIf="!parallelMode && !form.value.isOnline">
          <mat-label>Аудитория (необязательно)</mat-label>
          <mat-select formControlName="roomId">
            <mat-option [value]="null">Не указано</mat-option>
            <mat-option *ngFor="let r of data.rooms" [value]="r.id">
              {{ r.buildingShortCode ? r.buildingShortCode + '-' : '' }}{{ r.number }} (вм. {{ r.capacity }})
            </mat-option>
          </mat-select>
        </mat-form-field>

        <!-- Subgroup label — single-entry mode. Lets several subgroups share a slot (lab «Подгр. 1/2»). -->
        <mat-form-field appearance="outline" class="full" *ngIf="!parallelMode">
          <mat-label>Метка подгруппы (необязательно)</mat-label>
          <input matInput formControlName="subgroupLabel" placeholder="Напр. Подгр. 1">
          <mat-hint>Разные метки позволяют нескольким подгруппам стоять в одном слоте.</mat-hint>
        </mat-form-field>

        <!-- Parallel-sessions editor -->
        <div class="sessions" *ngIf="parallelMode" formArrayName="sessions">
          <div class="sessions-hdr">Сессии: каждая ведётся отдельным преподавателем одновременно</div>
          <div class="session-row" *ngFor="let s of sessions.controls; let i = index" [formGroupName]="i">
            <mat-form-field appearance="outline" class="s-teacher">
              <mat-label>Преподаватель</mat-label>
              <mat-select formControlName="teacherId">
                <mat-option *ngFor="let t of data.teachers" [value]="t.id">{{ t.displayName }}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="s-room" *ngIf="!form.value.isOnline">
              <mat-label>Ауд.</mat-label>
              <mat-select formControlName="roomId">
                <mat-option [value]="null">—</mat-option>
                <mat-option *ngFor="let r of data.rooms" [value]="r.id">
                  {{ r.buildingShortCode ? r.buildingShortCode + '-' : '' }}{{ r.number }}
                </mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="s-label">
              <mat-label>Метка</mat-label>
              <input matInput formControlName="label" placeholder="Поток 1">
            </mat-form-field>
            <button mat-icon-button type="button" color="warn" (click)="removeSession(i)" [disabled]="sessions.length <= 2">
              <mat-icon>remove_circle</mat-icon>
            </button>
          </div>
          <button mat-stroked-button type="button" (click)="addSession()"><mat-icon>add</mat-icon> Добавить сессию</button>
        </div>

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
      <button mat-raised-button color="primary" [disabled]="!canSubmit" (click)="submit()">
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
    .parallel-check { margin: 4px 0 8px; }
    .sessions { border: 1px solid #ba68c8; border-radius: 4px; padding: 10px 12px; margin: 4px 0 8px; background: #faf5fc; }
    .sessions-hdr { font-size: 12px; color: #6a1b9a; margin-bottom: 8px; }
    .session-row { display: flex; gap: 6px; align-items: center; }
    .s-teacher { flex: 2; }
    .s-room { flex: 1; }
    .s-label { flex: 1; }
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
      roomId:     [e?.roomId     ?? null],
      subgroupLabel: [e?.subgroupLabel ?? ''],
      parallelMode: [false],
      sessions:   this.fb.array([])
    });
  }

  get sessions(): FormArray {
    return this.form.get('sessions') as FormArray;
  }

  get parallelMode(): boolean {
    return !!this.form.get('parallelMode')?.value;
  }

  // The submit button can't rely on form.invalid because teacherId/roomId requirements differ
  // between single and parallel modes.
  get canSubmit(): boolean {
    const v = this.form.value;
    if (!v.subjectId || !v.lessonType || !v.weekType || !(v.groupIds?.length)) return false;
    if (this.parallelMode) {
      const rows = this.sessions.value as { teacherId: string }[];
      return rows.length >= 2 && rows.every(r => !!r.teacherId);
    }
    return !!v.teacherId;
  }

  ngOnInit(): void {
    // Language is inherently multi-stream — default to parallel mode when creating one.
    this.form.get('lessonType')!.valueChanges.subscribe(lt => {
      if (!this.isEdit && lt === 'Language' && !this.parallelMode) this.form.get('parallelMode')!.setValue(true);
    });
    // Seed two session rows the first time parallel mode is turned on.
    this.form.get('parallelMode')!.valueChanges.subscribe(on => {
      if (on && this.sessions.length === 0) { this.addSession(); this.addSession(); }
    });

    // Live-validate on form changes (debounced)
    this.form.valueChanges.pipe(debounceTime(400)).subscribe(() => this.validate());
    this.validate();
  }

  addSession(): void {
    this.sessions.push(this.fb.group({
      teacherId: ['', Validators.required],
      roomId: [null],
      label: ['']
    }));
  }

  removeSession(i: number): void {
    if (this.sessions.length > 2) this.sessions.removeAt(i);
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
      isOnline: v.isOnline,
      subgroupLabel: v.subgroupLabel?.trim() || null
    };
    this.api.validateScheduleEdit(this.data.scheduleId, body).subscribe({
      next: list => this.issues = list,
      error: () => this.issues = []
    });
  }

  submit(): void {
    if (!this.canSubmit) return;
    const v = this.form.value;
    const roomId = v.isOnline ? undefined : (v.roomId ?? undefined);

    // Parallel-sessions path (create only): one class, many simultaneous teacher/room sessions.
    if (!this.isEdit && this.parallelMode) {
      const dto: CreateParallelEntriesDto = {
        scheduleId: this.data.scheduleId,
        subjectId:  v.subjectId,
        lessonType: v.lessonType as LessonType,
        weekType:   v.weekType as WeekType,
        dayOfWeek:  this.data.day,
        pairNumber: this.data.pair,
        groupIds:   v.groupIds,
        isOnline:   v.isOnline,
        sessions:   (this.sessions.value as { teacherId: string; roomId: string | null; label: string }[])
          .map(s => ({ teacherId: s.teacherId, roomId: v.isOnline ? null : (s.roomId ?? null), label: s.label?.trim() || null }))
      };
      this.dialogRef.close({ mode: 'create-parallel', dto } as AddEntryDialogResult);
      return;
    }

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
        isOnline:   v.isOnline,
        subgroupLabel: v.subgroupLabel?.trim() || null
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
        isOnline:   v.isOnline,
        subgroupLabel: v.subgroupLabel?.trim() || null
      };
      const result: AddEntryDialogResult = { mode: 'create', dto };
      this.dialogRef.close(result);
    }
  }
}
