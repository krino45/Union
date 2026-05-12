import { DegreeType } from './enums';

export interface StudentGroup {
  id: string;
  name: string;
  year: number;
  specialty: string;
  studentCount: number;
  degreeType: DegreeType;
  facultyId: string;
  facultyName: string;
}

export interface CreateStudentGroupDto {
  name: string;
  year: number;
  specialty: string;
  studentCount: number;
  degreeType: DegreeType;
  facultyId: string;
}

export interface UpdateStudentGroupDto extends CreateStudentGroupDto {}
