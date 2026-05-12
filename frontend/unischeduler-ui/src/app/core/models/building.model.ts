export interface Building {
  id: string;
  shortCode: string;
  address: string;
  stairsDistancePerFloor: number;
}

export interface BuildingDistance {
  fromBuildingId: string;
  toBuildingId: string;
  distanceMeters: number;
  walkingMinutes: number;
  exceedsPairBreak: boolean;
}

export interface CreateBuildingDto {
  shortCode: string;
  address: string;
  stairsDistancePerFloor: number;
}

export interface UpdateBuildingDto {
  shortCode: string;
  address: string;
  stairsDistancePerFloor: number;
}

export interface UpsertDistanceDto {
  fromBuildingId: string;
  toBuildingId: string;
  distanceMeters: number;
}
