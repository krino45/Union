import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, finalize } from 'rxjs';
import { Router } from '@angular/router';
import { LoginRequest, AuthResponse, CurrentUser, UniversityAccess } from '../models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // The auth token is an httpOnly cookie (shared across tabs, unreadable by JS). This localStorage
  // entry is only a non-secret profile cache so route guards can run synchronously on reload; it is
  // re-validated against /auth/me on startup and kept in sync across tabs.
  private readonly userKey = 'unischeduler_user';
  private currentUserSubject = new BehaviorSubject<CurrentUser | null>(this.loadUser());
  private readonly channel = ('BroadcastChannel' in window) ? new BroadcastChannel('uni-auth') : null;

  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    this.listenForCrossTabChanges();
    if (this.currentUser) {
      this.me().subscribe({ error: (e) => { if (e?.status === 401) this.clearLocal(); } });
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

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap(response => this.storeUser(response))
    );
  }

  registerFromInvitation(token: string, username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register-from-invitation`, { token, username, password }).pipe(
      tap(response => this.storeUser(response))
    );
  }

  acceptInvitation(token: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/accept-invitation`, { token }).pipe(
      tap(response => this.storeUser(response, true))
    );
  }

  /** Hydrate/refresh the session from the cookie. Used on startup and after self-grants. */
  me(): Observable<AuthResponse> {
    return this.http.get<AuthResponse>(`${environment.apiUrl}/auth/me`).pipe(
      tap(response => this.storeUser(response, true))
    );
  }

  /** Kept for callers that expect to refresh universities (superadmin self-grant). */
  renewToken(): Observable<AuthResponse> {
    return this.me();
  }

  selectUniversity(access: UniversityAccess): void {
    const user = this.currentUser;
    if (!user) return;
    this.persist({ ...user, selectedUniversity: access });
  }

  clearUniversitySelection(): void {
    const user = this.currentUser;
    if (!user) return;
    this.persist({ ...user, selectedUniversity: undefined });
  }

  logout(): void {
    this.http.post(`${environment.apiUrl}/auth/logout`, {}).subscribe({ next: () => {}, error: () => {} });
    this.clearLocal();
    this.channel?.postMessage({ type: 'logout' });
    this.router.navigate(['/login']);
  }

  endSession(): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/auth/logout`, {}).pipe(
      finalize(() => {
        this.clearLocal();
        this.channel?.postMessage({ type: 'logout' });
      })
    );
  }

  handleUnauthorized(): void {
    if (!this.currentUser) return;
    this.clearLocal();
    this.channel?.postMessage({ type: 'logout' });
    this.router.navigate(['/login']);
  }

  private storeUser(response: AuthResponse, keepSelected = false): void {
    const existing = this.currentUser;
    const user: CurrentUser = {
      username: response.username,
      role: response.role,
      teacherId: response.teacherId,
      email: response.email,
      universities: response.universities ?? [],
      selectedUniversity: keepSelected ? existing?.selectedUniversity : undefined
    };
    this.persist(user);
  }

  private persist(user: CurrentUser): void {
    localStorage.setItem(this.userKey, JSON.stringify(user));
    this.currentUserSubject.next(user);
    this.channel?.postMessage({ type: 'sync' });
  }

  private clearLocal(): void {
    localStorage.removeItem(this.userKey);
    this.currentUserSubject.next(null);
  }

  private loadUser(): CurrentUser | null {
    try {
      const data = localStorage.getItem(this.userKey);
      if (!data) return null;
      const user = JSON.parse(data) as CurrentUser;
      if (!user.universities) user.universities = [];
      return user;
    } catch {
      return null;
    }
  }

  private listenForCrossTabChanges(): void {
    this.channel?.addEventListener('message', (e: MessageEvent) => {
      if (e.data?.type === 'logout') {
        if (this.currentUser) this.clearLocal();
      } else if (e.data?.type === 'sync') {
        this.applyCrossTabState(this.loadUser());
      }
    });

    // Fallback for browsers without BroadcastChannel: the storage event fires in other tabs.
    window.addEventListener('storage', (e: StorageEvent) => {
      if (e.key !== this.userKey) return;
      this.applyCrossTabState(e.newValue === null ? null : this.loadUser());
    });
  }

  private applyCrossTabState(user: CurrentUser | null): void {
    this.currentUserSubject.next(user);
    if (user && this.router.url.split('?')[0] === '/login') {
      this.router.navigate(['/']);
    }
  }
}
