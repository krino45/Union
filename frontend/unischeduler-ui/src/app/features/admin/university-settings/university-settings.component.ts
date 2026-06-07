import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { SolverWeights } from '../../../core/models/schedule.model';

interface PairRow { pairNumber: number; start: string; end: string; }

// Grouped solver-weight fields rendered as number inputs.
const WEIGHT_GROUPS: { title: string; fields: { key: keyof SolverWeights; label: string }[] }[] = [
  {
    title: 'Окна и дни',
    fields: [
      { key: 'studentWindow', label: 'Окно у студентов' },
      { key: 'teacherWindow', label: 'Окно у преподавателя' },
      { key: 'activeDay', label: 'Учебный день' },
      { key: 'saturdayPenalty', label: 'Суббота' },
    ],
  },
  {
    title: 'СанПиН',
    fields: [
      { key: 'sanPinOverload', label: 'Перегрузка дня' },
      { key: 'maxPePerDay', label: 'Макс. физ-ры в день' },
      { key: 'peNotLastPenalty', label: 'Физ-ра не последней парой' },
      { key: 'peConsecutiveReward', label: 'Бонус за сдвоенную физ-ру' },
    ],
  },
  {
    title: 'Подряд (одинаковые занятия)',
    fields: [
      { key: 'consecLecture', label: 'Лекции подряд' },
      { key: 'consecSeminar', label: 'Семинары подряд' },
      { key: 'consecPractical', label: 'Практики подряд' },
      { key: 'consecLab', label: 'Лабораторные подряд' },
      { key: 'consecRunScalar', label: 'Множитель серии' },
    ],
  },
  {
    title: 'Время дня',
    fields: [
      { key: 'earlyPair', label: 'Ранние пары' },
      { key: 'middlePair', label: 'Средние пары' },
      { key: 'latePair', label: 'Поздние пары' },
    ],
  },
  {
    title: 'Перемещения и прочее',
    fields: [
      { key: 'walkingPenaltyMax', label: 'Макс. штраф за переход' },
      { key: 'stairFloorMeters', label: 'Метров на этаж (лестница)' },
      { key: 'departmentMismatchPenalty', label: 'Чужая кафедра' },
      { key: 'languagePerTeacherCap', label: 'Студентов на поток (язык)' },
      { key: 'physicalEducationPerTeacherCap', label: 'Студентов на группу (физ-ра)' },
    ],
  },
];

@Component({
  selector: 'app-university-settings',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule, MatSnackBarModule,
  ],
  template: `
    <div class="settings-page">
      <div class="page-header">
        <mat-icon>settings</mat-icon>
        <h1>Настройки университета</h1>
      </div>

      <div *ngIf="loading" class="spinner-wrap"><mat-spinner diameter="36"></mat-spinner></div>

      <ng-container *ngIf="!loading">
        <mat-card class="block">
          <mat-card-header><mat-card-title>Профиль</mat-card-title></mat-card-header>
          <mat-card-content>
            <form [formGroup]="profileForm" class="profile-form">
              <mat-form-field appearance="outline">
                <mat-label>Название</mat-label>
                <input matInput formControlName="name">
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Краткое название</mat-label>
                <input matInput formControlName="shortName">
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Город</mat-label>
                <input matInput formControlName="city" placeholder="Москва">
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>URL логотипа</mat-label>
                <input matInput formControlName="logoUrl">
              </mat-form-field>
            </form>
          </mat-card-content>
        </mat-card>

        <mat-card class="block">
          <mat-card-header>
            <mat-card-title>Время пар</mat-card-title>
            <span class="spacer"></span>
            <button mat-stroked-button (click)="addPair()"><mat-icon>add</mat-icon> Пара</button>
          </mat-card-header>
          <mat-card-content>
            <div class="pair-row header-row">
              <span>№</span><span>Начало</span><span>Конец</span><span></span>
            </div>
            <div class="pair-row" *ngFor="let p of pairs; let i = index">
              <input class="num" type="number" min="1" [(ngModel)]="p.pairNumber">
              <input type="time" [(ngModel)]="p.start">
              <input type="time" [(ngModel)]="p.end">
              <button mat-icon-button color="warn" (click)="removePair(i)"><mat-icon>delete</mat-icon></button>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="block">
          <mat-card-header><mat-card-title>Параметры генерации</mat-card-title></mat-card-header>
          <mat-card-content>
            <form [formGroup]="weightsForm">
              <div class="weight-group" *ngFor="let g of weightGroups">
                <h3>{{ g.title }}</h3>
                <div class="weight-grid">
                  <mat-form-field appearance="outline" *ngFor="let f of g.fields">
                    <mat-label>{{ f.label }}</mat-label>
                    <input matInput type="number" [formControlName]="f.key">
                  </mat-form-field>
                </div>
              </div>
            </form>
          </mat-card-content>
        </mat-card>

        <div class="actions">
          <button mat-raised-button color="primary" (click)="save()" [disabled]="saving || profileForm.invalid">
            <mat-icon>save</mat-icon> Сохранить и применить
          </button>
          <span class="hint">Страница перезагрузится после сохранения.</span>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    .settings-page { max-width: 920px; margin: 0 auto; }
    .page-header { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; }
    .page-header h1 { margin: 0; font-size: 22px; }
    .block { margin-bottom: 20px; }
    mat-card-header { display: flex; align-items: center; }
    .spacer { flex: 1; }
    .spinner-wrap { display: flex; justify-content: center; padding: 48px; }
    .profile-form { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 16px; padding-top: 8px; }
    .pair-row { display: grid; grid-template-columns: 80px 1fr 1fr 48px; gap: 8px; align-items: center; margin-bottom: 6px; }
    .pair-row.header-row { color: #888; font-size: 12px; font-weight: 600; }
    .pair-row input { padding: 6px 8px; border: 1px solid rgba(0,0,0,0.2); border-radius: 4px; font: inherit; }
    .pair-row input.num { width: 70px; }
    .weight-group h3 { font-size: 13px; color: #666; margin: 12px 0 4px; }
    .weight-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 4px 12px; }
    .weight-grid mat-form-field { width: 100%; }
    .actions { display: flex; align-items: center; gap: 12px; margin: 8px 0 40px; }
    .actions .hint { color: #888; font-size: 12px; }
    @media (max-width: 768px) { .profile-form { grid-template-columns: 1fr; } }
  `]
})
export class UniversitySettingsComponent implements OnInit {
  loading = true;
  saving = false;
  weightGroups = WEIGHT_GROUPS;
  profileForm: FormGroup;
  weightsForm: FormGroup;
  pairs: PairRow[] = [];

  constructor(private fb: FormBuilder, private api: ApiService, private snack: MatSnackBar) {
    this.profileForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(300)]],
      shortName: ['', [Validators.required, Validators.maxLength(50)]],
      city: ['', [Validators.maxLength(200)]],
      logoUrl: [''],
    });
    const wControls: Record<string, unknown> = {};
    for (const g of WEIGHT_GROUPS)
      for (const f of g.fields) wControls[f.key] = [0, [Validators.required]];
    this.weightsForm = this.fb.group(wControls);
  }

  ngOnInit(): void {
    forkJoin({
      uni: this.api.getCurrentUniversity(),
      pairs: this.api.getPairTimes(),
      weights: this.api.getSolverSettings(),
    }).subscribe({
      next: ({ uni, pairs, weights }) => {
        this.profileForm.patchValue({
          name: uni.name, shortName: uni.shortName, city: uni.city ?? '', logoUrl: uni.logoUrl ?? '',
        });
        this.pairs = (pairs ?? [])
          .sort((a, b) => a.pairNumber - b.pairNumber)
          .map(p => ({ pairNumber: p.pairNumber, start: p.startTime.slice(0, 5), end: p.endTime.slice(0, 5) }));
        this.weightsForm.patchValue(weights as any);
        this.loading = false;
      },
      error: () => {
        this.snack.open('Не удалось загрузить настройки', 'OK', { duration: 4000 });
        this.loading = false;
      },
    });
  }

  addPair(): void {
    const next = this.pairs.length ? Math.max(...this.pairs.map(p => p.pairNumber)) + 1 : 1;
    this.pairs.push({ pairNumber: next, start: '08:00', end: '09:35' });
  }

  removePair(i: number): void {
    this.pairs.splice(i, 1);
  }

  save(): void {
    if (this.saving || this.profileForm.invalid) return;
    this.saving = true;
    const pv = this.profileForm.value;
    const profile = {
      name: pv.name, shortName: pv.shortName,
      city: pv.city?.trim() || null, logoUrl: pv.logoUrl?.trim() || null,
    };
    const pairs = this.pairs.map(p => ({ pairNumber: p.pairNumber, startTime: p.start, endTime: p.end }));
    const weights = this.weightsForm.value as SolverWeights;

    forkJoin([
      this.api.updateCurrentUniversity(profile),
      this.api.updatePairTimes(pairs),
      this.api.updateSolverSettings(weights),
    ]).subscribe({
      next: () => window.location.reload(),
      error: e => {
        this.saving = false;
        this.snack.open(e?.error?.title || e?.error?.errors?.Pairs?.[0] || 'Ошибка сохранения', 'OK', { duration: 5000 });
      },
    });
  }
}
