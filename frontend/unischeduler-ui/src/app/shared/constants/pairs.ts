import { RussianDayOfWeek } from '../../core/models/enums';

export const PAIR_TIMES: { pair: number; start: string; end: string }[] = [
  { pair: 1, start: '08:00', end: '09:30' },
  { pair: 2, start: '09:40', end: '11:10' },
  { pair: 3, start: '11:20', end: '12:50' },
  { pair: 4, start: '13:00', end: '14:30' },
  { pair: 5, start: '14:40', end: '16:10' },
  { pair: 6, start: '16:20', end: '17:50' },
];

export const DAYS: RussianDayOfWeek[] = [
  RussianDayOfWeek.Monday,
  RussianDayOfWeek.Tuesday,
  RussianDayOfWeek.Wednesday,
  RussianDayOfWeek.Thursday,
  RussianDayOfWeek.Friday,
  RussianDayOfWeek.Saturday,
];

export const PAIRS = [1, 2, 3, 4, 5, 6] as const;
