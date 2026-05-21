import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Inject } from '@angular/core';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';
import { Subject, of, Observable } from 'rxjs';
import { ApiService } from '../../../../core/services/api.service';
import { Building, BuildingDistance } from '../../../../core/models';


@Component({
  selector: 'app-buildings',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSnackBarModule, MatTooltipModule, MatProgressSpinnerModule
  ],
  template: `
    <div class="city-bar">
      <mat-icon class="city-icon">location_city</mat-icon>
      <span class="city-label">Город:</span>
      <ng-container *ngIf="!editingCity; else cityEdit">
        <span class="city-value">{{ city || 'Не указан' }}</span>
        <button mat-icon-button (click)="startEditCity()" matTooltip="Изменить город">
          <mat-icon>edit</mat-icon>
        </button>
      </ng-container>
      <ng-template #cityEdit>
        <input #cityInput class="city-input" [value]="city" placeholder="Например: Москва"
               (keydown.enter)="saveCity(cityInput.value)"
               (keydown.escape)="editingCity = false">
        <button mat-icon-button (click)="saveCity(cityInput.value)" matTooltip="Сохранить">
          <mat-icon>check</mat-icon>
        </button>
        <button mat-icon-button (click)="editingCity = false" matTooltip="Отмена">
          <mat-icon>close</mat-icon>
        </button>
      </ng-template>
    </div>

    <div class="page-header">
      <h1>Корпуса</h1>
      <button mat-raised-button color="primary" (click)="openDialog(null)">
        <mat-icon>add</mat-icon> Добавить
      </button>
    </div>

    <mat-card>
      <div class="loading-wrap" *ngIf="loading"><mat-spinner diameter="40"></mat-spinner></div>
      <table mat-table [dataSource]="buildings" class="full-width" *ngIf="!loading">
        <ng-container matColumnDef="shortCode">
          <th mat-header-cell *matHeaderCellDef>Код</th>
          <td mat-cell *matCellDef="let b"><strong>{{ b.shortCode }}</strong></td>
        </ng-container>
        <ng-container matColumnDef="address">
          <th mat-header-cell *matHeaderCellDef>Адрес</th>
          <td mat-cell *matCellDef="let b">{{ b.address }}</td>
        </ng-container>
        <ng-container matColumnDef="floors">
          <th mat-header-cell *matHeaderCellDef matTooltip="Этажей наземных / подземных">Этажи</th>
          <td mat-cell *matCellDef="let b">
            {{ b.numberOfFloors }}
            <span *ngIf="b.numberOfBasementFloors > 0" class="basement-badge">+{{ b.numberOfBasementFloors }}п</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let b">
            <button mat-icon-button (click)="openDialog(b)" matTooltip="Редактировать">
              <mat-icon>edit</mat-icon>
            </button>
            <button mat-icon-button color="warn" (click)="delete(b)" matTooltip="Удалить">
              <mat-icon>delete</mat-icon>
            </button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;"></tr>
      </table>
    </mat-card>

    <div class="distances-section">
      <div class="section-header">
        <h2>Расстояния между корпусами (метры)</h2>
      </div>
      <mat-card>
        <div class="distance-grid" *ngIf="buildings.length > 1">
          <table class="dist-table">
            <thead>
              <tr>
                <th></th>
                <th *ngFor="let b of buildings">{{ b.shortCode }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let from of buildings">
                <th>{{ from.shortCode }}</th>
                <td *ngFor="let to of buildings">
                  <ng-container *ngIf="from.id !== to.id; else sameBuilding">
                    <input
                      type="number"
                      class="dist-input"
                      [class.exceeds]="getDistance(from.id, to.id)?.exceedsPairBreak"
                      [value]="getDistance(from.id, to.id)?.distanceMeters || ''"
                      (change)="setDistance(from.id, to.id, $event)"
                      min="0" step="10"
                      [matTooltip]="getDistanceTooltip(from.id, to.id)">
                  </ng-container>
                  <ng-template #sameBuilding>—</ng-template>
                </td>
              </tr>
            </tbody>
          </table>
          <div class="dist-legend">
            <span class="legend-red">Красный = ходьба > 10 мин (нарушение перемены)</span>
          </div>
        </div>
        <p *ngIf="buildings.length <= 1" class="empty-msg">Добавьте минимум 2 корпуса</p>
      </mat-card>
    </div>
  `,
  styles: [`
    .city-bar {
      display: flex; align-items: center; gap: 8px; margin-bottom: 20px;
      padding: 8px 16px; background: #e8eaf6; border-radius: 8px; font-size: 14px;
    }
    .city-icon { color: #3f51b5; font-size: 20px; }
    .city-label { font-weight: 600; }
    .city-value { font-size: 15px; }
    .city-input {
      border: 1px solid #9fa8da; border-radius: 4px; padding: 4px 8px;
      font-size: 14px; min-width: 200px; outline: none;
    }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .distances-section { margin-top: 32px; }
    .section-header { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
    h2 { margin: 0; }
    .dist-table { border-collapse: collapse; }
    .dist-table th, .dist-table td { border: 1px solid #e0e0e0; padding: 4px 8px; text-align: center; }
    .dist-table th { background: #f5f5f5; font-weight: 600; }
    .dist-input {
      width: 70px; border: 1px solid #e0e0e0; border-radius: 4px;
      padding: 4px; text-align: center; font-size: 13px;
    }
    .dist-input.exceeds { background: #ffcdd2; }
    .dist-legend { margin-top: 8px; font-size: 12px; color: #555; }
    .legend-red { color: #c62828; }
    .empty-msg { color: #888; text-align: center; padding: 16px; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
    .basement-badge { font-size: 11px; color: #5c6bc0; margin-left: 4px; }
  `]
})
export class BuildingsComponent implements OnInit {
  buildings: Building[] = [];
  distances: BuildingDistance[] = [];
  loading = true;
  columns = ['shortCode', 'address', 'floors', 'actions'];
  city: string = '';
  editingCity = false;

  constructor(private api: ApiService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.load();
  }

  startEditCity(): void {
    this.editingCity = true;
  }

  saveCity(value: string): void {
    this.city = value.trim();
    this.editingCity = false;
  }

  load(): void {
    this.loading = true;
    this.api.getBuildings().subscribe({
      next: data => { this.buildings = data; this.loading = false; this.refreshDistances(); },
      error: () => { this.loading = false; }
    });
  }

  refreshDistances(): void {
    this.api.getBuildingDistances().subscribe(data => this.distances = data);
  }

  openDialog(building: Building | null): void {
    const ref = this.dialog.open(BuildingDialogComponent, {
      data: { building, city: this.city },
      width: '440px'
    });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (building) {
        this.api.updateBuilding(building.id, result).subscribe({
          next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else {
        this.api.createBuilding(result).subscribe({
          next: () => { this.load(); this.snackBar.open('Добавлено', 'OK', { duration: 2000 }); },
          error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      }
    });
  }

  delete(building: Building): void {
    if (!confirm(`Удалить корпус "${building.shortCode}"?`)) return;
    this.api.deleteBuilding(building.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  getDistance(fromId: string, toId: string): BuildingDistance | undefined {
    return this.distances.find(d =>
      (d.fromBuildingId === fromId && d.toBuildingId === toId) ||
      (d.fromBuildingId === toId && d.toBuildingId === fromId)
    );
  }

  getDistanceTooltip(fromId: string, toId: string): string {
    const d = this.getDistance(fromId, toId);
    if (!d) return 'Не указано';
    return `${d.distanceMeters} м ≈ ${d.walkingMinutes.toFixed(1)} мин${d.exceedsPairBreak ? ' ⚠️ > 10 мин' : ''}`;
  }

  setDistance(fromId: string, toId: string, event: Event): void {
    const val = parseInt((event.target as HTMLInputElement).value, 10);
    if (isNaN(val) || val < 0) return;
    this.api.upsertDistance({ fromBuildingId: fromId, toBuildingId: toId, distanceMeters: val }).subscribe({
      next: () => this.refreshDistances(),
      error: (e) => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 3000 })
    });
  }
}


@Component({
  selector: 'app-building-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatDialogModule,
    MatAutocompleteModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.building ? 'Редактировать' : 'Добавить' }} корпус</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Короткий код</mat-label>
          <input matInput formControlName="shortCode" placeholder="А, Б, 1, Г...">
          <mat-hint>Используется как метка корпуса (например, А, Б, ГК)</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Адрес</mat-label>
          <input matInput formControlName="address"
                 [matAutocomplete]="auto"
                 (input)="onAddressInput($event)"
                 placeholder="{{ cityPrefix }}ул. ...">
          <mat-autocomplete #auto="matAutocomplete" (optionSelected)="onAddressSelected($event)">
            <mat-option *ngFor="let s of addressSuggestions" [value]="s">
              {{ s }}
            </mat-option>
          </mat-autocomplete>
          <mat-hint *ngIf="searchingAddress">Поиск...</mat-hint>
        </mat-form-field>
        <div class="row-fields">
          <mat-form-field appearance="outline" class="half-width">
            <mat-label>Этажей (наземных)</mat-label>
            <input matInput type="number" formControlName="numberOfFloors" min="1" step="1">
            <mat-hint>1 и выше</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" class="half-width">
            <mat-label>Подземных этажей</mat-label>
            <input matInput type="number" formControlName="numberOfBasementFloors" min="0" step="1">
            <mat-hint>0 = нет подвала</mat-hint>
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; margin-bottom: 12px; }
    .dialog-form { display: flex; flex-direction: column; padding-top: 8px; min-width: 360px; }
    .row-fields { display: flex; gap: 12px; margin-bottom: 12px; }
    .half-width { flex: 1; }
  `]
})
export class BuildingDialogComponent implements OnDestroy {
  form: FormGroup;
  addressSuggestions: string[] = [];
  searchingAddress = false;
  cityPrefix: string;
  private addressSubject = new Subject<string>();
  private sub = this.addressSubject.pipe(
    debounceTime(400),
    distinctUntilChanged(),
    switchMap(q => this.searchAddress(q))
  ).subscribe(results => {
    this.addressSuggestions = results;
    this.searchingAddress = false;
  });

  constructor(
    private dialogRef: MatDialogRef<BuildingDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { building: Building | null; city: string },
    private fb: FormBuilder,
    private api: ApiService
  ) {
    const b = data.building;
    this.cityPrefix = data.city ? data.city + ', ' : '';
    this.form = this.fb.group({
      shortCode: [b?.shortCode ?? '', Validators.required],
      address: [b?.address ?? '', Validators.required],
      numberOfFloors: [b?.numberOfFloors ?? 5, [Validators.required, Validators.min(1)]],
      numberOfBasementFloors: [b?.numberOfBasementFloors ?? 0, [Validators.required, Validators.min(0)]]
    });
  }

  onAddressInput(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    if (val.length < 3) { this.addressSuggestions = []; return; }
    this.searchingAddress = true;
    this.addressSubject.next(val);
  }

  onAddressSelected(event: { option: { value: string } }): void {
    this.form.patchValue({ address: event.option.value });
  }

  private searchAddress(query: string): Observable<string[]> {
    const text = (this.data.city ? this.data.city + ', ' : '') + query;
    return this.api.suggestAddress(text).pipe(catchError(() => of([])));
  }

  submit(): void {
    if (this.form.valid) this.dialogRef.close(this.form.value);
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }
}
