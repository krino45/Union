import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { ApiService } from '../../../../core/services/api.service';
import { CalendarPlan, CalendarWeek, UpsertCalendarPlanDto, WeekKind } from '../../../../core/models';

const WEEK_KINDS: { value: WeekKind; label: string; color: string }[] = [
  { value: 'Study',           label: 'Учебная',            color: '#e3f2fd' },
  { value: 'Holiday',         label: 'Каникулы',           color: '#f3e5f5' },
  { value: 'ExamPreparation', label: 'Подготовка к экз.',  color: '#fff8e1' },
  { value: 'Exams',           label: 'Экзамены',           color: '#ffebee' },
  { value: 'Practice',        label: 'Практика',           color: '#e8f5e9' },
  { value: 'Thesis',          label: 'ВКР',                color: '#fce4ec' },
  { value: 'Other',           label: 'Другое',             color: '#f5f5f5' },
];

@Component({
  selector: 'app-calendar-plans',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatExpansionModule, MatSnackBarModule, MatCardModule, MatDialogModule
  ],
  template: `
    <div class="page-header">
      <h2>Календарные учебные графики</h2>
      <button mat-raised-button color="primary" (click)="startCreate()">
        <mat-icon>add</mat-icon> Новый график
      </button>
    </div>

    <!-- Edit / Create form -->
    <mat-card class="edit-card" *ngIf="editing">
      <mat-card-title>{{ form.get('id')?.value ? 'Редактировать график' : 'Новый график' }}</mat-card-title>
      <mat-card-content>
        <form [formGroup]="form">
          <div class="row-3">
            <mat-form-field appearance="outline">
              <mat-label>Название</mat-label>
              <input matInput formControlName="name">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Учебный год</mat-label>
              <input matInput type="number" formControlName="academicYear">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Семестр</mat-label>
              <mat-select formControlName="term">
                <mat-option value="First">1-й</mat-option>
                <mat-option value="Second">2-й</mat-option>
              </mat-select>
            </mat-form-field>
          </div>

          <!-- Weeks -->
          <div class="weeks-section">
            <div class="weeks-header">
              <strong>Недели <span class="study-count">(учебных: {{ studyWeeksCount }})</span></strong>
              <button mat-stroked-button type="button" (click)="addWeek()"><mat-icon>add</mat-icon> Добавить</button>
              <button mat-stroked-button type="button" (click)="showGenerate = !showGenerate">
                <mat-icon>auto_fix_high</mat-icon> Сгенерировать
              </button>
            </div>

            <!-- Generate options panel -->
            <div class="generate-panel" *ngIf="showGenerate">
              <div class="gen-row">
                <mat-form-field appearance="outline" class="gen-field">
                  <mat-label>Дата начала</mat-label>
                  <input matInput type="date" [(ngModel)]="genStartDate" [ngModelOptions]="{standalone: true}">
                </mat-form-field>
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Уч. до каникул</mat-label>
                  <input matInput type="number" [(ngModel)]="genStudyBefore" [ngModelOptions]="{standalone: true}" min="1" max="20">
                </mat-form-field>
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Уч. после каникул</mat-label>
                  <input matInput type="number" [(ngModel)]="genStudyAfter" [ngModelOptions]="{standalone: true}" min="0" max="20">
                </mat-form-field>
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Нед. каникул</mat-label>
                  <input matInput type="number" [(ngModel)]="genHolidayWeeks" [ngModelOptions]="{standalone: true}" min="0" max="4">
                </mat-form-field>
              </div>
              <div class="gen-row">
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Нед. подготовки</mat-label>
                  <input matInput type="number" [(ngModel)]="genPrepWeeks" [ngModelOptions]="{standalone: true}" min="0" max="4">
                </mat-form-field>
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Нед. экзаменов</mat-label>
                  <input matInput type="number" [(ngModel)]="genExamWeeks" [ngModelOptions]="{standalone: true}" min="0" max="6">
                </mat-form-field>
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Нед. ВКР</mat-label>
                  <input matInput type="number" [(ngModel)]="genThesisWeeks" [ngModelOptions]="{standalone: true}" min="0" max="10">
                </mat-form-field>
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Нед. практики</mat-label>
                  <input matInput type="number" [(ngModel)]="genPracticeWeeks" [ngModelOptions]="{standalone: true}" min="0" max="8">
                </mat-form-field>
              </div>
              <div class="gen-row gen-row-actions">
                <mat-form-field appearance="outline" class="gen-field-sm">
                  <mat-label>Каникулы после</mat-label>
                  <input matInput type="number" [(ngModel)]="genEndHolidayWeeks" [ngModelOptions]="{standalone: true}" min="0" max="6">
                </mat-form-field>
                <button mat-raised-button color="primary" type="button" (click)="generateWeeks()">Сгенерировать</button>
                <button mat-button type="button" (click)="showGenerate = false">Отмена</button>
              </div>
            </div>

            <div class="weeks-legend">
              <span *ngFor="let k of weekKinds" class="legend-item" [style.background]="k.color">{{ k.label }}</span>
            </div>

            <div class="weeks-grid" formArrayName="weeks">
              <div *ngFor="let wg of weeksArray.controls; let i = index" [formGroupName]="i"
                   class="week-row" [style.background]="getBg(wg.get('kind')?.value)">
                <span class="wk-num">{{ i + 1 }}</span>
                <input type="date" formControlName="startDate" class="date-input">
                <select formControlName="kind" class="kind-select">
                  <option *ngFor="let k of weekKinds" [value]="k.value">{{ k.label }}</option>
                </select>
                <input type="text" formControlName="note" placeholder="Примечание" class="note-input">
                <button type="button" class="del-btn" (click)="removeWeek(i)" title="Удалить неделю">×</button>
              </div>
            </div>
          </div>
        </form>
      </mat-card-content>
      <mat-card-actions>
        <button mat-button (click)="cancelEdit()">Отмена</button>
        <button mat-raised-button color="primary" [disabled]="form.invalid || saving" (click)="save()">
          {{ saving ? 'Сохранение...' : 'Сохранить' }}
        </button>
      </mat-card-actions>
    </mat-card>

    <!-- Plans list -->
    <mat-expansion-panel *ngFor="let cp of plans" class="plan-panel">
      <mat-expansion-panel-header>
        <mat-panel-title>{{ cp.name }}</mat-panel-title>
        <mat-panel-description>
          {{ cp.academicYear }}/{{ cp.academicYear + 1 }} · {{ cp.term === 'First' ? '1-й' : '2-й' }} сем.
          · {{ studyWeeksFor(cp) }} учебных нед. / {{ cp.weeks.length }} всего
        </mat-panel-description>
      </mat-expansion-panel-header>
      <div class="cp-detail">
        <div class="weeks-row">
          <div *ngFor="let w of cp.weeks; let i = index" class="wk-chip"
               [style.background]="getBg(w.kind)" [title]="kindLabel(w.kind) + (w.note ? ': ' + w.note : '')">
            {{ i + 1 }}
          </div>
        </div>
        <div class="cp-legend">
          <span *ngFor="let k of weekKinds" class="legend-item" [style.background]="k.color">
            {{ k.label }}: {{ countKind(cp.weeks, k.value) }}
          </span>
        </div>
        <div class="plan-actions">
          <button mat-stroked-button (click)="startEdit(cp)"><mat-icon>edit</mat-icon> Редактировать</button>
          <button mat-stroked-button color="warn" (click)="deletePlan(cp)"><mat-icon>delete</mat-icon> Удалить</button>
        </div>
      </div>
    </mat-expansion-panel>

    <div *ngIf="plans.length === 0 && !editing" class="empty-state">
      Нет календарных графиков. Создайте первый.
    </div>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .edit-card { margin-bottom: 20px; }
    .row-3 { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 8px; }
    .row-3 mat-form-field { flex: 1; min-width: 160px; }
    .weeks-section { border: 1px solid #e0e0e0; border-radius: 4px; padding: 12px; }
    .weeks-header { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
    .study-count { color: #1976d2; font-weight: normal; font-size: 13px; }
    .generate-panel { background: #f9f9f9; border: 1px solid #e0e0e0; border-radius: 4px; padding: 10px 12px; margin-bottom: 10px; }
    .gen-row { display: flex; gap: 8px; align-items: flex-end; flex-wrap: wrap; margin-bottom: 4px; }
    .gen-row-actions { margin-top: 4px; }
    .gen-field { flex: 1; min-width: 160px; }
    .gen-field-sm { flex: 1; min-width: 110px; }
    .weeks-legend { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 8px; }
    .legend-item { padding: 2px 8px; border-radius: 10px; font-size: 11px; border: 1px solid #ddd; }
    .weeks-grid { max-height: 400px; overflow-y: auto; display: flex; flex-direction: column; gap: 3px; }
    .week-row { display: flex; align-items: center; gap: 6px; padding: 4px 6px; border-radius: 4px; }
    .wk-num { width: 24px; text-align: right; font-size: 11px; color: #666; flex-shrink: 0; }
    .date-input { width: 130px; border: 1px solid #ccc; border-radius: 3px; padding: 3px 6px; font-size: 12px; }
    .kind-select { flex: 1; min-width: 150px; border: 1px solid #ccc; border-radius: 3px; padding: 3px 6px; font-size: 12px; }
    .note-input { flex: 2; border: 1px solid #ccc; border-radius: 3px; padding: 3px 6px; font-size: 12px; }
    .del-btn { border: none; background: transparent; cursor: pointer; color: #999; font-size: 16px; padding: 0 4px; }
    .del-btn:hover { color: #c62828; }
    .plan-panel { margin-bottom: 8px; }
    .cp-detail { padding: 8px 0; }
    .weeks-row { display: flex; flex-wrap: wrap; gap: 3px; margin-bottom: 8px; }
    .wk-chip { width: 28px; height: 28px; display: flex; align-items: center; justify-content: center;
               border-radius: 4px; font-size: 11px; font-weight: 600; border: 1px solid rgba(0,0,0,0.1); cursor: default; }
    .cp-legend { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 10px; }
    .plan-actions { display: flex; gap: 8px; }
    .empty-state { text-align: center; padding: 48px; color: #999; }
  `]
})
export class CalendarPlansComponent implements OnInit {
  plans: CalendarPlan[] = [];
  editing = false;
  saving = false;
  form!: FormGroup;
  readonly weekKinds = WEEK_KINDS;

  // generate panel state
  showGenerate = false;
  genStartDate = '';
  genStudyBefore = 8;
  genStudyAfter = 10;
  genHolidayWeeks = 1;
  genPrepWeeks = 1;
  genExamWeeks = 3;
  genThesisWeeks = 0;
  genPracticeWeeks = 0;
  genEndHolidayWeeks = 2;

  constructor(private api: ApiService, private fb: FormBuilder, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getCalendarPlans().subscribe(p => this.plans = p);
  }

  get weeksArray(): FormArray { return this.form.get('weeks') as FormArray; }

  get studyWeeksCount(): number {
    return this.weeksArray.controls.filter(c => c.get('kind')?.value === 'Study').length;
  }

  getBg(kind: WeekKind): string {
    return WEEK_KINDS.find(k => k.value === kind)?.color ?? '#f5f5f5';
  }

  kindLabel(kind: WeekKind): string {
    return WEEK_KINDS.find(k => k.value === kind)?.label ?? kind;
  }

  studyWeeksFor(cp: CalendarPlan): number {
    return cp.weeks.filter(w => w.kind === 'Study').length;
  }

  countKind(weeks: CalendarWeek[], kind: WeekKind): number {
    return weeks.filter(w => w.kind === kind).length;
  }

  startCreate(): void {
    this.showGenerate = false;
    this.form = this.buildForm();
    this.editing = true;
  }

  startEdit(cp: CalendarPlan): void {
    this.showGenerate = false;
    this.form = this.buildForm(cp);
    this.editing = true;
  }

  cancelEdit(): void { this.editing = false; this.showGenerate = false; }

  addWeek(): void {
    const arr = this.weeksArray;
    let nextDate = '';
    if (arr.length > 0) {
      const lastDate = arr.at(arr.length - 1).get('startDate')?.value;
      if (lastDate) {
        const d = new Date(lastDate);
        d.setDate(d.getDate() + 7);
        nextDate = d.toISOString().slice(0, 10);
      }
    }
    arr.push(this.fb.group({ startDate: [nextDate], kind: ['Study'], note: [''] }));
  }

  removeWeek(i: number): void {
    const arr = this.weeksArray;
    const removedDate = arr.at(i).get('startDate')?.value as string;
    arr.removeAt(i);
    // Cascade: shift all subsequent dates back by 7 days
    if (removedDate) {
      for (let j = i; j < arr.length; j++) {
        const ctrl = arr.at(j).get('startDate');
        const val = ctrl?.value as string;
        if (val) {
          const d = new Date(val);
          d.setDate(d.getDate() - 7);
          ctrl!.setValue(d.toISOString().slice(0, 10));
        }
      }
    }
  }

  generateWeeks(): void {
    // Determine start date
    let start: Date;
    if (this.genStartDate) {
      start = new Date(this.genStartDate);
    } else {
      const year: number = this.form.get('academicYear')?.value ?? new Date().getFullYear();
      const term: string = this.form.get('term')?.value ?? 'First';
      const startMonth = term === 'First' ? 8 : 1;
      start = new Date(term === 'First' ? year : year + 1, startMonth, term === 'First' ? 1 : 10);
    }
    // Move to Monday
    const dow = start.getDay();
    start.setDate(start.getDate() + (dow === 0 ? 1 : dow === 1 ? 0 : 8 - dow));

    // Build pattern from configurable params
    const pattern: WeekKind[] = [
      ...Array(this.genStudyBefore).fill('Study' as WeekKind),
      ...Array(this.genHolidayWeeks).fill('Holiday' as WeekKind),
      ...Array(this.genStudyAfter).fill('Study' as WeekKind),
      ...Array(this.genPrepWeeks).fill('ExamPreparation' as WeekKind),
      ...Array(this.genExamWeeks).fill('Exams' as WeekKind),
      ...Array(this.genPracticeWeeks).fill('Practice' as WeekKind),
      ...Array(this.genThesisWeeks).fill('Thesis' as WeekKind),
      ...Array(this.genEndHolidayWeeks).fill('Holiday' as WeekKind),
    ];

    this.weeksArray.clear();
    let cur = new Date(start);
    for (const kind of pattern) {
      this.weeksArray.push(this.fb.group({
        startDate: [cur.toISOString().slice(0, 10)],
        kind: [kind],
        note: ['']
      }));
      cur.setDate(cur.getDate() + 7);
    }
    this.showGenerate = false;
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.value;
    const dto: UpsertCalendarPlanDto = {
      name: v.name, academicYear: v.academicYear, term: v.term,
      weeks: v.weeks.map((w: any) => ({ startDate: w.startDate, kind: w.kind, note: w.note || null }))
    };
    const op = v.id ? this.api.updateCalendarPlan(v.id, dto) : this.api.createCalendarPlan(dto);
    op.subscribe({
      next: () => {
        this.snackBar.open('Сохранено', 'OK', { duration: 2000 });
        this.editing = false; this.saving = false;
        this.api.getCalendarPlans().subscribe(p => this.plans = p);
      },
      error: () => { this.saving = false; this.snackBar.open('Ошибка', 'OK', { duration: 3000 }); }
    });
  }

  deletePlan(cp: CalendarPlan): void {
    if (!confirm(`Удалить график "${cp.name}"?`)) return;
    this.api.deleteCalendarPlan(cp.id).subscribe({
      next: () => { this.plans = this.plans.filter(p => p.id !== cp.id); },
      error: () => this.snackBar.open('Ошибка', 'OK', { duration: 3000 })
    });
  }

  private buildForm(cp?: CalendarPlan): FormGroup {
    return this.fb.group({
      id: [cp?.id ?? null],
      name: [cp?.name ?? '', Validators.required],
      academicYear: [cp?.academicYear ?? new Date().getFullYear(), Validators.required],
      term: [cp?.term ?? 'First', Validators.required],
      weeks: this.fb.array((cp?.weeks ?? []).map(w => this.fb.group({
        startDate: [w.startDate], kind: [w.kind], note: [w.note ?? '']
      })))
    });
  }
}
