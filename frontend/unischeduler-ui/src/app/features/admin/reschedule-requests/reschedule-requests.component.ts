import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../../core/services/api.service';
import { RescheduleRequest } from '../../../core/models';
import { RescheduleStatus, WeekType } from '../../../core/models/enums';
import { DayOfWeekPipe } from '../../../shared/pipes/day-of-week.pipe';
import { WeekTypePipe } from '../../../shared/pipes/week-type.pipe';

@Component({
  selector: 'app-reschedule-requests',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatChipsModule,
    MatSnackBarModule, MatTooltipModule,
    DayOfWeekPipe, WeekTypePipe
  ],
  template: `
    <div class="page-header">
      <h1>Запросы на перенос</h1>
    </div>

    <mat-card>
      <table mat-table [dataSource]="requests" class="full-width">
        <ng-container matColumnDef="teacher">
          <th mat-header-cell *matHeaderCellDef>Преподаватель</th>
          <td mat-cell *matCellDef="let r" data-label="Преподаватель">{{ r.teacherName }}</td>
        </ng-container>
        <ng-container matColumnDef="entry">
          <th mat-header-cell *matHeaderCellDef>Занятие</th>
          <td mat-cell *matCellDef="let r" data-label="Занятие">{{ r.originalEntryDescription || '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="proposed">
          <th mat-header-cell *matHeaderCellDef>Предлагает</th>
          <td mat-cell *matCellDef="let r" data-label="Предлагает">
            <span *ngIf="r.proposedDayOfWeek">{{ r.proposedDayOfWeek | dayOfWeek: 'short' }}, пара {{ r.proposedPairNumber }}</span>
            <span *ngIf="r.proposedWeekType"> ({{ r.proposedWeekType | weekType }})</span>
            <span *ngIf="!r.proposedDayOfWeek">Любое свободное</span>
            <span *ngIf="r.proposedIsOnline" class="online-tag"> · онлайн</span>
            <span *ngIf="r.proposedRoomName" class="room-tag"> · {{ r.proposedRoomName }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="reason">
          <th mat-header-cell *matHeaderCellDef>Причина</th>
          <td mat-cell *matCellDef="let r" data-label="Причина">{{ r.reason }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Статус</th>
          <td mat-cell *matCellDef="let r" data-label="Статус">
            <mat-chip [class]="statusClass(r.status)">{{ statusLabel(r.status) }}</mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Дата</th>
          <td mat-cell *matCellDef="let r" data-label="Дата">{{ r.createdAt | date:'dd.MM.yyyy' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let r">
            <ng-container *ngIf="r.status === 'Pending'">
              <button mat-icon-button color="primary" (click)="resolve(r, true)" matTooltip="Одобрить">
                <mat-icon>check_circle</mat-icon>
              </button>
              <button mat-icon-button color="warn" (click)="resolve(r, false)" matTooltip="Отклонить">
                <mat-icon>cancel</mat-icon>
              </button>
            </ng-container>
            <span *ngIf="r.adminNote" [matTooltip]="r.adminNote" class="note-icon">
              <mat-icon>comment</mat-icon>
            </span>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;"></tr>
      </table>
    </mat-card>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .status-pending { background: #fff3e0; color: #e65100; }
    .status-approved { background: #e8f5e9; color: #1b5e20; }
    .status-rejected { background: #ffebee; color: #b71c1c; }
    .note-icon { color: #888; }
    .online-tag { color: #1565c0; font-weight: 600; }
    .room-tag { color: #555; }
  `]
})
export class RescheduleRequestsComponent implements OnInit {
  requests: RescheduleRequest[] = [];
  columns = ['teacher', 'entry', 'proposed', 'reason', 'status', 'date', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getRescheduleRequests().subscribe(data => this.requests = data);
  }

  resolve(request: RescheduleRequest, approve: boolean): void {
    // Approval applies the teacher's proposed slot via a schedule move, so it needs a concrete day + pair.
    if (approve && (!request.proposedDayOfWeek || request.proposedPairNumber == null)) {
      this.snackBar.open('Преподаватель не указал конкретный слот — перенесите занятие вручную через редактор расписания.', 'OK', { duration: 6000 });
      return;
    }
    const ref = this.dialog.open(ResolveDialogComponent, {
      data: { approve, request }, width: '380px'
    });
    ref.afterClosed().subscribe(note => {
      if (note === undefined) return;
      const obs = approve
        ? this.api.approveRescheduleRequest(request.id, {
            newDay: request.proposedDayOfWeek,
            newPair: request.proposedPairNumber,
            newWeekType: request.proposedWeekType ?? WeekType.Both,
            newRoomId: request.proposedRoomId,
            newIsOnline: request.proposedIsOnline,
            adminNote: note
          })
        : this.api.rejectRescheduleRequest(request.id, { adminNote: note });
      obs.subscribe({
        next: () => {
          this.load();
          this.snackBar.open(approve ? 'Запрос одобрен' : 'Запрос отклонён', 'OK', { duration: 3000 });
        },
        error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
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

@Component({
  selector: 'app-resolve-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.approve ? 'Одобрить' : 'Отклонить' }} запрос</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Комментарий администратора (необязательно)</mat-label>
        <textarea matInput [(ngModel)]="adminNote" rows="3"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button [color]="data.approve ? 'primary' : 'warn'" (click)="submit()">
        {{ data.approve ? 'Одобрить' : 'Отклонить' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`.full-width { width: 100%; } `]
})
export class ResolveDialogComponent {
  adminNote = '';

  constructor(
    private dialogRef: MatDialogRef<ResolveDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { approve: boolean; request: RescheduleRequest }
  ) {}

  submit(): void {
    this.dialogRef.close(this.adminNote || undefined);
  }
}
