import { Term } from './enums';

export interface Subject {
  id: string;
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
}

export interface CreateSubjectDto {
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
}

export interface UpdateSubjectDto extends CreateSubjectDto {}
