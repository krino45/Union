import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { PAIR_TIMES } from '../../shared/constants/pairs';

// Holds the current university's pair-time grid.
@Injectable({ providedIn: 'root' })
export class PairTimesService {
  readonly times: { pair: number; start: string; end: string }[] = PAIR_TIMES.map(t => ({ ...t }));
  private loaded = false;

  constructor(private api: ApiService) {}

  ensureLoaded(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.api.getPairTimes().subscribe({
      next: pt => {
        if (pt?.length) {
          const mapped = [...pt]
            .sort((a, b) => a.pairNumber - b.pairNumber)
            .map(p => ({ pair: p.pairNumber, start: p.startTime.slice(0, 5), end: p.endTime.slice(0, 5) }));
          this.times.splice(0, this.times.length, ...mapped);
        }
      },
      error: () => { this.loaded = false; }
    });
  }
}
