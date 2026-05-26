import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, NG_VALUE_ACCESSOR, ControlValueAccessor, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule, MatSelectChange } from '@angular/material/select';
import { NgxMatSelectSearchModule } from 'ngx-mat-select-search';

@Component({
  selector: 'app-search-select',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatFormFieldModule, MatSelectModule, NgxMatSelectSearchModule],
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SearchSelectComponent), multi: true }
  ],
  template: `
    <mat-form-field [appearance]="appearance" class="full-width">
      <mat-label>{{ label }}</mat-label>
      <mat-select [multiple]="multiple" [value]="value" [disabled]="disabled"
                  (selectionChange)="onSelectionChange($event)"
                  (openedChange)="onOpened($event)">
        <mat-option>
          <ngx-mat-select-search [formControl]="searchCtrl"
            [placeholderLabel]="searchPlaceholder"
            noEntriesFoundLabel="Ничего не найдено"></ngx-mat-select-search>
        </mat-option>
        <mat-option *ngIf="allowNull && !multiple" [value]="null">{{ nullLabel }}</mat-option>
        <mat-option *ngFor="let o of filtered" [value]="o[valueField]">{{ display(o) }}</mat-option>
      </mat-select>
      <mat-hint *ngIf="hint">{{ hint }}</mat-hint>
    </mat-form-field>
  `,
  styles: [`:host { display: block; } .full-width { width: 100%; }`]
})
export class SearchSelectComponent implements ControlValueAccessor {
  @Input() label = '';
  @Input() options: any[] = [];
  @Input() valueField = 'id';
  @Input() displayField = 'name';
  @Input() displayWith?: (o: any) => string;
  @Input() multiple = false;
  @Input() allowNull = false;
  @Input() nullLabel = '—';
  @Input() searchPlaceholder = 'Поиск...';
  @Input() appearance: 'fill' | 'outline' = 'outline';
  @Input() hint = '';

  value: any = null;
  disabled = false;
  searchCtrl = new FormControl('');

  private onChange: (v: any) => void = () => {};
  onTouched: () => void = () => {};

  get filtered(): any[] {
    const term = (this.searchCtrl.value || '').trim().toLowerCase();
    if (!term) return this.options;
    return this.options.filter(o => this.display(o).toLowerCase().includes(term));
  }

  display(o: any): string {
    return this.displayWith ? this.displayWith(o) : (o?.[this.displayField] ?? '');
  }

  onSelectionChange(e: MatSelectChange): void {
    this.value = e.value;
    this.onChange(e.value);
  }

  onOpened(opened: boolean): void {
    if (opened) this.searchCtrl.setValue('');
    else this.onTouched();
  }

  writeValue(v: any): void { this.value = v; }
  registerOnChange(fn: (v: any) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(d: boolean): void { this.disabled = d; }
}
