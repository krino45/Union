import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../../core/services/api.service';
import {
  StudyPlan, UpsertStudyPlanDto, UpsertStudyPlanEntryDto,
  CalendarPlan, Subject, StudentGroup, Faculty
} from '../../../../core/models';

@Component({
  selector: 'app-study-plans',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatTableModule, MatExpansionModule, MatSnackBarModule,
    MatDialogModule, MatChipsModule, MatCardModule, MatProgressSpinnerModule
  ],
  template: `
    <div class="page-header">
      <h2>Учебные планы</h2>
      <button mat-raised-button color="primary" (click)="startCreate()">
        <mat-icon>add</mat-icon> Новый план
      </button>
    </div>

    <!-- Edit / Create form -->
    <mat-card class="edit-card" *ngIf="editing">
      <mat-card-title>{{ form.get('id')?.value ? 'Редактировать план' : 'Новый учебный план' }}</mat-card-title>
      <mat-card-content>
        <form [formGroup]="form" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Название</mat-label>
            <input matInput formControlName="name">
          </mat-form-field>
          <div class="row-2">
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
            <mat-form-field appearance="outline">
              <mat-label>Факультет</mat-label>
              <mat-select formControlName="facultyId">
                <mat-option [value]="null">Любой</mat-option>
                <mat-option *ngFor="let f of faculties" [value]="f.id">{{ f.name }}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Календарный план</mat-label>
              <mat-select formControlName="calendarPlanId">
                <mat-option [value]="null">Не указан</mat-option>
                <mat-option *ngFor="let cp of calendarPlans" [value]="cp.id">{{ cp.name }}</mat-option>
              </mat-select>
            </mat-form-field>
          </div>
          <mat-form-field appearance="outline">
            <mat-label>Группы</mat-label>
            <mat-select formControlName="groupIds" multiple>
              <mat-option *ngFor="let g of groups" [value]="g.id">{{ g.name }}</mat-option>
            </mat-select>
          </mat-form-field>

          <!-- Discipline entries -->
          <div class="entries-section">
            <div class="entries-header">
              <strong>Дисциплины</strong>
              <button mat-stroked-button type="button" (click)="addEntry()">
                <mat-icon>add</mat-icon> Добавить
              </button>
            </div>
            <table class="entries-table" *ngIf="entriesArray.length > 0">
              <thead>
                <tr>
                  <th>Дисциплина</th>
                  <th>Лек. (ак.ч.)</th>
                  <th>Пр. (ак.ч.)</th>
                  <th>Лаб. (ак.ч.)</th>
                  <th>Сем. (ак.ч.)</th>
                  <th>ВКР (ак.ч.)</th>
                  <th></th>
                </tr>
              </thead>
              <tbody formArrayName="entries">
                <tr *ngFor="let eg of entriesArray.controls; let i = index" [formGroupName]="i">
                  <td>
                    <mat-select formControlName="subjectId" class="subj-select">
                      <mat-option *ngFor="let s of subjects" [value]="s.id">{{ s.shortName }} — {{ s.name }}</mat-option>
                    </mat-select>
                  </td>
                  <td><input type="number" min="0" formControlName="lectureHours" class="hrs-input"></td>
                  <td><input type="number" min="0" formControlName="practicalHours" class="hrs-input"></td>
                  <td><input type="number" min="0" formControlName="labHours" class="hrs-input"></td>
                  <td><input type="number" min="0" formControlName="seminarHours" class="hrs-input"></td>
                  <td><input type="number" min="0" formControlName="thesisHours" class="hrs-input"></td>
                  <td><button mat-icon-button type="button" (click)="removeEntry(i)" color="warn"><mat-icon>delete</mat-icon></button></td>
                </tr>
              </tbody>
            </table>
            <div *ngIf="entriesArray.length === 0" class="no-entries">Нет дисциплин — нажмите «Добавить»</div>
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

    <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>

    <ng-container *ngIf="!loading">
      <mat-expansion-panel *ngFor="let plan of plans" class="plan-panel">
        <mat-expansion-panel-header>
          <mat-panel-title>{{ plan.name }}</mat-panel-title>
          <mat-panel-description>
            {{ plan.academicYear }}/{{ plan.academicYear + 1 }} · {{ plan.term === 'First' ? '1-й' : '2-й' }} сем.
            · {{ plan.groups.length }} групп
            · {{ plan.entries.length }} дисциплин
            <span *ngIf="plan.calendarPlanName" class="cal-badge">📅 {{ plan.calendarPlanName }}</span>
          </mat-panel-description>
        </mat-expansion-panel-header>

        <div class="plan-detail">
          <div class="plan-groups">
            <strong>Группы:</strong>
            <span *ngFor="let g of plan.groups" class="group-chip">{{ g.groupName }}</span>
            <span *ngIf="plan.groups.length === 0" class="muted">Не назначены</span>
          </div>

          <table class="entries-view-table" *ngIf="plan.entries.length > 0">
            <thead><tr>
              <th>Дисциплина</th><th>Лек.</th><th>Пр.</th><th>Лаб.</th><th>Сем.</th><th>ВКР</th><th>Всего</th>
            </tr></thead>
            <tbody>
              <tr *ngFor="let e of plan.entries">
                <td>{{ e.subjectShortName }} <span class="subj-full">({{ e.subjectName }})</span></td>
                <td>{{ e.lectureHours || '—' }}</td>
                <td>{{ e.practicalHours || '—' }}</td>
                <td>{{ e.labHours || '—' }}</td>
                <td>{{ e.seminarHours || '—' }}</td>
                <td>{{ e.thesisHours || '—' }}</td>
                <td class="total">{{ e.lectureHours + e.practicalHours + e.labHours + e.seminarHours + e.thesisHours }}</td>
              </tr>
            </tbody>
          </table>

          <div class="plan-actions">
            <button mat-stroked-button (click)="startEdit(plan)"><mat-icon>edit</mat-icon> Редактировать</button>
            <button mat-stroked-button color="warn" (click)="deletePlan(plan)"><mat-icon>delete</mat-icon> Удалить</button>
          </div>
        </div>
      </mat-expansion-panel>

      <div *ngIf="plans.length === 0 && !editing" class="empty-state">
        Нет учебных планов. Создайте первый план.
      </div>
    </ng-container>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .edit-card { margin-bottom: 20px; }
    .form-grid { display: flex; flex-direction: column; gap: 8px; }
    .row-2 { display: flex; gap: 8px; flex-wrap: wrap; }
    .row-2 mat-form-field { flex: 1; min-width: 160px; }
    .entries-section { border: 1px solid #e0e0e0; border-radius: 4px; padding: 12px; }
    .entries-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    .entries-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .entries-table th { background: #f5f5f5; padding: 4px 8px; text-align: left; }
    .entries-table td { padding: 4px 8px; border-bottom: 1px solid #f0f0f0; }
    .subj-select { width: 220px; font-size: 12px; }
    .hrs-input { width: 60px; border: 1px solid #ccc; border-radius: 3px; padding: 3px 6px; }
    .no-entries { color: #999; font-size: 13px; padding: 8px 0; }
    .plan-panel { margin-bottom: 8px; }
    .plan-detail { padding: 8px 0; }
    .plan-groups { margin-bottom: 12px; display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .group-chip { background: #e3f2fd; border-radius: 12px; padding: 2px 10px; font-size: 12px; }
    .cal-badge { background: #fff3e0; border-radius: 12px; padding: 2px 8px; font-size: 12px; margin-left: 8px; }
    .entries-view-table { width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 12px; }
    .entries-view-table th { background: #f5f5f5; padding: 6px 8px; text-align: left; border-bottom: 2px solid #e0e0e0; }
    .entries-view-table td { padding: 4px 8px; border-bottom: 1px solid #f0f0f0; }
    .subj-full { color: #888; font-size: 11px; }
    .total { font-weight: 600; }
    .plan-actions { display: flex; gap: 8px; }
    .muted { color: #999; font-size: 13px; }
    .empty-state { text-align: center; padding: 48px; color: #999; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
  `]
})
export class StudyPlansComponent implements OnInit {
  plans: StudyPlan[] = [];
  faculties: Faculty[] = [];
  calendarPlans: CalendarPlan[] = [];
  subjects: Subject[] = [];
  groups: any[] = [];
  loading = true;
  editing = false;
  saving = false;
  form!: FormGroup;

  constructor(private api: ApiService, private fb: FormBuilder, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    forkJoin({
      plans: this.api.getStudyPlans(),
      faculties: this.api.getFaculties(),
      calendarPlans: this.api.getCalendarPlans(),
      subjects: this.api.getSubjects(),
      groups: this.api.getGroups()
    }).subscribe({
      next: ({ plans, faculties, calendarPlans, subjects, groups }) => {
        this.plans = plans;
        this.faculties = faculties;
        this.calendarPlans = calendarPlans;
        this.subjects = subjects;
        this.groups = groups;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  get entriesArray(): FormArray { return this.form.get('entries') as FormArray; }

  startCreate(): void {
    this.form = this.buildForm();
    this.editing = true;
  }

  startEdit(plan: StudyPlan): void {
    this.form = this.buildForm(plan);
    this.editing = true;
  }

  cancelEdit(): void { this.editing = false; }

  addEntry(): void {
    this.entriesArray.push(this.fb.group({
      subjectId: ['', Validators.required],
      lectureHours: [0], practicalHours: [0], labHours: [0], seminarHours: [0], thesisHours: [0]
    }));
  }

  removeEntry(i: number): void { this.entriesArray.removeAt(i); }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.value;
    const dto: UpsertStudyPlanDto = {
      name: v.name, academicYear: v.academicYear, term: v.term,
      facultyId: v.facultyId, calendarPlanId: v.calendarPlanId,
      groupIds: v.groupIds ?? [],
      entries: v.entries as UpsertStudyPlanEntryDto[]
    };
    const op = v.id
      ? this.api.updateStudyPlan(v.id, dto)
      : this.api.createStudyPlan(dto);
    op.subscribe({
      next: () => {
        this.snackBar.open('Сохранено', 'OK', { duration: 2000 });
        this.editing = false;
        this.saving = false;
        this.api.getStudyPlans().subscribe(p => this.plans = p);
      },
      error: () => { this.saving = false; this.snackBar.open('Ошибка сохранения', 'OK', { duration: 3000 }); }
    });
  }

  deletePlan(plan: StudyPlan): void {
    if (!confirm(`Удалить учебный план "${plan.name}"?`)) return;
    this.api.deleteStudyPlan(plan.id).subscribe({
      next: () => { this.plans = this.plans.filter(p => p.id !== plan.id); },
      error: () => this.snackBar.open('Ошибка удаления', 'OK', { duration: 3000 })
    });
  }

  private buildForm(plan?: StudyPlan): FormGroup {
    const fg = this.fb.group({
      id: [plan?.id ?? null],
      name: [plan?.name ?? '', Validators.required],
      academicYear: [plan?.academicYear ?? new Date().getFullYear(), Validators.required],
      term: [plan?.term ?? 'First', Validators.required],
      facultyId: [plan?.facultyId ?? null],
      calendarPlanId: [plan?.calendarPlanId ?? null],
      groupIds: [plan?.groups.map(g => g.studentGroupId) ?? []],
      entries: this.fb.array((plan?.entries ?? []).map(e => this.fb.group({
        subjectId: [e.subjectId, Validators.required],
        lectureHours: [e.lectureHours], practicalHours: [e.practicalHours],
        labHours: [e.labHours], seminarHours: [e.seminarHours], thesisHours: [e.thesisHours]
      })))
    });
    return fg;
  }
}
