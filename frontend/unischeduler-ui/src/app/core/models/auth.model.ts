export interface LoginRequest {
  username: string;
  password: string;
}

export interface UniversityAccess {
  universityId: string;
  universityName: string;
  shortName: string;
  logoUrl?: string;
  role: 'Admin' | 'Teacher';
}

export interface AuthResponse {
  token: string;
  username: string;
  role: string;
  teacherId?: string;
  universities: UniversityAccess[];
}

export interface CurrentUser {
  token: string;
  username: string;
  role: string;
  teacherId?: string;
  universities: UniversityAccess[];
  selectedUniversity?: UniversityAccess;
}

export interface University {
  id: string;
  name: string;
  shortName: string;
  logoUrl?: string;
}

export interface UniversityUser {
  userId: string;
  username: string;
  systemRole: string;
  universityRole: string;
}
