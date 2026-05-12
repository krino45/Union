import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
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
import { ApiService } from '../../../../core/services/api.service';
import { Room, Building } from '../../../../core/models';
import { RoomType } from '../../../../core/models/enums';
import { RoomTypePipe } from '../../../../shared/pipes/room-type.pipe';

@Component({
  selector: 'app-rooms',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatCheckboxModule, MatSnackBarModule, MatTooltipModule, MatChipsModule,
    RoomTypePipe
  ],
  template: `
    <div class="page-header">
      <h1>Аудитории</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <table mat-table [dataSource]="rooms" class="full-width">
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
          <th mat-header-cell *matHeaderCellDef>Этаж / от лестн.</th>
          <td mat-cell *matCellDef="let r">
            <span *ngIf="!r.isOnline">{{ r.floor }} эт. / {{ r.distanceFromStairsMeters }} м</span>
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
            <mat-chip *ngIf="r.hasProjector">Проектор</mat-chip>
            <mat-chip *ngIf="r.hasComputers">ПК</mat-chip>
            <mat-chip *ngIf="r.hasLab">Лаборатория</mat-chip>
            <mat-chip *ngIf="r.isOnline">Онлайн</mat-chip>
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
        <tr mat-row *matRowDef="let row; columns: columns;"></tr>
      </table>
    </mat-card>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    mat-chip { font-size: 11px; margin: 1px; }
  `]
})
export class RoomsComponent implements OnInit {
  rooms: Room[] = [];
  buildings: Building[] = [];
  columns = ['number', 'building', 'location', 'type', 'capacity', 'features', 'actions'];

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getBuildings().subscribe(b => { this.buildings = b; this.load(); });
  }

  load(): void {
    this.api.getRooms().subscribe(data => this.rooms = data);
  }

  openDialog(room: Room | null): void {
    const ref = this.dialog.open(RoomDialogComponent, {
      data: { room, buildings: this.buildings }, width: '520px'
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
    MatSelectModule, MatCheckboxModule, MatDialogModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.room ? 'Редактировать' : 'Добавить' }} аудиторию</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Корпус</mat-label>
          <mat-select formControlName="buildingId">
            <mat-option *ngFor="let b of data.buildings" [value]="b.id">{{ b.shortCode }} — {{ b.address }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Номер аудитории</mat-label>
          <input matInput formControlName="number" placeholder="101, А-203...">
        </mat-form-field>
        <div class="row-fields">
          <mat-form-field appearance="outline" class="half-width">
            <mat-label>Этаж</mat-label>
            <input matInput type="number" formControlName="floor" min="1">
          </mat-form-field>
          <mat-form-field appearance="outline" class="half-width">
            <mat-label>Расст. от лестницы (м)</mat-label>
            <input matInput type="number" formControlName="distanceFromStairsMeters" min="0" step="5">
          </mat-form-field>
        </div>
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
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; margin-bottom: 8px; }
    .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 400px; }
    .checkboxes { display: flex; flex-direction: column; gap: 4px; margin-bottom: 8px; }
    .row-fields { display: flex; gap: 12px; margin-bottom: 8px; }
    .half-width { flex: 1; }
  `]
})
export class RoomDialogComponent {
  form: FormGroup;

  constructor(
    private dialogRef: MatDialogRef<RoomDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { room: Room | null; buildings: Building[] },
    private fb: FormBuilder
  ) {
    const r = data.room;
    this.form = this.fb.group({
      buildingId: [r?.buildingId ?? '', Validators.required],
      number: [r?.number ?? '', Validators.required],
      floor: [r?.floor ?? 1, [Validators.required, Validators.min(1)]],
      distanceFromStairsMeters: [r?.distanceFromStairsMeters ?? 0, [Validators.required, Validators.min(0)]],
      roomType: [r?.roomType ?? RoomType.RegularCabinet, Validators.required],
      capacity: [r?.capacity ?? 30, [Validators.required, Validators.min(1)]],
      hasProjector: [r?.hasProjector ?? false],
      hasComputers: [r?.hasComputers ?? false],
      hasLab: [r?.hasLab ?? false],
      isOnline: [r?.isOnline ?? false]
    });
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }
}
