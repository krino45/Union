import { RussianDayOfWeek } from '../../core/models/enums';

export const PAIR_TIMES: { pair: number; start: string; end: string }[] = [
  { pair: 1, start: '08:00', end: '09:35' },
  { pair: 2, start: '09:50', end: '11:25' },
  { pair: 3, start: '11:40', end: '13:15' },
  { pair: 4, start: '13:45', end: '15:20' },
  { pair: 5, start: '15:35', end: '17:10' },
  { pair: 6, start: '17:25', end: '19:00' },
  { pair: 7, start: '19:15', end: '20:50' },
];

export const DAYS: RussianDayOfWeek[] = [
  RussianDayOfWeek.Monday,
  RussianDayOfWeek.Tuesday,
  RussianDayOfWeek.Wednesday,
  RussianDayOfWeek.Thursday,
  RussianDayOfWeek.Friday,
  RussianDayOfWeek.Saturday,
];

export const PAIRS = [1, 2, 3, 4, 5, 6, 7] as const;
