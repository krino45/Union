export interface Semester {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  totalWeeks: number;
}

export interface CreateSemesterDto {
  name: string;
  startDate: string;
  endDate: string;
  totalWeeks: number;
}

export interface UpdateSemesterDto extends CreateSemesterDto {}
