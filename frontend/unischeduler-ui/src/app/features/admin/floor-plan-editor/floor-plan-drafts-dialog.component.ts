import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA, MatDialog } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { ApiService } from '../../../core/services/api.service';
import { FloorPlanDraftSummary, FloorPlanSummary } from '../../../core/models';

interface DialogData {
  buildingId: string;
  buildingShortCode: string;
}

interface DialogResult {
  action: 'open-draft' | 'load-active' | null;
  draftId?: string;
}

@Component({
  selector: 'app-floor-plan-drafts-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatDialogModule, MatButtonModule, MatIconModule, MatSlideToggleModule,
    MatFormFieldModule, MatInputModule, MatTableModule, MatTooltipModule,
    MatChipsModule, MatSnackBarModule, MatDividerModule
  ],
  template: `
    <h2 mat-dialog-title>Версии планировки: {{ data.buildingShortCode }}</h2>
    <mat-dialog-content>
      <h3>Опубликованные версии</h3>
      <p class="hint" *ngIf="versions.length === 0">Нет опубликованных версий.</p>
      <table mat-table [dataSource]="versions" class="full-table" *ngIf="versions.length > 0">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let v" data-label="Название">
            <mat-chip *ngIf="v.isActive" color="primary" highlighted style="margin-right: 8px">активна</mat-chip>
            {{ v.name }}
          </td>
        </ng-container>
        <ng-container matColumnDef="created">
          <th mat-header-cell *matHeaderCellDef>Создана</th>
          <td mat-cell *matCellDef="let v" data-label="Создана">{{ v.createdAt | date: 'dd.MM.yyyy HH:mm' }} {{ v.createdByUsername ? '— ' + v.createdByUsername : '' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let v">
            <button mat-button color="primary" *ngIf="!v.isActive" (click)="activate(v)">Активировать</button>
            <button mat-icon-button color="warn" *ngIf="!v.isActive" (click)="deleteVersion(v)" matTooltip="Удалить">
              <mat-icon>delete</mat-icon>
            </button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="versionCols"></tr>
        <tr mat-row *matRowDef="let row; columns: versionCols;"></tr>
      </table>

      <mat-divider style="margin: 24px 0"></mat-divider>

      <h3>Черновики</h3>
      <p class="hint" *ngIf="drafts.length === 0">Нет видимых черновиков.</p>
      <table mat-table [dataSource]="drafts" class="full-table" *ngIf="drafts.length > 0">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Название</th>
          <td mat-cell *matCellDef="let d" data-label="Название">
            {{ d.name }}
            <mat-chip *ngIf="!d.isMine" style="margin-left: 8px">{{ d.ownerUsername }}</mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="modified">
          <th mat-header-cell *matHeaderCellDef>Изменён</th>
          <td mat-cell *matCellDef="let d" data-label="Изменён">{{ d.lastModified | date: 'dd.MM.yyyy HH:mm' }}</td>
        </ng-container>
        <ng-container matColumnDef="access">
          <th mat-header-cell *matHeaderCellDef>Доступ</th>
          <td mat-cell *matCellDef="let d" data-label="Доступ">
            <mat-slide-toggle *ngIf="d.isMine"
                              [checked]="d.isOpenToAdmins"
                              (change)="toggleAccess(d, $event.checked)"
                              matTooltip="Разрешить другим админам редактировать">
              {{ d.isOpenToAdmins ? 'открыт' : 'закрыт' }}
            </mat-slide-toggle>
            <mat-chip *ngIf="!d.isMine && d.isOpenToAdmins">открытый</mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let d">
            <button mat-icon-button (click)="openDraft(d)" matTooltip="Открыть в редакторе">
              <mat-icon>edit</mat-icon>
            </button>
            <button mat-icon-button *ngIf="d.isMine" (click)="rename(d)" matTooltip="Переименовать">
              <mat-icon>drive_file_rename_outline</mat-icon>
            </button>
            <button mat-button color="primary" *ngIf="d.isMine" (click)="publish(d)" matTooltip="Опубликовать как версию">
              <mat-icon>publish</mat-icon> Опубликовать
            </button>
            <button mat-icon-button color="warn" *ngIf="d.isMine" (click)="deleteDraft(d)" matTooltip="Удалить">
              <mat-icon>delete</mat-icon>
            </button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="draftCols"></tr>
        <tr mat-row *matRowDef="let row; columns: draftCols;"></tr>
      </table>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Закрыть</button>
    </mat-dialog-actions>
  `,
  styles: [
    '.full-table { width: 100%; min-width: 560px; }',
    'h3 { margin: 0 0 8px; font-size: 14px; color: #555; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; }',
    '.hint { color: #999; font-style: italic; margin: 4px 0 16px; }',
    'mat-chip { font-size: 11px; }'
  ]
})
export class FloorPlanDraftsDialogComponent implements OnInit {
  drafts: FloorPlanDraftSummary[] = [];
  versions: FloorPlanSummary[] = [];
  draftCols = ['name', 'modified', 'access', 'actions'];
  versionCols = ['name', 'created', 'actions'];

  constructor(
    private api: ApiService,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<FloorPlanDraftsDialogComponent, DialogResult>,
    private snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public data: DialogData
  ) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.api.listFloorPlanDrafts(this.data.buildingId).subscribe(d => this.drafts = d);
    this.api.listFloorPlans(this.data.buildingId).subscribe(v => this.versions = v);
  }

  openDraft(d: FloorPlanDraftSummary): void {
    this.dialogRef.close({ action: 'open-draft', draftId: d.id });
  }

  toggleAccess(d: FloorPlanDraftSummary, value: boolean): void {
    this.api.setFloorPlanDraftAccess(this.data.buildingId, d.id, value).subscribe({
      next: () => this.reload(),
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  rename(d: FloorPlanDraftSummary): void {
    const name = prompt('Новое название', d.name);
    if (!name) return;
    this.api.renameFloorPlanDraft(this.data.buildingId, d.id, name).subscribe({
      next: () => this.reload(),
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  publish(d: FloorPlanDraftSummary): void {
    const name = prompt('Название опубликованной версии', d.name) || d.name;
    if (!confirm(`Опубликовать черновик "${d.name}"? Текущая активная версия станет неактивной.`)) return;
    this.api.publishFloorPlanFromDraft(this.data.buildingId, d.id, name).subscribe({
      next: () => {
        this.snackBar.open('Версия опубликована', 'OK', { duration: 2000 });
        this.reload();
        this.dialogRef.close({ action: 'load-active' });
      },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  deleteDraft(d: FloorPlanDraftSummary): void {
    if (!confirm(`Удалить черновик "${d.name}"?`)) return;
    this.api.deleteFloorPlanDraft(this.data.buildingId, d.id).subscribe({
      next: () => this.reload(),
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  activate(v: FloorPlanSummary): void {
    if (!confirm(`Сделать "${v.name}" активной версией планировки?`)) return;
    this.api.activateFloorPlan(this.data.buildingId, v.id).subscribe({
      next: () => {
        this.snackBar.open('Версия активирована', 'OK', { duration: 2000 });
        this.reload();
        this.dialogRef.close({ action: 'load-active' });
      },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  deleteVersion(v: FloorPlanSummary): void {
    if (!confirm(`Удалить версию "${v.name}"?`)) return;
    this.api.deleteFloorPlanVersion(this.data.buildingId, v.id).subscribe({
      next: () => this.reload(),
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}
