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

export interface ScoreBreakdown {
  hardConflicts: number;
  studentWindows: number;
  teacherWindows: number;
  activeDays: number;
  walking: number;
  sanPinOverload: number;
  consecSameLesson: number;
  timeOfDay: number;
  saturday: number;
  deptMismatch: number;
  overflow: number;
  roomTypeMismatch: number;
  blockedPlacement: number;
  total: number;
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

export interface BackfillTargets {
  rooms: boolean;
  teachers: boolean;
  studyPlans: boolean;
  roomBindings: boolean;
  groupSizes: boolean;
  roomTypes: boolean;
  subjectProjector: boolean;
  presetGroupSize: number;
}

export interface RoomBackfillChange {
  roomId: string;
  roomLabel: string;
  addedTypes: string[];
}

export interface TeacherSubjectAdd {
  subjectId: string;
  subjectName: string;
  lessonType: string;
}

export interface TeacherBackfillChange {
  teacherId: string;
  teacherName: string;
  added: TeacherSubjectAdd[];
}

export interface StudyPlanHourChange {
  subjectId: string;
  subjectName: string;
  field: string;
  fieldLabel: string;
  oldHours: number;
  newHours: number;
}

export interface StudyPlanBackfillChange {
  studyPlanId: string;
  planName: string;
  changes: StudyPlanHourChange[];
}

export interface SubjectRoomBindingChange {
  subjectId: string;
  subjectName: string;
  lessonType: string;
  roomLabels: string[];
}

export interface GroupSizeChange {
  groupId: string;
  groupName: string;
  oldSize: number;
  newSize: number;
}

export interface RoomCapacityChange {
  roomId: string;
  roomLabel: string;
  oldCapacity: number;
  newCapacity: number;
}

export interface RoomTypeChange {
  roomId: string;
  roomLabel: string;
  oldType: string;
  newType: string;
}

export interface SubjectProjectorChange {
  subjectId: string;
  subjectName: string;
}

export interface BackfillPreview {
  rooms: RoomBackfillChange[];
  teachers: TeacherBackfillChange[];
  studyPlans: StudyPlanBackfillChange[];
  roomBindings: SubjectRoomBindingChange[];
  groupSizes: GroupSizeChange[];
  roomCapacities: RoomCapacityChange[];
  roomTypes: RoomTypeChange[];
  subjectProjectors: SubjectProjectorChange[];
}

export interface BackfillResult {
  roomsUpdated: number;
  teacherLinksAdded: number;
  studyPlanFieldsUpdated: number;
  roomBindingsAdded: number;
  groupSizesUpdated: number;
  roomCapacitiesUpdated: number;
  roomTypesUpdated: number;
  subjectProjectorsUpdated: number;
}

export interface StageLogItem {
  seq: number;
  at: string;
  message: string;
}

export interface GenerationJobStatus {
  scheduleId: string;
  status: 'queued' | 'running' | 'completed' | 'failed' | 'not_found';
  message?: string;
  stage?: string;
  entriesCreated: number;
  completedAt?: string;
  log?: StageLogItem[];
  latestSeq?: number;
}

export interface GenerateScheduleRequest {
  timeoutSeconds: number;
  planIds?: string[] | null;
  polish?: boolean;
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
  maxPePerDay: number;
  peNotLastPenalty: number;
  peConsecutiveReward: number;
  languagePerTeacherCap: number;
  physicalEducationPerTeacherCap: number;
}
