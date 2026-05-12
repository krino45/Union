import { Pipe, PipeTransform } from '@angular/core';
import { RussianDayOfWeek } from '../../core/models/enums';

const DAY_NAMES: Record<string, string> = {
  [RussianDayOfWeek.Monday]: 'Понедельник',
  [RussianDayOfWeek.Tuesday]: 'Вторник',
  [RussianDayOfWeek.Wednesday]: 'Среда',
  [RussianDayOfWeek.Thursday]: 'Четверг',
  [RussianDayOfWeek.Friday]: 'Пятница',
  [RussianDayOfWeek.Saturday]: 'Суббота',
};

const DAY_SHORT: Record<string, string> = {
  [RussianDayOfWeek.Monday]: 'Пн',
  [RussianDayOfWeek.Tuesday]: 'Вт',
  [RussianDayOfWeek.Wednesday]: 'Ср',
  [RussianDayOfWeek.Thursday]: 'Чт',
  [RussianDayOfWeek.Friday]: 'Пт',
  [RussianDayOfWeek.Saturday]: 'Сб',
};

@Pipe({ name: 'dayOfWeek', standalone: true })
export class DayOfWeekPipe implements PipeTransform {
  transform(value: string, format: 'full' | 'short' = 'full'): string {
    return format === 'short' ? (DAY_SHORT[value] ?? '') : (DAY_NAMES[value] ?? '');
  }
}
