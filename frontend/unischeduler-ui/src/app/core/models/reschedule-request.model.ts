import { RescheduleStatus, WeekType, RussianDayOfWeek } from './enums';

export interface RescheduleRequest {
  id: string;
  requestedByTeacherId: string;
  teacherName: string;
  originalEntryId: string;
  originalEntryDescription?: string;
  proposedDayOfWeek?: RussianDayOfWeek;
  proposedPairNumber?: number;
  proposedWeekType?: WeekType;
  proposedRoomId?: string;
  proposedRoomName?: string;
  proposedIsOnline: boolean;
  reason: string;
  status: RescheduleStatus;
  adminNote?: string;
  createdAt: string;
  resolvedAt?: string;
}

export interface CreateRescheduleRequestDto {
  teacherId: string;
  originalEntryId: string;
  proposedDayOfWeek?: RussianDayOfWeek;
  proposedPairNumber?: number;
  proposedWeekType?: WeekType;
  proposedRoomId?: string;
  proposedIsOnline: boolean;
  reason: string;
}

export interface ResolveRescheduleDto {
  newDay?: RussianDayOfWeek;
  newPair?: number;
  newWeekType?: WeekType;
  newRoomId?: string;
  newIsOnline?: boolean;
  adminNote?: string;
}
