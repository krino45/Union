import { Injectable, Inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { BehaviorSubject } from 'rxjs';

/**
 * Holds light/dark mode srttings in localStorage
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly key = 'darkMode';
  private readonly _dark = new BehaviorSubject<boolean>(localStorage.getItem(this.key) === 'true');
  readonly dark$ = this._dark.asObservable();

  constructor(@Inject(DOCUMENT) private doc: Document) {
    this.apply(this._dark.value);
  }

  get isDark(): boolean {
    return this._dark.value;
  }

  toggle(): void {
    this.set(!this._dark.value);
  }

  set(dark: boolean): void {
    this._dark.next(dark);
    localStorage.setItem(this.key, String(dark));
    this.apply(dark);
  }

  private apply(dark: boolean): void {
    this.doc.body.classList.toggle('dark-mode', dark);
  }
}
