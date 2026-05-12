import { Term, WeekType } from './enums';

export interface Subject {
  id: string;
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
  lectureHoursPerWeek: number;
  practicalHoursPerWeek: number;
  labHoursPerWeek: number;
  lectureWeekType: WeekType;
  practicalWeekType: WeekType;
  labWeekType: WeekType;
}

export interface CreateSubjectDto {
  name: string;
  shortName: string;
  academicYear: number;
  term: Term;
  lectureHoursPerWeek: number;
  practicalHoursPerWeek: number;
  labHoursPerWeek: number;
  lectureWeekType: WeekType;
  practicalWeekType: WeekType;
  labWeekType: WeekType;
}

export interface UpdateSubjectDto extends CreateSubjectDto {}
