import { Pipe, PipeTransform } from '@angular/core';
import { LessonType } from '../../core/models/enums';

@Pipe({ name: 'lessonType', standalone: true })
export class LessonTypePipe implements PipeTransform {
  transform(value: string): string {
    switch (value) {
      case LessonType.Lecture: return 'Лекция';
      case LessonType.Practical: return 'Практика';
      case LessonType.Lab: return 'Лабораторная';
      case LessonType.Seminar: return 'Семинар';
      case LessonType.Language: return 'Ин. язык';
      default: return value;
    }
  }
}
