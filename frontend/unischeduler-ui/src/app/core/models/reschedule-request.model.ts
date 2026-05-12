import { RescheduleStatus, WeekType, RussianDayOfWeek } from './enums';

export interface RescheduleRequest {
  id: string;
  requestedByTeacherId: string;
  requestedByTeacherName: string;
  originalEntryId: string;
  originalEntryDescription?: string;
  proposedDay?: RussianDayOfWeek;
  proposedPair?: number;
  proposedWeekType?: WeekType;
  reason: string;
  status: RescheduleStatus;
  adminNote?: string;
  createdAt: string;
  resolvedAt?: string;
}

export interface CreateRescheduleRequestDto {
  originalEntryId: string;
  proposedDay?: RussianDayOfWeek;
  proposedPair?: number;
  proposedWeekType?: WeekType;
  reason: string;
}

export interface ResolveRescheduleDto {
  adminNote?: string;
}
