export interface StudyPlan {
  id: string;
  name: string;
  academicYear: number;
  term: string;
  facultyId: string | null;
  facultyName: string | null;
  calendarPlanId: string | null;
  calendarPlanName: string | null;
  groups: StudyPlanGroup[];
  entries: StudyPlanEntry[];
}

export interface StudyPlanGroup {
  studentGroupId: string;
  groupName: string;
}

export interface StudyPlanEntry {
  id: string;
  subjectId: string;
  subjectName: string;
  subjectShortName: string;
  lectureHours: number;
  practicalHours: number;
  labHours: number;
  seminarHours: number;
  thesisHours: number;
  languageHours: number;
  physicalEducationHours: number;
}

export interface CalendarPlan {
  id: string;
  name: string;
  academicYear: number;
  term: string;
  weeks: CalendarWeek[];
}

export interface CalendarWeek {
  id: string;
  startDate: string; // ISO date string
  kind: WeekKind;
  note: string | null;
}

export type WeekKind = 'Study' | 'Holiday' | 'ExamPreparation' | 'Exams' | 'Practice' | 'Thesis' | 'Other';

export interface PlanProgressItem {
  subjectId: string;
  subjectName: string;
  subjectShortName: string;
  groupId: string;
  groupName: string;
  lessonType: string;
  expectedHours: number;
  actualPairsPerWeek: number;
  studyWeeks: number;
  isUnplaced: boolean;
}

// ── Upsert DTOs ───────────────────────────────────────────────────────────────

export interface UpsertStudyPlanDto {
  name: string;
  academicYear: number;
  term: string;
  facultyId: string | null;
  calendarPlanId: string | null;
  groupIds: string[];
  entries: UpsertStudyPlanEntryDto[];
}

export interface UpsertStudyPlanEntryDto {
  subjectId: string;
  lectureHours: number;
  practicalHours: number;
  labHours: number;
  seminarHours: number;
  thesisHours: number;
  languageHours: number;
  physicalEducationHours: number;
}

export interface UpsertCalendarPlanDto {
  name: string;
  academicYear: number;
  term: string;
  weeks: UpsertCalendarWeekDto[];
}

export interface UpsertCalendarWeekDto {
  startDate: string;
  kind: WeekKind;
  note: string | null;
}
