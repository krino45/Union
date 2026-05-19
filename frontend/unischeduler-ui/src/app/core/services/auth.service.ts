import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { LoginRequest, AuthResponse, CurrentUser, UniversityAccess } from '../models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly userKey = 'unischeduler_user';
  private currentUserSubject = new BehaviorSubject<CurrentUser | null>(this.loadUser());

  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    if (this.currentUser) {
      this.tryRenewOnStartup();
    }
  }

  get currentUser(): CurrentUser | null {
    return this.currentUserSubject.value;
  }

  get isAuthenticated(): boolean {
    return !!this.currentUser;
  }

  get isSuperAdmin(): boolean {
    return this.currentUser?.role === 'SuperAdmin';
  }

  get isAdmin(): boolean {
    const selected = this.currentUser?.selectedUniversity;
    return selected?.role === 'Admin' || this.isSuperAdmin;
  }

  get isTeacher(): boolean {
    return this.currentUser?.selectedUniversity?.role === 'Teacher';
  }

  get currentUniversity(): UniversityAccess | undefined {
    return this.currentUser?.selectedUniversity;
  }

  get canSwitchUniversity(): boolean {
    const u = this.currentUser;
    if (!u) return false;
    return u.universities.length > 1 || this.isSuperAdmin;
  }

  get token(): string | null {
    return this.currentUser?.token ?? null;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap(response => this.storeUser(response))
    );
  }

  renewToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/renew`, {}).pipe(
      tap(response => this.storeUser(response, true))
    );
  }

  selectUniversity(access: UniversityAccess): void {
    const user = this.currentUser;
    if (!user) return;
    const updated: CurrentUser = { ...user, selectedUniversity: access };
    localStorage.setItem(this.userKey, JSON.stringify(updated));
    this.currentUserSubject.next(updated);
  }

  clearUniversitySelection(): void {
    const user = this.currentUser;
    if (!user) return;
    const updated: CurrentUser = { ...user, selectedUniversity: undefined };
    localStorage.setItem(this.userKey, JSON.stringify(updated));
    this.currentUserSubject.next(updated);
  }

  logout(): void {
    localStorage.removeItem(this.userKey);
    this.currentUserSubject.next(null);
    this.router.navigate(['/login']);
  }

  private storeUser(response: AuthResponse, keepSelected = false): void {
    const existing = this.currentUser;
    const user: CurrentUser = {
      token: response.token,
      username: response.username,
      role: response.role,
      teacherId: response.teacherId,
      universities: response.universities ?? [],
      selectedUniversity: keepSelected ? existing?.selectedUniversity : undefined
    };
    localStorage.setItem(this.userKey, JSON.stringify(user));
    this.currentUserSubject.next(user);
  }

  private loadUser(): CurrentUser | null {
    try {
      const data = localStorage.getItem(this.userKey);
      if (!data) return null;
      const user = JSON.parse(data) as CurrentUser;
      // Ensure backwards compatibility: if universities is missing, default to empty
      if (!user.universities) user.universities = [];
      return user;
    } catch {
      return null;
    }
  }

  private tryRenewOnStartup(): void {
    this.renewToken().subscribe({ error: () => {} });
  }
}
