import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSelectModule } from '@angular/material/select';
import { MatDividerModule } from '@angular/material/divider';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { Router } from '@angular/router';
import { Observable, debounceTime, distinctUntilChanged, switchMap, of, startWith } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { UniversityAccess } from '../../core/models';
import { Teacher } from '../../core/models/teacher.model';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-superadmin',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSnackBarModule, MatTabsModule, MatSelectModule, MatDividerModule,
    MatTooltipModule, ThemeToggleComponent
  ],
  template: `
    <div class="superadmin-page">
      <div class="page-header">
        <div class="header-left">
          <mat-icon>admin_panel_settings</mat-icon>
          <h1>Управление системой</h1>
        </div>
        <div class="header-right">
          <app-theme-toggle [fixed]="false"></app-theme-toggle>
          <button mat-stroked-button (click)="goToUniSelect()">
            <mat-icon>arrow_back</mat-icon> К выбору университета
          </button>
        </div>
      </div>

      <mat-card>
        <mat-card-header>
          <mat-card-title>Университеты</mat-card-title>
          <span class="spacer"></span>
          <button mat-raised-button color="primary" (click)="openCreateDialog()">
            <mat-icon>add</mat-icon> Добавить
          </button>
        </mat-card-header>
        <mat-card-content>
          <table mat-table [dataSource]="universities" class="full-table">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Название</th>
              <td mat-cell *matCellDef="let u">{{ u.name }}</td>
            </ng-container>
            <ng-container matColumnDef="shortName">
              <th mat-header-cell *matHeaderCellDef>Краткое</th>
              <td mat-cell *matCellDef="let u">{{ u.shortName }}</td>
            </ng-container>
            <ng-container matColumnDef="city">
              <th mat-header-cell *matHeaderCellDef>Город</th>
              <td mat-cell *matCellDef="let u">{{ u.city || '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>Действия</th>
              <td mat-cell *matCellDef="let u">
                <button mat-icon-button (click)="enterUniversity(u)" matTooltip="Войти как администратор">
                  <mat-icon>login</mat-icon>
                </button>
                <button mat-icon-button (click)="openUsersDialog(u)" matTooltip="Управление доступом">
                  <mat-icon>people</mat-icon>
                </button>
                <button mat-icon-button (click)="openInvitationsDialog(u)" matTooltip="Приглашения">
                  <mat-icon>mail</mat-icon>
                </button>
                <button mat-icon-button (click)="openEditDialog(u)" matTooltip="Редактировать">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button color="warn" (click)="delete(u)" matTooltip="Удалить">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .superadmin-page { max-width: 960px; margin: 0 auto; }
    .page-header {
      display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px;
    }
    .header-left { display: flex; align-items: center; gap: 8px; }
    .header-left mat-icon { font-size: 28px; color: #7b1fa2; }
    .header-right { display: flex; align-items: center; gap: 8px; }
    h1 { margin: 0; }
    .spacer { flex: 1; }
    mat-card-header { display: flex; align-items: center; padding: 16px 16px 8px; }
    .full-table { width: 100%; }
  `]
})
export class SuperAdminComponent implements OnInit {
  universities: any[] = [];
  columns = ['name', 'shortName', 'city', 'actions'];

  constructor(
    private api: ApiService,
    public auth: AuthService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (this.auth.currentUniversity) {
      this.auth.clearUniversitySelection();
    }
    this.load();
  }

  load(): void {
    this.api.getUniversities().subscribe(data => this.universities = data);
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(UniversityDialogComponent, { data: null, width: '420px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.api.createUniversity(result).subscribe({
        next: () => { this.load(); this.snackBar.open('Университет создан', 'OK', { duration: 2000 }); },
        error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  openEditDialog(u: any): void {
    const ref = this.dialog.open(UniversityDialogComponent, { data: u, width: '420px' });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      this.api.updateUniversity(u.id, result).subscribe({
        next: () => { this.load(); this.snackBar.open('Сохранено', 'OK', { duration: 2000 }); },
        error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
      });
    });
  }

  openUsersDialog(u: any): void {
    this.dialog.open(UniversityUsersDialogComponent, { data: u, width: '640px' });
  }

  openInvitationsDialog(u: any): void {
    this.dialog.open(UniversityInvitationsDialogComponent, { data: u, width: '720px' });
  }

  enterUniversity(u: any): void {
    // SuperAdmin auto-grants themselves Admin access before entering so the X-University-Id header
    // gets through global query filters cleanly. Then renew the JWT so the local universities list
    // includes the new access row.
    this.api.grantSelfUniversityAccess(u.id).subscribe({
      next: () => this.refreshAndProceed(u),
      error: e => {
        if (e.status === 204 || e.status === 200) this.refreshAndProceed(u);
        else this.snackBar.open(e.error?.title || 'Не удалось получить доступ', 'OK', { duration: 4000 });
      }
    });
  }

  private refreshAndProceed(u: any): void {
    // renewToken() updates auth.currentUser.universities with the freshly-granted access
    this.auth.renewToken().subscribe({
      next: () => this.proceedToUniversity(u),
      error: () => this.proceedToUniversity(u)
    });
  }

  private proceedToUniversity(u: any): void {
    const access: UniversityAccess = {
      universityId: u.id,
      universityName: u.name,
      shortName: u.shortName,
      logoUrl: u.logoUrl,
      role: 'Admin'
    };
    this.auth.selectUniversity(access);
    this.router.navigate(['/admin/schedules']);
  }

  delete(u: any): void {
    if (!confirm(`Удалить университет "${u.name}"? Все данные будут удалены.`)) return;
    this.api.deleteUniversity(u.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Удалено', 'OK', { duration: 2000 }); },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  goToUniSelect(): void {
    this.auth.clearUniversitySelection();
    this.router.navigate(['/select-university']);
  }
}

@Component({
  selector: 'app-university-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Редактировать университет' : 'Новый университет' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Название</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Краткое название</mat-label>
          <input matInput formControlName="shortName">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Город</mat-label>
          <input matInput formControlName="city" placeholder="Москва">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>URL логотипа (необязательно)</mat-label>
          <input matInput formControlName="logoUrl">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Отмена</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="save()">Сохранить</button>
    </mat-dialog-actions>
  `,
  styles: ['.form { display: flex; flex-direction: column; min-width: 320px; } .full { width: 100%; }']
})
export class UniversityDialogComponent {
  form: FormGroup;
  constructor(
    private fb: FormBuilder,
    private ref: MatDialogRef<UniversityDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {
    this.form = this.fb.group({
      name: [data?.name ?? '', [Validators.required, Validators.maxLength(300)]],
      shortName: [data?.shortName ?? '', [Validators.required, Validators.maxLength(50)]],
      city: [data?.city ?? '', [Validators.maxLength(200)]],
      logoUrl: [data?.logoUrl ?? '']
    });
  }
  save(): void {
    if (this.form.valid) {
      const v = this.form.value;
      this.ref.close({
        name: v.name,
        shortName: v.shortName,
        city: v.city?.trim() || null,
        logoUrl: v.logoUrl?.trim() || null
      });
    }
  }
}

@Component({
  selector: 'app-university-users-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatTableModule, MatIconModule,
    MatSelectModule, MatSnackBarModule, MatAutocompleteModule
  ],
  template: `
    <h2 mat-dialog-title>Доступ: {{ data.name }}</h2>
    <mat-dialog-content>
      <table mat-table [dataSource]="users" class="full-table">
        <ng-container matColumnDef="username">
          <th mat-header-cell *matHeaderCellDef>Пользователь</th>
          <td mat-cell *matCellDef="let u">{{ u.username }}</td>
        </ng-container>
        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>Роль</th>
          <td mat-cell *matCellDef="let u">{{ u.universityRole }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let u">
            <button mat-icon-button color="warn" (click)="revoke(u)"><mat-icon>person_remove</mat-icon></button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="cols"></tr>
        <tr mat-row *matRowDef="let row; columns: cols;"></tr>
      </table>

      <div class="assign-form">
        <mat-form-field appearance="outline" class="grow">
          <mat-label>Поиск пользователя</mat-label>
          <input matInput
                 [formControl]="userSearch"
                 [matAutocomplete]="auto"
                 placeholder="Логин">
          <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayUser" (optionSelected)="selectUser($event.option.value)">
            <mat-option *ngFor="let u of suggestions$ | async" [value]="u">
              {{ u.username }} <small style="color: #888">({{ u.role }})</small>
            </mat-option>
          </mat-autocomplete>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Роль</mat-label>
          <mat-select [(ngModel)]="assignRole">
            <mat-option value="Admin">Администратор</mat-option>
            <mat-option value="Teacher">Преподаватель</mat-option>
          </mat-select>
        </mat-form-field>
        <button mat-raised-button color="primary" (click)="assign()" [disabled]="!selectedUserId">Добавить</button>
      </div>

      <div class="create-section">
        <h3 class="section-title">Создать администратора без e-mail</h3>
        <p class="section-hint">Аккаунт с логином и паролем (вход по логину). Сразу получит права администратора в этом университете.</p>
        <div class="assign-form">
          <mat-form-field appearance="outline" class="grow">
            <mat-label>Логин</mat-label>
            <input matInput [(ngModel)]="newUsername" autocomplete="off">
          </mat-form-field>
          <mat-form-field appearance="outline" class="grow">
            <mat-label>Пароль</mat-label>
            <input matInput [(ngModel)]="newPassword" type="password" autocomplete="new-password">
            <mat-hint>Минимум 6 символов</mat-hint>
          </mat-form-field>
          <button mat-raised-button color="primary" (click)="createAdmin()" [disabled]="!canCreate || creating">
            Создать
          </button>
        </div>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Закрыть</button>
    </mat-dialog-actions>
  `,
  styles: [
    '.full-table { width: 100%; min-width: 400px; }',
    '.assign-form { display: flex; gap: 8px; align-items: center; margin-top: 16px; flex-wrap: wrap; }',
    '.assign-form .grow { flex: 1; min-width: 200px; }',
    '.create-section { margin-top: 24px; border-top: 1px solid rgba(0,0,0,0.12); padding-top: 8px; }',
    '.section-title { margin: 8px 0 2px; font-size: 14px; }',
    '.section-hint { margin: 0; color: #888; font-size: 12px; }'
  ]
})
export class UniversityUsersDialogComponent implements OnInit {
  users: any[] = [];
  cols = ['username', 'role', 'actions'];
  userSearch = new FormControl<any>('');
  selectedUserId: string | null = null;
  assignRole = 'Teacher';
  suggestions$: Observable<{ id: string; username: string; role: string }[]> = of([]);
  newUsername = '';
  newPassword = '';
  creating = false;

  get canCreate(): boolean {
    return this.newUsername.trim().length >= 3 && this.newPassword.length >= 6;
  }

  constructor(
    private api: ApiService,
    private snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {}

  ngOnInit(): void {
    this.load();
    this.suggestions$ = this.userSearch.valueChanges.pipe(
      startWith(''),
      debounceTime(200),
      distinctUntilChanged(),
      switchMap(v => {
        const q = typeof v === 'string' ? v : v?.username;
        if (!q || q.length < 1) return of([]);
        return this.api.getUsers(q);
      })
    );
  }

  displayUser = (u: any): string => (u && typeof u === 'object') ? u.username : (u || '');

  selectUser(u: { id: string; username: string }): void {
    this.selectedUserId = u.id;
  }

  load(): void {
    this.api.getUniversityUsers(this.data.id).subscribe(u => this.users = u);
  }

  assign(): void {
    if (!this.selectedUserId) return;
    this.api.assignUniversityUser(this.data.id, this.selectedUserId, this.assignRole).subscribe({
      next: () => {
        this.load();
        this.userSearch.setValue('');
        this.selectedUserId = null;
        this.snackBar.open('Доступ предоставлен', 'OK', { duration: 2000 });
      },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  revoke(u: any): void {
    this.api.revokeUniversityUser(this.data.id, u.userId).subscribe({
      next: () => { this.load(); this.snackBar.open('Доступ отозван', 'OK', { duration: 2000 }); },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  createAdmin(): void {
    if (!this.canCreate || this.creating) return;
    this.creating = true;
    this.api.createUser({
      username: this.newUsername.trim(),
      password: this.newPassword,
      universityId: this.data.id,
      role: 'Admin'
    }).subscribe({
      next: () => {
        this.creating = false;
        this.newUsername = '';
        this.newPassword = '';
        this.load();
        this.snackBar.open('Администратор создан', 'OK', { duration: 2000 });
      },
      error: e => {
        this.creating = false;
        this.snackBar.open(e.error?.title || e.error?.message || 'Ошибка', 'OK', { duration: 4000 });
      }
    });
  }
}

@Component({
  selector: 'app-university-invitations-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule,
    MatTableModule, MatIconModule, MatSelectModule, MatSnackBarModule,
    MatChipsModule, MatTooltipModule
  ],
  template: `
    <h2 mat-dialog-title>Приглашения: {{ data.name }}</h2>
    <mat-dialog-content>
      <div class="invite-form">
        <mat-form-field appearance="outline">
          <mat-label>Роль</mat-label>
          <mat-select [(ngModel)]="role" (ngModelChange)="onRoleChange()">
            <mat-option value="Teacher">Преподаватель</mat-option>
            <mat-option value="Admin" *ngIf="auth.isSuperAdmin">Администратор</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="grow" *ngIf="role === 'Teacher'">
          <mat-label>Преподаватель</mat-label>
          <mat-select [(ngModel)]="teacherId" (ngModelChange)="onTeacherChange()">
            <mat-option *ngFor="let t of teachers" [value]="t.id">
              {{ t.displayName || (t.lastName + ' ' + t.firstName) }}<span *ngIf="t.email"> · {{ t.email }}</span>
            </mat-option>
          </mat-select>
          <mat-hint>Приглашение привяжет аккаунт к этой карточке</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline" class="grow">
          <mat-label>E-mail</mat-label>
          <input matInput [(ngModel)]="email" placeholder="user@example.com" type="email">
          <mat-hint *ngIf="role === 'Teacher'">Можно оставить — возьмём e-mail преподавателя</mat-hint>
        </mat-form-field>
        <button mat-raised-button color="primary" (click)="invite()" [disabled]="!canInvite">
          <mat-icon>send</mat-icon> Пригласить
        </button>
      </div>

      <table mat-table [dataSource]="invitations" class="full-table">
        <ng-container matColumnDef="email">
          <th mat-header-cell *matHeaderCellDef>E-mail</th>
          <td mat-cell *matCellDef="let i">{{ i.email }}</td>
        </ng-container>
        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>Роль</th>
          <td mat-cell *matCellDef="let i">{{ i.universityRole }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Статус</th>
          <td mat-cell *matCellDef="let i">
            <mat-chip *ngIf="i.isConsumed" color="primary" highlighted>Принято</mat-chip>
            <mat-chip *ngIf="!i.isConsumed && isExpired(i)" color="warn" highlighted>Истекло</mat-chip>
            <mat-chip *ngIf="!i.isConsumed && !isExpired(i)">Ожидает</mat-chip>
          </td>
        </ng-container>
        <ng-container matColumnDef="expires">
          <th mat-header-cell *matHeaderCellDef>Действует до</th>
          <td mat-cell *matCellDef="let i">{{ i.expiresAt | date: 'dd.MM.yyyy HH:mm' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let i">
            <button mat-icon-button color="warn" *ngIf="!i.isConsumed"
                    (click)="cancel(i)" matTooltip="Отменить">
              <mat-icon>cancel</mat-icon>
            </button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="cols"></tr>
        <tr mat-row *matRowDef="let row; columns: cols;"></tr>
      </table>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Закрыть</button>
    </mat-dialog-actions>
  `,
  styles: [
    '.full-table { width: 100%; min-width: 560px; }',
    '.invite-form { display: flex; gap: 8px; align-items: center; margin-bottom: 16px; flex-wrap: wrap; }',
    '.invite-form .grow { flex: 1; min-width: 220px; }'
  ]
})
export class UniversityInvitationsDialogComponent implements OnInit {
  invitations: any[] = [];
  teachers: Teacher[] = [];
  cols = ['email', 'role', 'status', 'expires', 'actions'];
  email = '';
  role: 'Admin' | 'Teacher' = 'Teacher';
  teacherId: string | null = null;

  constructor(
    private api: ApiService,
    public auth: AuthService,
    private snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {}

  ngOnInit(): void {
    this.load();
    this.api.getTeachersForUniversity(this.data.id).subscribe(list => this.teachers = list);
  }

  load(): void {
    this.api.listInvitations(this.data.id).subscribe(list => this.invitations = list);
  }

  isExpired(i: any): boolean {
    return new Date(i.expiresAt) < new Date();
  }

  // Teacher invites must be bound to a teacher card (the backend enforces this too); admin invites
  // just need an e-mail.
  get canInvite(): boolean {
    if (this.role === 'Teacher') return !!this.teacherId && (!!this.email || this.teacherHasEmail);
    return !!this.email;
  }

  private get teacherHasEmail(): boolean {
    return !!this.teachers.find(t => t.id === this.teacherId)?.email;
  }

  onRoleChange(): void {
    if (this.role === 'Admin') this.teacherId = null;
  }

  onTeacherChange(): void {
    // Pre-fill the e-mail from the teacher card so the admin doesn't have to retype it.
    const t = this.teachers.find(x => x.id === this.teacherId);
    if (t?.email && !this.email) this.email = t.email;
  }

  invite(): void {
    if (!this.canInvite) return;
    const teacherId = this.role === 'Teacher' ? (this.teacherId ?? undefined) : undefined;
    this.api.createInvitation(this.data.id, this.email, this.role, teacherId).subscribe({
      next: () => {
        this.email = '';
        this.teacherId = null;
        this.load();
        this.snackBar.open('Приглашение отправлено', 'OK', { duration: 4000 });
      },
      error: e => this.snackBar.open(e.error?.title || e.error?.message || 'Ошибка', 'OK', { duration: 4000 })
    });
  }

  cancel(i: any): void {
    this.api.cancelInvitation(i.id).subscribe({
      next: () => { this.load(); this.snackBar.open('Приглашение отменено', 'OK', { duration: 2000 }); },
      error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
    });
  }
}
