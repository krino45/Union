import { Term } from './enums';

export interface Subject {
  id: string;
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
  departmentId?: string | null;
  departmentName?: string | null;
}

export interface CreateSubjectDto {
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
  departmentId?: string | null;
}

export interface UpdateSubjectDto extends CreateSubjectDto {}
