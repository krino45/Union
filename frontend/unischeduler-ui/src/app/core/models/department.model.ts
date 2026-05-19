export interface Department {
  id: string;
  name: string;
  shortCode: string;
  facultyId: string;
  facultyName: string;
}

export interface CreateDepartmentDto {
  name: string;
  shortCode: string;
  facultyId: string;
}

export interface UpdateDepartmentDto extends CreateDepartmentDto {}
