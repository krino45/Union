export interface Teacher {
  id: string;
  firstName: string;
  lastName: string;
  middleName: string;
  displayName: string;
  email: string;
  subjects?: TeacherSubjectDto[];
  loadHoursPerWeek?: number;
}

export interface TeacherSubjectDto {
  subjectId: string;
  subjectName: string;
  lessonType: string;
  preferredRoomType?: string;
}

export interface CreateTeacherDto {
  firstName: string;
  lastName: string;
  middleName: string;
  email: string;
}

export interface UpdateTeacherDto extends CreateTeacherDto {}

export interface TeacherSubjectAssignment {
  subjectId: string;
  lessonType: string;
  preferredRoomType?: string;
}
