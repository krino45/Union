export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  username: string;
  role: string;
  teacherId?: string;
}

export interface CurrentUser {
  token: string;
  username: string;
  role: string;
  teacherId?: string;
}
