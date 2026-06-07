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
import { AuthService } from '../../../../core/services/auth.service';
import { Building } from '../../../../core/models';


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
      <span class="city-value">{{ city || 'Не указан' }}</span>
      <span class="city-hint">— задаётся в настройках вуза</span>
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
          <td mat-cell *matCellDef="let b" data-label="Код"><strong>{{ b.shortCode }}</strong></td>
        </ng-container>
        <ng-container matColumnDef="address">
          <th mat-header-cell *matHeaderCellDef>Адрес</th>
          <td mat-cell *matCellDef="let b" data-label="Адрес">{{ b.address }}</td>
        </ng-container>
        <ng-container matColumnDef="floors">
          <th mat-header-cell *matHeaderCellDef matTooltip="Этажей наземных / подземных">Этажи</th>
          <td mat-cell *matCellDef="let b" data-label="Этажи">
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

    <div class="distances-note">
      <mat-icon class="note-icon">route</mat-icon>
      Расстояния между корпусами задаются в редакторе планировок — на узлах «Вход».
    </div>
  `,
  styles: [`
    .city-bar {
      display: flex; align-items: center; gap: 8px; margin-bottom: 20px;
      padding: 8px 16px; background: #e8eaf6; border-radius: 8px; font-size: 14px;
    }
    .city-icon { color: #3f51b5; font-size: 20px; }
    .city-label { font-weight: 600; }
    .city-value { font-size: 15px; font-weight: 500; }
    .city-hint { color: #777; font-size: 12px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; }
    .full-width { width: 100%; }
    .distances-note {
      margin-top: 24px; display: flex; align-items: center; gap: 8px;
      padding: 12px 16px; background: #f1f8e9; border-radius: 8px;
      font-size: 13px; color: #33691e;
    }
    .note-icon { color: #558b2f; }
    .loading-wrap { display: flex; justify-content: center; padding: 32px; }
    .basement-badge { font-size: 11px; color: #5c6bc0; margin-left: 4px; }
  `]
})
export class BuildingsComponent implements OnInit {
  buildings: Building[] = [];
  loading = true;
  columns = ['shortCode', 'address', 'floors', 'actions'];
  city: string = '';

  constructor(private api: ApiService, private auth: AuthService, private dialog: MatDialog, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.city = this.auth.currentUniversity?.city ?? '';
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api.getBuildings().subscribe({
      next: data => { this.buildings = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
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
