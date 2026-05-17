import { Pipe, PipeTransform } from '@angular/core';
import { WeekType } from '../../core/models/enums';

@Pipe({ name: 'weekType', standalone: true })
export class WeekTypePipe implements PipeTransform {
  transform(value: string): string {
    switch (value) {
      case WeekType.Both: return 'Каждую';
      case WeekType.Odd: return 'Нечётная';
      case WeekType.Even: return 'Чётная';
      default: return value;
    }
  }
}
