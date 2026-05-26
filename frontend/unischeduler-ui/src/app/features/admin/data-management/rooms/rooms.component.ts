import { Component, OnInit, Inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../../core/services/api.service';
import { Room, Building, Department } from '../../../../core/models';
import { LessonType, RoomType } from '../../../../core/models/enums';
import { RoomTypePipe } from '../../../../shared/pipes/room-type.pipe';
import { LessonTypePipe } from '../../../../shared/pipes/lesson-type.pipe';
import { SearchSelectComponent } from '../../../../shared/components/search-select.component';

@Component({
  selector: 'app-rooms',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatCheckboxModule, MatSnackBarModule, MatTooltipModule, MatChipsModule,
    MatProgressSpinnerModule, RoomTypePipe, LessonTypePipe
  ],
  template: `
    <div class="page-header">
      <h1>Аудитории</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <mat-form-field appearance="outline" class="search-field" *ngIf="!loading">
        <mat-icon matPrefix>search</mat-icon>
        <mat-label>Поиск по номеру или корпусу</mat-label>
        <input matInput [(ngModel)]="search" placeholder="101, А-...">
        <button mat-icon-button matSuffix *ngIf="search" (click)="search = ''"><mat-icon>close</mat-icon></button>
      </mat-form-field>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>
      <div class="table-scroll" *ngIf="!loading">
      <table mat-table [dataSource]="filteredRooms" class="full-width">
        <ng-container matColumnDef="number">
          <th mat-header-cell *matHeaderCellDef>Аудитория</th>
          <td mat-cell *matCellDef="let r">
            {{ r.buildingShortCode ? r.buildingShortCode + '-' : '' }}{{ r.number }}
          </td>
        </ng-container>
        <ng-container matColumnDef="building">
          <th mat-header-cell *matHeaderCellDef>Корпус</th>
          <td mat-cell *matCellDef="let r">{{ r.buildingShortCode }}</td>
        </ng-container>
        <ng-container matColumnDef="location">
          <th mat-header-cell *matHeaderCellDef>Этаж</th>
          <td mat-cell *matCellDef="let r">
            <span *ngIf="!r.isOnline">{{ r.floor }} эт.</span>
            <span *ngIf="r.isOnline">—</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="type">
          <th mat-header-cell *matHeaderCellDef>Тип</th>
          <td mat-cell *matCellDef="let r">{{ r.roomType | roomType }}</td>
        </ng-container>
        <ng-container matColumnDef="capacity">
          <th mat-header-cell *matHeaderCellDef>Вместимость</th>
          <td mat-cell *matCellDef="let r">{{ r.capacity }}</td>
        </ng-container>
        <ng-container matColumnDef="features">
          <th mat-header-cell *matHeaderCellDef>Оснащение</th>
          <td mat-cell *matCellDef="let r">
            <mat-chip *ngIf="r.hasProjector" class="features-chip">Проектор</mat-chip>
            <mat-chip *ngIf="r.hasComputers" class="features-chip">ПК</mat-chip>
            <mat-chip *ngIf="r.hasLab" class="features-chip">Лаборатория</mat-chip>
            <mat-chip *ngIf="r.isOnline" class="features-chip">Онлайн</mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="allowedTypes">
          <th mat-header-cell *matHeaderCellDef>Разрешённые занятия</th>
          <td mat-cell *matCellDef="let r">
            <span *ngIf="!r.allowedLessonTypes?.length" class="all-types">Любые</span>
            <mat-chip *ngFor="let lt of r.allowedLessonTypes" class="type-chip">{{ lt | lessonType }}</mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="department">
          <th mat-header-cell *matHeaderCellDef>Кафедра</th>
          <td mat-cell *matCellDef="let r">
            <span *ngIf="r.departmentName" class="dept-name">{{ r.departmentName }}</span>
            <span *ngIf="!r.departmentName" class="no-dept">—</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="enabled">
          <th mat-header-cell *matHeaderCellDef>Статус</th>
          <td mat-cell *matCellDef="let r">
            <mat-chip [class]="r.isEnabled ? 'chip-enabled' : 'chip-disabled'">
              {{ r.isEnabled ? 'Активна' : 'Отключена' }}
            </mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="utilization">
          <th mat-header-cell *matHeaderCellDef>Загруженность</th>
          <td mat-cell *matCellDef="let r">
            <mat-chip [class]="'util-chip util-' + utilizationBand(r.utilizationPercent)"
                      [matTooltip]="utilizationTooltip(r.utilizationPercent)">
              {{ r.utilizationPercent || 0 }}%
            </mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let r">
            <button mat-icon-button (click)="openDialog(r)" matTooltip="Редактировать">
              <mat-icon>edit</mat-icon>
            </button>
            <button mat-icon-button color="warn" (click)="delete(r)" matTooltip="Удалить">
              <mat-icon>delete</mat-icon>
            </button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;" [class.row-disabled]="!row.isEnabled"></tr>
      </table>
      </div>
    </mat-card>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .table-scroll { overflow-x: auto; }
    .search-field { width: 100%; max-width: 420px; margin-top: 8px; margin-bottom: 8px; }
    .full-width { width: 100%; min-width: 920px; }
    mat-chip { font-size: 11px; margin: 1px; }
    .type-chip { background: #e3f2fd; color: #1565c0; font-size: 11px; margin: 1px; }
    .all-types { color: #9e9e9e; font-size: 12px; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
    .dept-name { font-size: 12px; color: #555; }
    .no-dept { color: #ccc; }
    .chip-enabled { background: #e8f5e9; color: #1b5e20; font-size: 11px; }
    .chip-disabled { background: #fce4ec; color: #880e4f; font-size: 11px; }
    .row-disabled td { opacity: 0.5; }

    /* Utilization chip — value-graded green→amber→red, paired light/dark */
    .util-chip { font-size: 11px; font-weight: 600; }
    .util-empty   { background: #f5f5f5; color: #9e9e9e; }
    .util-low     { background: #e8f5e9; color: #1b5e20; }
    .util-medium  { background: #fff3e0; color: #e65100; }
    .util-high    { background: #ffebee; color: #b71c1c; }
    :host-context(body.dark-mode) .util-empty  { background: #2a2a2a; color: #777; }
    :host-context(body.dark-mode) .util-low    { background: #1b3a1d; color: #a5d6a7; }
    :host-context(body.dark-mode) .util-medium { background: #4a2c10; color: #ffb74d; }
    :host-context(body.dark-mode) .util-high   { background: #4a1818; color: #ef9a9a; }
  `]
})
export class RoomsComponent implements OnInit {
  rooms: Room[] = [];
  buildings: Building[] = [];
  departments: Department[] = [];
  loading = true;
  search = '';
  columns = ['number', 'building', 'location', 'type', 'capacity', 'features', 'department', 'enabled', 'utilization', 'allowedTypes', 'actions'];

  get filteredRooms(): Room[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.rooms;
    return this.rooms.filter(r =>
      (r.number ?? '').toLowerCase().includes(q) ||
      (r.buildingShortCode ?? '').toLowerCase().includes(q) ||
      ((r.buildingShortCode ?? '') + '-' + (r.number ?? '')).toLowerCase().includes(q));
  }

  utilizationBand(pct: number | undefined): 'empty' | 'low' | 'medium' | 'high' {
    const p = pct ?? 0;
    if (p <= 0) return 'empty';
    if (p < 30) return 'low';
    if (p < 70) return 'medium';
    return 'high';
  }

  utilizationTooltip(pct: number | undefined): string {
    const p = pct ?? 0;
    if (p <= 0) return 'Нет занятий в опубликованных расписаниях';
    return `${p}% от 42 пар/нед (опубликованные расписания)`;
  }

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getBuildings().subscribe(b => { this.buildings = b; });
    this.api.getDepartments().subscribe(d => { this.departments = d; this.load(); });
  }

  load(): void {
    this.loading = true;
    this.api.getRooms().subscribe({
      next: data => { this.rooms = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  openDialog(room: Room | null): void {
    const ref = this.dialog.open(RoomDialogComponent, {
      data: { room, buildings: this.buildings, departments: this.departments }, width: '560px'
    });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (room) {
        this.api.updateRoom(room.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createRoom(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  delete(room: Room): void {
    if (!confirm(`Удалить аудиторию ${room.number}?`)) return;
    this.api.deleteRoom(room.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}

@Component({
  selector: 'app-room-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatButtonModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatCheckboxModule, MatDialogModule, MatIconModule, SearchSelectComponent
  ],
  template: `
    <h2 mat-dialog-title>{{ data.room ? 'Редактировать' : 'Добавить' }} аудиторию</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <app-search-select class="full-width" label="Корпус" formControlName="buildingId"
          [options]="data.buildings" [displayWith]="buildingLabel"></app-search-select>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Номер аудитории</mat-label>
          <input matInput formControlName="number" placeholder="101, А-203...">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Этаж</mat-label>
          <input matInput type="number" formControlName="floor">
          <mat-hint>Отрицательный — цокольный/подвал (-1, -2...)</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Тип</mat-label>
          <mat-select formControlName="roomType">
            <mat-option value="LectureHall">Лекционный зал</mat-option>
            <mat-option value="RegularCabinet">Кабинет</mat-option>
            <mat-option value="Lab">Лаборатория</mat-option>
            <mat-option value="ComputerLab">Компьютерный класс</mat-option>
            <mat-option value="Virtual">Дистанционно</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Вместимость</mat-label>
          <input matInput type="number" formControlName="capacity" min="1">
        </mat-form-field>
        <div class="checkboxes">
          <mat-checkbox formControlName="hasProjector">Проектор</mat-checkbox>
          <mat-checkbox formControlName="hasComputers">Компьютеры</mat-checkbox>
          <mat-checkbox formControlName="hasLab">Лабораторное оборудование</mat-checkbox>
          <mat-checkbox formControlName="isOnline">Онлайн</mat-checkbox>
        </div>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Разрешённые типы занятий</mat-label>
          <mat-select formControlName="allowedLessonTypes" multiple>
            <mat-option value="Lecture">Лекция</mat-option>
            <mat-option value="Practical">Практика</mat-option>
            <mat-option value="Lab">Лабораторная</mat-option>
            <mat-option value="Seminar">Семинар</mat-option>
          </mat-select>
          <mat-hint>Оставьте пустым — допускаются все типы</mat-hint>
        </mat-form-field>
        <app-search-select class="full-width" label="Кафедра (необязательно)" formControlName="departmentId"
          [options]="data.departments" [displayWith]="departmentLabel"
          [allowNull]="true" nullLabel="— Без кафедры —"></app-search-select>
        <div class="checkboxes">
          <mat-checkbox formControlName="isEnabled">Аудитория активна (доступна для расписания)</mat-checkbox>
        </div>
        <div class="warn-note" *ngIf="data.room">
          <mat-icon class="warn-icon">warning</mat-icon>
          Изменение допустимых типов занятий не затрагивает уже размещённые занятия.
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; margin-bottom: 8px; }
    .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 440px; }
    .checkboxes { display: flex; flex-direction: column; gap: 4px; margin-bottom: 8px; }
    .row-fields { display: flex; gap: 12px; margin-bottom: 8px; }
    .half-width { flex: 1; }
    .warn-note { display: flex; align-items: flex-start; gap: 6px; font-size: 12px; color: #e65100;
      background: #fff3e0; border-radius: 4px; padding: 6px 8px; margin-top: 4px; }
    .warn-icon { font-size: 16px; height: 16px; width: 16px; flex-shrink: 0; margin-top: 1px; }
  `]
})
export class RoomDialogComponent implements OnDestroy {
  form: FormGroup;
  private sub: Subscription;

  buildingLabel = (b: Building): string => `${b.shortCode} — ${b.address}`;
  departmentLabel = (d: Department): string => `${d.shortCode} — ${d.name}`;

  private static suggestForType(rt: RoomType): LessonType[] {
    switch (rt) {
      case RoomType.LectureHall:   return [LessonType.Lecture];
      case RoomType.Lab:           return [LessonType.Lab];
      case RoomType.ComputerLab:   return [LessonType.Lab, LessonType.Practical];
      case RoomType.RegularCabinet: return [LessonType.Lecture, LessonType.Practical, LessonType.Seminar];
      default:                     return [];
    }
  }

  constructor(
    private dialogRef: MatDialogRef<RoomDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { room: Room | null; buildings: Building[]; departments: Department[] },
    private fb: FormBuilder
  ) {
    const r = data.room;
    this.form = this.fb.group({
      buildingId: [r?.buildingId ?? '', Validators.required],
      number: [r?.number ?? '', Validators.required],
      floor: [r?.floor ?? 1, [Validators.required]],
      roomType: [r?.roomType ?? RoomType.RegularCabinet, Validators.required],
      capacity: [r?.capacity ?? 30, [Validators.required, Validators.min(1)]],
      hasProjector: [r?.hasProjector ?? false],
      hasComputers: [r?.hasComputers ?? false],
      hasLab: [r?.hasLab ?? false],
      isOnline: [r?.isOnline ?? false],
      allowedLessonTypes: [r?.allowedLessonTypes ?? []],
      isEnabled: [r?.isEnabled ?? true],
      departmentId: [r?.departmentId ?? null]
    });

    // Auto-suggest AllowedLessonTypes when room type changes (new rooms only)
    this.sub = this.form.get('roomType')!.valueChanges.subscribe((rt: RoomType) => {
      const current: LessonType[] = this.form.get('allowedLessonTypes')!.value ?? [];
      if (!r || current.length === 0) {
        this.form.get('allowedLessonTypes')!.setValue(RoomDialogComponent.suggestForType(rt), { emitEvent: false });
      }
    });

    // Populate suggestion on first open for new rooms
    if (!r) {
      this.form.get('allowedLessonTypes')!.setValue(
        RoomDialogComponent.suggestForType(RoomType.RegularCabinet), { emitEvent: false });
    }
  }

  ngOnDestroy(): void { this.sub.unsubscribe(); }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
