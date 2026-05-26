import { LessonType, WeekType, RussianDayOfWeek } from './enums';

export interface ScheduleEntry {
  id: string;
  scheduleId: string;
  subjectId: string;
  subjectName: string;
  subjectShortName: string;
  teacherId: string;
  teacherDisplayName: string;
  roomId?: string;
  roomNumber?: string;
  buildingShortCode?: string;
  dayOfWeek: RussianDayOfWeek;
  pairNumber: number;
  weekType: WeekType;
  lessonType: LessonType;
  isOnline: boolean;
  studentGroups: { id: string; name: string }[];
  parallelGroupId?: string | null;
  subgroupLabel?: string | null;
}

export interface MoveEntryDto {
  dayOfWeek: RussianDayOfWeek;
  pairNumber: number;
  weekType: WeekType;
  roomId?: string;
}

export interface ConflictInfo {
  type: string;
  description: string;
  entryId?: string;
}

export interface CreateScheduleEntryDto {
  scheduleId: string;
  subjectId: string;
  teacherId: string;
  roomId?: string;
  dayOfWeek: RussianDayOfWeek;
  pairNumber: number;
  weekType: WeekType;
  lessonType: LessonType;
  isOnline: boolean;
  groupIds: string[];
}

export interface UpdateScheduleEntryDto {
  subjectId: string;
  teacherId: string;
  roomId?: string;
  dayOfWeek: RussianDayOfWeek;
  pairNumber: number;
  weekType: WeekType;
  lessonType: LessonType;
  isOnline: boolean;
  groupIds: string[];
}
