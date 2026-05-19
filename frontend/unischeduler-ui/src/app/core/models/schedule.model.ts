import { ScheduleStatus, Term } from './enums';

export interface Schedule {
  id: string;
  academicYear: number;
  term: Term;
  startDate: string;
  endDate: string;
  facultyId?: string;
  facultyName?: string;
  allowCrossFacultyLessons: boolean;
  status: ScheduleStatus;
  generatedAt?: string;
  generationNotes?: string;
}

export interface CreateScheduleDto {
  academicYear: number;
  term: Term;
  startDate: string;
  endDate: string;
  facultyId?: string | null;
  allowCrossFacultyLessons: boolean;
}

export interface GenerationJobStatus {
  scheduleId: string;
  status: 'queued' | 'running' | 'completed' | 'failed' | 'not_found';
  message?: string;
  stage?: string;
  entriesCreated: number;
  completedAt?: string;
}

export interface GenerateScheduleRequest {
  timeoutSeconds: number;
}

export interface SolverWeights {
  studentWindow: number;
  teacherWindow: number;
  activeDay: number;
  sanPinOverload: number;
  consecLecture: number;
  consecSeminar: number;
  consecPractical: number;
  consecLab: number;
  earlyPair: number;
  latePair: number;
  consecRunScalar: number;
}
