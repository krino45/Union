import { Pipe, PipeTransform } from '@angular/core';
import { RoomType } from '../../core/models/enums';

@Pipe({ name: 'roomType', standalone: true })
export class RoomTypePipe implements PipeTransform {
  transform(value: string): string {
    switch (value) {
      case RoomType.LectureHall: return 'Лекционный зал';
      case RoomType.RegularCabinet: return 'Кабинет';
      case RoomType.Lab: return 'Лаборатория';
      case RoomType.ComputerLab: return 'Компьютерный класс';
      case RoomType.Virtual: return 'Дистанционно';
      default: return value;
    }
  }
}
