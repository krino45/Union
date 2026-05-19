import { DegreeType, RussianDayOfWeek } from './enums';

export interface StudentGroup {
  id: string;
  name: string;
  year: number;
  specialty: string;
  studentCount: number;
  degreeType: DegreeType;
  facultyId: string;
  facultyName: string;
  blockedDays: RussianDayOfWeek[];
}

export interface CreateStudentGroupDto {
  name: string;
  year: number;
  specialty: string;
  studentCount: number;
  degreeType: DegreeType;
  facultyId: string;
  blockedDays: RussianDayOfWeek[];
}

export interface UpdateStudentGroupDto extends CreateStudentGroupDto {}
