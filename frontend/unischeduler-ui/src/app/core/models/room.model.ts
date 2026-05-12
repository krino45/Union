import { RoomType } from './enums';

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
  distanceFromStairsMeters: number;
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
  distanceFromStairsMeters: number;
}

export interface UpdateRoomDto extends CreateRoomDto {}
