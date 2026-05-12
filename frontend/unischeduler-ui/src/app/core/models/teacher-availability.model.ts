import { WeekType, RussianDayOfWeek } from './enums';

export interface TeacherAvailability {
  id: string;
  teacherId: string;
  teacherFullName?: string;
  dayOfWeek: RussianDayOfWeek;
  pairNumber: number;
  weekType: WeekType;
  reason?: string;
  validFrom?: string;
  validTo?: string;
  isRecurring: boolean;
}

export interface CreateTeacherAvailabilityDto {
  teacherId: string;
  dayOfWeek: RussianDayOfWeek;
  pairNumber: number;
  weekType: WeekType;
  reason?: string;
  validFrom?: string;
  validTo?: string;
  isRecurring: boolean;
}

export interface UpdateTeacherAvailabilityDto extends CreateTeacherAvailabilityDto {}
