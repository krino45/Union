import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Building, CreateBuildingDto, UpdateBuildingDto, BuildingDistance, UpsertDistanceDto,
  Room, CreateRoomDto, UpdateRoomDto,
  Faculty, CreateFacultyDto, UpdateFacultyDto,
  Teacher, CreateTeacherDto, UpdateTeacherDto, TeacherSubjectAssignment,
  Subject, CreateSubjectDto, UpdateSubjectDto,
  StudentGroup, CreateStudentGroupDto, UpdateStudentGroupDto,
  Schedule, CreateScheduleDto, GenerationJobStatus, GenerateScheduleRequest,
  ScheduleEntry, MoveEntryDto, ConflictInfo, CreateScheduleEntryDto, UpdateScheduleEntryDto,
  TeacherAvailability, CreateTeacherAvailabilityDto, UpdateTeacherAvailabilityDto,
  RescheduleRequest, CreateRescheduleRequestDto, ResolveRescheduleDto,
  StudyPlan, CalendarPlan, UpsertStudyPlanDto, UpsertCalendarPlanDto, PlanProgressItem
} from '../models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // ── Buildings ─────────────────────────────────────────────────────────────
  getBuildings(): Observable<Building[]> {
    return this.http.get<Building[]>(`${this.base}/buildings`);
  }
  getBuilding(id: string): Observable<Building> {
    return this.http.get<Building>(`${this.base}/buildings/${id}`);
  }
  createBuilding(dto: CreateBuildingDto): Observable<Building> {
    return this.http.post<Building>(`${this.base}/buildings`, dto);
  }
  updateBuilding(id: string, dto: UpdateBuildingDto): Observable<void> {
    return this.http.put<void>(`${this.base}/buildings/${id}`, dto);
  }
  deleteBuilding(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/buildings/${id}`);
  }
  getBuildingDistances(): Observable<BuildingDistance[]> {
    return this.http.get<BuildingDistance[]>(`${this.base}/buildings/distances`);
  }
  upsertDistance(dto: UpsertDistanceDto): Observable<void> {
    return this.http.put<void>(`${this.base}/buildings/distances`, [dto]);
  }

  // ── Rooms ─────────────────────────────────────────────────────────────────
  getRooms(filters?: { buildingId?: string; type?: string; minCapacity?: number }): Observable<Room[]> {
    let params = new HttpParams();
    if (filters?.buildingId) params = params.set('buildingId', filters.buildingId);
    if (filters?.type) params = params.set('type', filters.type);
    if (filters?.minCapacity != null) params = params.set('minCapacity', filters.minCapacity.toString());
    return this.http.get<Room[]>(`${this.base}/rooms`, { params });
  }
  getRoom(id: string): Observable<Room> {
    return this.http.get<Room>(`${this.base}/rooms/${id}`);
  }
  createRoom(dto: CreateRoomDto): Observable<Room> {
    return this.http.post<Room>(`${this.base}/rooms`, dto);
  }
  updateRoom(id: string, dto: UpdateRoomDto): Observable<void> {
    return this.http.put<void>(`${this.base}/rooms/${id}`, dto);
  }
  deleteRoom(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/rooms/${id}`);
  }

  // ── Teachers ─────────────────────────────────────────────────────────────
  getTeachers(): Observable<Teacher[]> {
    return this.http.get<Teacher[]>(`${this.base}/teachers`);
  }
  getTeacher(id: string): Observable<Teacher> {
    return this.http.get<Teacher>(`${this.base}/teachers/${id}`);
  }
  createTeacher(dto: CreateTeacherDto): Observable<Teacher> {
    return this.http.post<Teacher>(`${this.base}/teachers`, dto);
  }
  updateTeacher(id: string, dto: UpdateTeacherDto): Observable<void> {
    return this.http.put<void>(`${this.base}/teachers/${id}`, dto);
  }
  deleteTeacher(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/teachers/${id}`);
  }
  updateTeacherSubjects(id: string, assignments: TeacherSubjectAssignment[]): Observable<void> {
    return this.http.put<void>(`${this.base}/teachers/${id}/subjects`, assignments);
  }

  promoteGroups(facultyId?: string | null): Observable<{ promoted: number }> {
    return this.http.post<{ promoted: number }>(`${this.base}/student-groups/promote`, { facultyId: facultyId ?? null });
  }

  // ── Subjects ─────────────────────────────────────────────────────────────
  getSubjects(academicYear?: number, term?: string): Observable<Subject[]> {
    let params = new HttpParams();
    if (academicYear != null) params = params.set('academicYear', academicYear.toString());
    if (term) params = params.set('term', term);
    return this.http.get<Subject[]>(`${this.base}/subjects`, { params });
  }
  getSubject(id: string): Observable<Subject> {
    return this.http.get<Subject>(`${this.base}/subjects/${id}`);
  }
  createSubject(dto: CreateSubjectDto): Observable<Subject> {
    return this.http.post<Subject>(`${this.base}/subjects`, dto);
  }
  updateSubject(id: string, dto: UpdateSubjectDto): Observable<void> {
    return this.http.put<void>(`${this.base}/subjects/${id}`, dto);
  }
  deleteSubject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/subjects/${id}`);
  }

  // ── Student Groups ────────────────────────────────────────────────────────
  getGroups(): Observable<StudentGroup[]> {
    return this.http.get<StudentGroup[]>(`${this.base}/student-groups`);
  }
  getGroup(id: string): Observable<StudentGroup> {
    return this.http.get<StudentGroup>(`${this.base}/student-groups/${id}`);
  }
  createGroup(dto: CreateStudentGroupDto): Observable<StudentGroup> {
    return this.http.post<StudentGroup>(`${this.base}/student-groups`, dto);
  }
  updateGroup(id: string, dto: UpdateStudentGroupDto): Observable<void> {
    return this.http.put<void>(`${this.base}/student-groups/${id}`, dto);
  }
  deleteGroup(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/student-groups/${id}`);
  }

  // ── Faculties ─────────────────────────────────────────────────────────────
  getFaculties(): Observable<Faculty[]> {
    return this.http.get<Faculty[]>(`${this.base}/faculties`);
  }
  createFaculty(dto: CreateFacultyDto): Observable<Faculty> {
    return this.http.post<Faculty>(`${this.base}/faculties`, dto);
  }
  updateFaculty(id: string, dto: UpdateFacultyDto): Observable<Faculty> {
    return this.http.put<Faculty>(`${this.base}/faculties/${id}`, dto);
  }
  deleteFaculty(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/faculties/${id}`);
  }

  // ── Pair Times ────────────────────────────────────────────────────────────
  getPairTimes(): Observable<{ pairNumber: number; startTime: string; endTime: string }[]> {
    return this.http.get<{ pairNumber: number; startTime: string; endTime: string }[]>(`${this.base}/pair-times`);
  }

  // ── Schedules ─────────────────────────────────────────────────────────────
  getSchedules(): Observable<Schedule[]> {
    return this.http.get<Schedule[]>(`${this.base}/schedules`);
  }
  getSchedule(id: string): Observable<Schedule> {
    return this.http.get<Schedule>(`${this.base}/schedules/${id}`);
  }
  createSchedule(dto: CreateScheduleDto): Observable<Schedule> {
    return this.http.post<Schedule>(`${this.base}/schedules`, dto);
  }
  deleteSchedule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/schedules/${id}`);
  }
  generateSchedule(id: string, dto: GenerateScheduleRequest): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.base}/schedules/${id}/generate`, dto);
  }
  getGenerationStatus(id: string): Observable<GenerationJobStatus> {
    return this.http.get<GenerationJobStatus>(`${this.base}/schedules/${id}/generate/status`);
  }
  publishSchedule(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/schedules/${id}/publish`, {});
  }
  archiveSchedule(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/schedules/${id}/archive`, {});
  }
  unarchiveSchedule(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/schedules/${id}/unarchive`, {});
  }
  getScheduleAudit(scheduleId: string): Observable<{
    conflicts: { type: string; description: string }[];
    warnings: { type: string; description: string }[];
    generationNotes: string | null;
    totalEntries: number;
  }> {
    return this.http.get<any>(`${this.base}/schedules/${scheduleId}/audit`);
  }
  getScheduleEntries(scheduleId: string, filters?: { groupId?: string; teacherId?: string }): Observable<ScheduleEntry[]> {
    let params = new HttpParams();
    if (filters?.groupId) params = params.set('groupId', filters.groupId);
    if (filters?.teacherId) params = params.set('teacherId', filters.teacherId);
    return this.http.get<ScheduleEntry[]>(`${this.base}/schedules/${scheduleId}/entries`, { params });
  }

  // ── Schedule Entries ──────────────────────────────────────────────────────
  createEntry(dto: CreateScheduleEntryDto): Observable<ScheduleEntry> {
    return this.http.post<ScheduleEntry>(`${this.base}/schedule-entries`, dto);
  }
  updateEntry(id: string, dto: UpdateScheduleEntryDto): Observable<void> {
    return this.http.put<void>(`${this.base}/schedule-entries/${id}`, dto);
  }
  deleteEntry(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/schedule-entries/${id}`);
  }
  moveEntry(id: string, dto: MoveEntryDto): Observable<void> {
    return this.http.post<void>(`${this.base}/schedule-entries/${id}/move`, dto);
  }
  checkConflicts(dto: CreateScheduleEntryDto): Observable<ConflictInfo[]> {
    return this.http.post<ConflictInfo[]>(`${this.base}/schedule-entries/conflicts`, dto);
  }

  // ── Teacher Availability ──────────────────────────────────────────────────
  getAvailability(teacherId?: string): Observable<TeacherAvailability[]> {
    let params = new HttpParams();
    if (teacherId) params = params.set('teacherId', teacherId);
    return this.http.get<TeacherAvailability[]>(`${this.base}/teacher-availability`, { params });
  }
  createAvailability(dto: CreateTeacherAvailabilityDto): Observable<TeacherAvailability> {
    return this.http.post<TeacherAvailability>(`${this.base}/teacher-availability`, dto);
  }
  updateAvailability(id: string, dto: UpdateTeacherAvailabilityDto): Observable<void> {
    return this.http.put<void>(`${this.base}/teacher-availability/${id}`, dto);
  }
  deleteAvailability(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/teacher-availability/${id}`);
  }

  // ── Reschedule Requests ───────────────────────────────────────────────────
  getRescheduleRequests(): Observable<RescheduleRequest[]> {
    return this.http.get<RescheduleRequest[]>(`${this.base}/reschedule-requests`);
  }
  createRescheduleRequest(dto: CreateRescheduleRequestDto): Observable<RescheduleRequest> {
    return this.http.post<RescheduleRequest>(`${this.base}/reschedule-requests`, dto);
  }
  approveRescheduleRequest(id: string, dto: ResolveRescheduleDto): Observable<void> {
    return this.http.put<void>(`${this.base}/reschedule-requests/${id}/approve`, dto);
  }
  rejectRescheduleRequest(id: string, dto: ResolveRescheduleDto): Observable<void> {
    return this.http.put<void>(`${this.base}/reschedule-requests/${id}/reject`, dto);
  }

  // ── Study Plans ───────────────────────────────────────────────────────────
  getStudyPlans(academicYear?: number, term?: string): Observable<StudyPlan[]> {
    let params = new HttpParams();
    if (academicYear != null) params = params.set('academicYear', academicYear.toString());
    if (term) params = params.set('term', term);
    return this.http.get<StudyPlan[]>(`${this.base}/study-plans`, { params });
  }
  getStudyPlan(id: string): Observable<StudyPlan> {
    return this.http.get<StudyPlan>(`${this.base}/study-plans/${id}`);
  }
  createStudyPlan(dto: UpsertStudyPlanDto): Observable<StudyPlan> {
    return this.http.post<StudyPlan>(`${this.base}/study-plans`, dto);
  }
  updateStudyPlan(id: string, dto: UpsertStudyPlanDto): Observable<StudyPlan> {
    return this.http.put<StudyPlan>(`${this.base}/study-plans/${id}`, dto);
  }
  deleteStudyPlan(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/study-plans/${id}`);
  }

  // ── Calendar Plans ────────────────────────────────────────────────────────
  getCalendarPlans(academicYear?: number, term?: string): Observable<CalendarPlan[]> {
    let params = new HttpParams();
    if (academicYear != null) params = params.set('academicYear', academicYear.toString());
    if (term) params = params.set('term', term);
    return this.http.get<CalendarPlan[]>(`${this.base}/calendar-plans`, { params });
  }
  getCalendarPlan(id: string): Observable<CalendarPlan> {
    return this.http.get<CalendarPlan>(`${this.base}/calendar-plans/${id}`);
  }
  createCalendarPlan(dto: UpsertCalendarPlanDto): Observable<CalendarPlan> {
    return this.http.post<CalendarPlan>(`${this.base}/calendar-plans`, dto);
  }
  updateCalendarPlan(id: string, dto: UpsertCalendarPlanDto): Observable<CalendarPlan> {
    return this.http.put<CalendarPlan>(`${this.base}/calendar-plans/${id}`, dto);
  }
  deleteCalendarPlan(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/calendar-plans/${id}`);
  }

  // ── Plan progress ─────────────────────────────────────────────────────────
  getPlanProgress(scheduleId: string): Observable<PlanProgressItem[]> {
    return this.http.get<PlanProgressItem[]>(`${this.base}/schedules/${scheduleId}/plan-progress`);
  }

  // ── JSON Export / Import ──────────────────────────────────────────────────
  exportJson(scheduleId: string): Observable<Blob> {
    return this.http.get(`${this.base}/schedules/${scheduleId}/export/json`, { responseType: 'blob' });
  }
  importJson(scheduleId: string, entries: any[], replace: boolean): Observable<{ committed: number; errors: string[] }> {
    return this.http.post<{ committed: number; errors: string[] }>(
      `${this.base}/schedules/${scheduleId}/import/json`,
      { replace, entries }
    );
  }

  // ── Excel ─────────────────────────────────────────────────────────────────
  exportExcel(scheduleId: string, groupId?: string, teacherId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (groupId) params = params.set('groupId', groupId);
    if (teacherId) params = params.set('teacherId', teacherId);
    return this.http.get(`${this.base}/excel/export/${scheduleId}`, {
      params,
      responseType: 'blob'
    });
  }
  previewImport(scheduleId: string, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('scheduleId', scheduleId);
    return this.http.post<any>(`${this.base}/excel/import`, formData);
  }
  confirmImport(scheduleId: string, preview: any): Observable<{ committed: number }> {
    return this.http.post<{ committed: number }>(`${this.base}/excel/import/confirm`, { scheduleId, preview });
  }
}
