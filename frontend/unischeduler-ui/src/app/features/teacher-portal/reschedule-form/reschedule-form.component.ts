import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { ScheduleEntry, Schedule, RescheduleRequest, Room } from '../../../core/models';
import { RussianDayOfWeek, WeekType, RescheduleStatus } from '../../../core/models/enums';
import { DayOfWeekPipe } from '../../../shared/pipes/day-of-week.pipe';
import { WeekTypePipe } from '../../../shared/pipes/week-type.pipe';

@Component({
  selector: 'app-reschedule-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatCardModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatTableModule, MatChipsModule, MatSnackBarModule, MatCheckboxModule,
    DayOfWeekPipe, WeekTypePipe
  ],
  template: `
    <h1>Запрос на перенос занятия</h1>

    <div class="layout">
      <!-- Request Form -->
      <mat-card class="form-card">
        <mat-card-header>
          <mat-card-title>Новый запрос</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()" class="form">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Расписание</mat-label>
              <mat-select formControlName="scheduleId" (selectionChange)="onScheduleChange()">
                <mat-option *ngFor="let s of schedules" [value]="s.id">{{ s.academicYear }}/{{ s.academicYear + 1 }} — {{ s.term === 'First' ? '1-й' : '2-й' }} сем.</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Занятие</mat-label>
              <mat-select formControlName="originalEntryId">
                <mat-option *ngFor="let e of myEntries" [value]="e.id">
                  {{ e.subjectName }} — {{ e.dayOfWeek | dayOfWeek: 'short' }}, пара {{ e.pairNumber }}
                </mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Причина</mat-label>
              <textarea matInput formControlName="reason" rows="3"></textarea>
            </mat-form-field>

            <h4>Предлагаемое время (необязательно)</h4>
            <div class="row">
              <mat-form-field appearance="outline" class="flex1">
                <mat-label>День</mat-label>
                <mat-select formControlName="proposedDayOfWeek" (selectionChange)="onSlotChange()">
                  <mat-option [value]="null">Любой</mat-option>
                  <mat-option value="Monday">Понедельник</mat-option>
                  <mat-option value="Tuesday">Вторник</mat-option>
                  <mat-option value="Wednesday">Среда</mat-option>
                  <mat-option value="Thursday">Четверг</mat-option>
                  <mat-option value="Friday">Пятница</mat-option>
                  <mat-option value="Saturday">Суббота</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" class="flex1">
                <mat-label>Пара</mat-label>
                <mat-select formControlName="proposedPairNumber" (selectionChange)="onSlotChange()">
                  <mat-option [value]="null">Любая</mat-option>
                  <mat-option *ngFor="let p of [1,2,3,4,5,6,7]" [value]="p">{{ p }}</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" class="flex1">
                <mat-label>Тип недели</mat-label>
                <mat-select formControlName="proposedWeekType" (selectionChange)="onSlotChange()">
                  <mat-option [value]="null">Любой</mat-option>
                  <mat-option value="Both">Обе</mat-option>
                  <mat-option value="Odd">Нечётная</mat-option>
                  <mat-option value="Even">Чётная</mat-option>
                </mat-select>
              </mat-form-field>
            </div>

            <mat-checkbox formControlName="proposedIsOnline" (change)="onOnlineChange()">
              Перевести в онлайн (без аудитории)
            </mat-checkbox>

            <mat-form-field appearance="outline" class="full-width" *ngIf="!form.value.proposedIsOnline && slotChosen">
              <mat-label>Аудитория (свободные на этот слот)</mat-label>
              <mat-select formControlName="proposedRoomId">
                <mat-option [value]="null">Не выбрана (решит администратор)</mat-option>
                <mat-option *ngFor="let r of availableRooms" [value]="r.id">
                  {{ r.buildingShortCode }}-{{ r.number }}
                </mat-option>
              </mat-select>
              <mat-hint *ngIf="loadingRooms">Загрузка...</mat-hint>
              <mat-hint *ngIf="!loadingRooms && availableRooms.length === 0">Нет свободных аудиторий</mat-hint>
            </mat-form-field>

            <button mat-raised-button color="primary" type="submit" [disabled]="form.invalid">
              <mat-icon>send</mat-icon> Отправить запрос
            </button>
          </form>
        </mat-card-content>
      </mat-card>

      <!-- My Requests -->
      <mat-card class="requests-card">
        <mat-card-header>
          <mat-card-title>Мои запросы</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <div *ngFor="let r of myRequests" class="request-item">
            <div class="request-header">
              <strong>{{ r.originalEntryDescription || 'Занятие' }}</strong>
              <mat-chip [class]="statusClass(r.status)">{{ statusLabel(r.status) }}</mat-chip>
            </div>
            <div class="request-detail">{{ r.reason }}</div>
            <div class="request-detail" *ngIf="r.adminNote">
              <mat-icon style="font-size:14px">comment</mat-icon> {{ r.adminNote }}
            </div>
            <div class="request-date">{{ r.createdAt | date:'dd.MM.yyyy' }}</div>
          </div>
          <div *ngIf="myRequests.length === 0" class="empty">Нет запросов</div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    h1 { margin-bottom: 24px; }
    .layout { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
    .form { display: flex; flex-direction: column; gap: 12px; padding-top: 8px; }
    .full-width { width: 100%; }
    .row { display: flex; gap: 8px; }
    .flex1 { flex: 1; }
    h4 { margin: 0; color: #666; font-size: 13px; }
    .request-item {
      border-bottom: 1px solid #e0e0e0; padding: 12px 0;
    }
    .request-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
    .request-detail { font-size: 13px; color: #555; display: flex; align-items: center; gap: 4px; }
    .request-date { font-size: 11px; color: #999; margin-top: 4px; }
    .empty { color: #888; text-align: center; padding: 24px; }
    .status-pending { background: #fff3e0; color: #e65100; }
    .status-approved { background: #e8f5e9; color: #1b5e20; }
    .status-rejected { background: #ffebee; color: #b71c1c; }
    @media (max-width: 768px) { .layout { grid-template-columns: 1fr; } }
  `]
})
export class RescheduleFormComponent implements OnInit {
  form: FormGroup;
  schedules: Schedule[] = [];
  myEntries: ScheduleEntry[] = [];
  myRequests: RescheduleRequest[] = [];
  availableRooms: Room[] = [];
  loadingRooms = false;

  // A room can only be suggested once a concrete day + pair are picked, since availability is slot-specific.
  get slotChosen(): boolean {
    return !!this.form.value.proposedDayOfWeek && this.form.value.proposedPairNumber != null;
  }

  constructor(
    private fb: FormBuilder,
    private api: ApiService,
    private auth: AuthService,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      scheduleId: ['', Validators.required],
      originalEntryId: ['', Validators.required],
      reason: ['', Validators.required],
      proposedDayOfWeek: [null],
      proposedPairNumber: [null],
      proposedWeekType: [null],
      proposedRoomId: [null],
      proposedIsOnline: [false]
    });
  }

  onSlotChange(): void {
    this.form.patchValue({ proposedRoomId: null });
    this.availableRooms = [];
    if (this.form.value.proposedIsOnline || !this.slotChosen) return;
    this.loadingRooms = true;
    this.api.getAvailableRooms({
      scheduleId: this.form.value.scheduleId,
      dayOfWeek: this.form.value.proposedDayOfWeek,
      pairNumber: this.form.value.proposedPairNumber,
      weekType: this.form.value.proposedWeekType ?? WeekType.Both
    }).subscribe({
      next: rooms => { this.availableRooms = rooms; this.loadingRooms = false; },
      error: () => { this.availableRooms = []; this.loadingRooms = false; }
    });
  }

  onOnlineChange(): void {
    if (this.form.value.proposedIsOnline) {
      this.form.patchValue({ proposedRoomId: null });
    } else {
      this.onSlotChange();
    }
  }

  ngOnInit(): void {
    this.api.getSchedules().subscribe(s => {
      this.schedules = s;
      if (s.length > 0) {
        this.form.patchValue({ scheduleId: s[0].id });
        this.onScheduleChange();
      }
    });
    this.api.getRescheduleRequests().subscribe(r => this.myRequests = r);
  }

  onScheduleChange(): void {
    const scheduleId = this.form.value.scheduleId;
    const teacherId = this.auth.currentUser?.teacherId;
    if (!scheduleId || !teacherId) return;
    this.api.getScheduleEntries(scheduleId, { teacherId }).subscribe(e => this.myEntries = e);
  }

  submit(): void {
    if (this.form.invalid) return;
    const teacherId = this.auth.currentUser?.teacherId;
    if (!teacherId) {
      this.snackBar.open('Профиль преподавателя не найден', 'OK', { duration: 4000 });
      return;
    }
    const v = this.form.value;
    const dto = {
      teacherId,
      originalEntryId: v.originalEntryId,
      reason: v.reason,
      proposedIsOnline: !!v.proposedIsOnline,
      ...(v.proposedDayOfWeek ? { proposedDayOfWeek: v.proposedDayOfWeek } : {}),
      ...(v.proposedPairNumber != null ? { proposedPairNumber: v.proposedPairNumber } : {}),
      ...(v.proposedWeekType ? { proposedWeekType: v.proposedWeekType } : {}),
      ...(!v.proposedIsOnline && v.proposedRoomId ? { proposedRoomId: v.proposedRoomId } : {})
    };

    this.api.createRescheduleRequest(dto).subscribe({
      next: () => {
        this.snackBar.open('Запрос отправлен', 'OK', { duration: 3000 });
        this.api.getRescheduleRequests().subscribe(r => this.myRequests = r);
        this.form.patchValue({
          originalEntryId: '', reason: '',
          proposedDayOfWeek: null, proposedPairNumber: null, proposedWeekType: null,
          proposedRoomId: null, proposedIsOnline: false
        });
        this.availableRooms = [];
      },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  statusLabel(status: string): string {
    switch (status) {
      case RescheduleStatus.Pending: return 'Ожидает';
      case RescheduleStatus.Approved: return 'Одобрен';
      case RescheduleStatus.Rejected: return 'Отклонён';
      default: return status;
    }
  }

  statusClass(status: string): string {
    return 'status-' + status.toLowerCase();
  }
}
