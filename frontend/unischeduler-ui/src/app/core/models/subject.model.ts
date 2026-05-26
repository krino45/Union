import { Term } from './enums';

export interface Subject {
  id: string;
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
  departmentId?: string | null;
  departmentName?: string | null;
  allowsSubgroups: boolean;
  subgroupCount: number;
}

export interface CreateSubjectDto {
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
  departmentId?: string | null;
  allowsSubgroups: boolean;
  subgroupCount: number;
}

export interface UpdateSubjectDto extends CreateSubjectDto {}
