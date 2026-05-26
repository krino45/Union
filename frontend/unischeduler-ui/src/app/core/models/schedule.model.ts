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
  name?: string;
  ownerUserId?: string;
  ownerUsername?: string;
  isOpenToAdmins?: boolean;
  isMine?: boolean;
}

export interface CreateScheduleDto {
  academicYear: number;
  term: Term;
  startDate: string;
  endDate: string;
  facultyId?: string | null;
  allowCrossFacultyLessons: boolean;
  name?: string;
}

export interface ValidationIssue {
  severity: 'error' | 'warning' | 'info';
  code: string;
  message: string;
}

export interface ValidateEditBody {
  entryId?: string | null;
  subjectId: string;
  teacherId: string;
  roomId?: string | null;
  groupIds: string[];
  dayOfWeek: string;
  pairNumber: number;
  weekType: string;
  lessonType: string;
  isOnline: boolean;
  subgroupLabel?: string | null;
}

export interface SplitEditBody {
  targetWeek: 'Odd' | 'Even';
  subjectId: string;
  teacherId: string;
  roomId?: string | null;
  groupIds: string[];
  dayOfWeek: string;
  pairNumber: number;
  lessonType: string;
  isOnline: boolean;
}

export interface InvitationDto {
  id: string;
  email: string;
  universityRole: 'Admin' | 'Teacher';
  systemRole: string;
  createdAt: string;
  expiresAt: string;
  isConsumed: boolean;
  invitedByUsername?: string;
}

export interface InvitationInfo {
  isValid: boolean;
  email: string | null;
  universityName: string | null;
  universityShortName: string | null;
  universityRole: 'Admin' | 'Teacher' | null;
  teacherDisplayName: string | null;
  teacherAlreadyLinked: boolean;
  mode: 'register' | 'accept' | 'wrong-account' | 'invalid';
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
  middlePair: number;
  latePair: number;
  consecRunScalar: number;
  saturdayPenalty: number;
  departmentMismatchPenalty: number;
  walkingPenaltyMax: number;
  stairFloorMeters: number;
}
