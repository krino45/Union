export interface Faculty {
  id: string;
  name: string;
  shortCode: string;
}

export interface CreateFacultyDto {
  name: string;
  shortCode: string;
}

export interface UpdateFacultyDto extends CreateFacultyDto {}
