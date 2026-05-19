import { LessonType, RoomType } from './enums';

export interface Room {
  id: string;
  buildingId: string;
  buildingShortCode?: string;
  number: string;
  roomType: RoomType;
  capacity: number;
  hasProjector: boolean;
  hasComputers: boolean;
  hasLab: boolean;
  isOnline: boolean;
  floor: number;
  allowedLessonTypes: LessonType[];
  isEnabled: boolean;
  departmentId?: string | null;
  departmentName?: string | null;
}

export interface CreateRoomDto {
  buildingId: string;
  number: string;
  roomType: RoomType;
  capacity: number;
  hasProjector: boolean;
  hasComputers: boolean;
  hasLab: boolean;
  isOnline: boolean;
  floor: number;
  allowedLessonTypes: LessonType[];
  isEnabled: boolean;
  departmentId?: string | null;
}

export interface UpdateRoomDto extends CreateRoomDto {}
