import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { AdminService } from '../../core/services/admin.service';
import { AdminUser, Rules } from '../../core/models/admin.models';
import { AppRoles, ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/confirm-dialog.component';

const ALL_ROLES: string[] = Object.values(AppRoles);

@Component({
  selector: 'pf-admin',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatSnackBarModule, MatDialogModule, StatusChipComponent],
  template: `
    <div class="page-header">
      <div>
        <h2>Administration</h2>
        <p class="pf-muted">Manage users and roles, and tune the platform's business rules.</p>
      </div>
    </div>

    <div class="tabs">
      <button class="tab" [class.active]="tab() === 'users'" (click)="tab.set('users')">Users &amp; roles</button>
      <button class="tab" [class.active]="tab() === 'rules'" (click)="showRules()">Rules configuration</button>
    </div>

    @if (tab() === 'users') {
      <div class="pf-card">
        <div class="card-head">
          <h3>Users</h3>
          <button mat-flat-button color="primary" (click)="showCreate.set(!showCreate())">
            {{ showCreate() ? 'Cancel' : 'New user' }}
          </button>
        </div>

        @if (showCreate()) {
          <div class="create-form">
            <div class="grid">
              <label>Email <input type="email" [(ngModel)]="newUser.email" placeholder="name@paymentflow.local" /></label>
              <label>Display name <input type="text" [(ngModel)]="newUser.displayName" /></label>
              <label>Temp. password <input type="text" [(ngModel)]="newUser.password" placeholder="min 10 chars" /></label>
            </div>
            <div class="roles-pick">
              <span class="pf-muted">Roles:</span>
              @for (r of allRoles; track r) {
                <label class="chk"><input type="checkbox" [checked]="newUser.roles.includes(r)"
                  (change)="toggleNewRole(r)" /> {{ r }}</label>
              }
            </div>
            <div class="form-actions">
              <button mat-flat-button color="primary" [disabled]="creating()" (click)="create()">
                {{ creating() ? 'Creating…' : 'Create user' }}
              </button>
            </div>
          </div>
        }

        @switch (usersState()) {
          @case ('loading') {
            <div class="skeleton-table">@for (r of [1,2,3]; track r) { <div class="skeleton-row"></div> }</div>
          }
          @case ('error') {
            <div class="state-block error">
              <span class="material-symbols-outlined">error</span>
              <p>{{ usersError()?.title ?? 'Could not load users.' }}</p>
              <button mat-stroked-button (click)="loadUsers()">Retry</button>
            </div>
          }
          @case ('loaded') {
            <table class="data-table">
              <thead>
                <tr><th>Email</th><th>Name</th><th>Roles</th><th>Status</th><th>Created</th><th class="right">Actions</th></tr>
              </thead>
              <tbody>
                @for (u of users(); track u.id) {
                  <tr>
                    <td class="primary">{{ u.email }}</td>
                    <td>{{ u.displayName }}</td>
                    <td class="roles-cell">
                      @for (r of u.roles; track r) { <span class="role-chip">{{ r }}</span> }
                      @if (u.roles.length === 0) { <span class="pf-muted">—</span> }
                    </td>
                    <td><pf-status-chip [label]="u.isActive ? 'Active' : 'Inactive'"
                                        [tone]="u.isActive ? 'success' : 'neutral'" /></td>
                    <td class="nowrap">{{ u.createdAtUtc | date:'mediumDate' }}</td>
                    <td class="right actions">
                      <button mat-stroked-button (click)="toggleEditRoles(u)">Roles</button>
                      <button mat-stroked-button (click)="resetPassword(u)">Password</button>
                      @if (u.isActive) {
                        <button mat-stroked-button color="warn" (click)="setActive(u, false)">Deactivate</button>
                      } @else {
                        <button mat-stroked-button color="primary" (click)="setActive(u, true)">Activate</button>
                      }
                    </td>
                  </tr>
                  @if (editingRolesFor() === u.id) {
                    <tr class="edit-row">
                      <td colspan="6">
                        <div class="roles-pick">
                          <span class="pf-muted">Set roles:</span>
                          @for (r of allRoles; track r) {
                            <label class="chk"><input type="checkbox" [checked]="roleDraft().includes(r)"
                              (change)="toggleDraftRole(r)" /> {{ r }}</label>
                          }
                          <button mat-flat-button color="primary" (click)="saveRoles(u)">Save roles</button>
                        </div>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          }
        }
      </div>
    }

    @if (tab() === 'rules') {
      @switch (rulesState()) {
        @case ('loading') {
          <div class="pf-card"><div class="skeleton-table">@for (r of [1,2,3,4]; track r) { <div class="skeleton-row"></div> }</div></div>
        }
        @case ('error') {
          <div class="pf-card state-block error">
            <span class="material-symbols-outlined">error</span>
            <p>{{ rulesError()?.title ?? 'Could not load rules.' }}</p>
            <button mat-stroked-button (click)="loadRules()">Retry</button>
          </div>
        }
        @case ('loaded') {
          @if (rules(); as r) {
            <!-- Approval -->
            <div class="pf-card rule-card">
              <div class="card-head">
                <h3>Approval thresholds</h3>
                <span class="badge" [class.on]="r.approval.isOverridden">
                  {{ r.approval.isOverridden ? 'Overridden' : 'Using defaults' }}</span>
              </div>
              <div class="grid">
                <label>Auto-approve below <input type="number" [(ngModel)]="r.approval.values.autoApproveBelow" /></label>
                <label>Dual approval at/above <input type="number" [(ngModel)]="r.approval.values.dualApprovalAtOrAbove" /></label>
              </div>
              <div class="form-actions">
                <button mat-flat-button color="primary" (click)="saveApproval(r)">Save</button>
              </div>
            </div>

            <!-- Screening -->
            <div class="pf-card rule-card">
              <div class="card-head">
                <h3>Compliance screening</h3>
                <span class="badge" [class.on]="r.screening.isOverridden">
                  {{ r.screening.isOverridden ? 'Overridden' : 'Using defaults' }}</span>
              </div>
              <div class="grid">
                <label class="chk-inline"><input type="checkbox" [(ngModel)]="r.screening.values.autoScreenOnSubmit" /> Auto-screen on submit</label>
                <label>Single-payment review limit <input type="number" [(ngModel)]="r.screening.values.singlePaymentReviewLimit" /></label>
                <label>Watchlist names (comma-separated)
                  <input type="text" [ngModel]="joinList(r.screening.values.watchlistBeneficiaryNames)"
                         (ngModelChange)="r.screening.values.watchlistBeneficiaryNames = splitList($event)" /></label>
                <label>Watchlist countries (comma-separated)
                  <input type="text" [ngModel]="joinList(r.screening.values.watchlistCountryCodes)"
                         (ngModelChange)="r.screening.values.watchlistCountryCodes = splitList($event)" /></label>
              </div>
              <div class="form-actions">
                <button mat-flat-button color="primary" (click)="saveScreening(r)">Save</button>
              </div>
            </div>

            <!-- Reconciliation -->
            <div class="pf-card rule-card">
              <div class="card-head">
                <h3>Reconciliation drift</h3>
                <span class="badge" [class.on]="r.reconciliation.isOverridden">
                  {{ r.reconciliation.isOverridden ? 'Overridden' : 'Using defaults' }}</span>
              </div>
              <div class="grid">
                <label class="chk-inline"><input type="checkbox" [(ngModel)]="r.reconciliation.values.introduceSyntheticBreaks" /> Introduce synthetic breaks</label>
                <label>Drop reference ending in <input type="text" maxlength="1" [(ngModel)]="r.reconciliation.values.dropReferenceEndingIn" /></label>
                <label>Phantom amount <input type="number" [(ngModel)]="r.reconciliation.values.phantomAmount" /></label>
                <label>Amount drift (cents) <input type="number" [(ngModel)]="r.reconciliation.values.amountDriftMinorUnits" /></label>
              </div>
              <div class="form-actions">
                <button mat-flat-button color="primary" (click)="saveReconciliation(r)">Save</button>
              </div>
            </div>

            <!-- Processing -->
            <div class="pf-card rule-card">
              <div class="card-head">
                <h3>Settlement processing</h3>
                <span class="badge" [class.on]="r.processing.isOverridden">
                  {{ r.processing.isOverridden ? 'Overridden' : 'Using defaults' }}</span>
              </div>
              <p class="pf-muted note">The failure sentinel applies immediately. Worker cadence &amp; enable changes take effect on the next service restart.</p>
              <div class="grid">
                <label class="chk-inline"><input type="checkbox" [(ngModel)]="r.processing.values.autoProcessEnabled" /> Auto-process enabled</label>
                <label>Polling interval (s) <input type="number" [(ngModel)]="r.processing.values.pollingIntervalSeconds" /></label>
                <label>Batch size <input type="number" [(ngModel)]="r.processing.values.batchSize" /></label>
                <label>Fail on cents (0–99) <input type="number" [(ngModel)]="r.processing.values.failOnCents" /></label>
                <label>Latency min (ms) <input type="number" [(ngModel)]="r.processing.values.simulatedLatencyMsMin" /></label>
                <label>Latency max (ms) <input type="number" [(ngModel)]="r.processing.values.simulatedLatencyMsMax" /></label>
              </div>
              <div class="form-actions">
                <button mat-flat-button color="primary" (click)="saveProcessing(r)">Save</button>
              </div>
            </div>
          }
        }
      }
    }
  `,
  styles: [`
    .page-header { margin-bottom: 16px; }
    .page-header h2 { margin: 0 0 4px; }
    .page-header p { margin: 0; }
    .tabs { display: flex; gap: 4px; margin-bottom: 16px; border-bottom: 1px solid var(--pf-border); }
    .tab {
      border: none; background: none; padding: 10px 16px; cursor: pointer; font-size: 14px;
      color: var(--pf-text-muted); border-bottom: 2px solid transparent; margin-bottom: -1px;
    }
    .tab.active { color: var(--pf-accent); border-bottom-color: var(--pf-accent); font-weight: 600; }
    .card-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .card-head h3 { margin: 0; font-size: 15px; }
    .rule-card { margin-bottom: 16px; }
    .rule-card .note { margin: -4px 0 12px; font-size: 12.5px; }
    .badge { font-size: 11.5px; font-weight: 600; padding: 2px 10px; border-radius: 999px; background: #eceff3; color: #4b5a6b; }
    .badge.on { background: var(--pf-accent-soft); color: var(--pf-accent); }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px 20px; }
    .grid label, .create-form label { display: flex; flex-direction: column; gap: 4px; font-size: 12.5px; color: var(--pf-text-muted); }
    .grid input, .create-form input {
      height: 36px; padding: 0 10px; border: 1px solid var(--pf-border); border-radius: 8px;
      background: var(--pf-surface); color: var(--pf-text); font-size: 13px;
    }
    .chk-inline { flex-direction: row !important; align-items: center; gap: 8px; }
    .chk-inline input { height: auto; }
    .form-actions { margin-top: 14px; }
    .create-form { padding: 12px 0 16px; border-bottom: 1px solid var(--pf-border); margin-bottom: 12px; }
    .create-form .grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    .roles-pick { display: flex; flex-wrap: wrap; align-items: center; gap: 14px; margin-top: 12px; }
    .chk { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--pf-text); }
    .data-table { width: 100%; border-collapse: collapse; }
    .data-table th {
      text-align: left; font-size: 12px; font-weight: 600; text-transform: uppercase;
      letter-spacing: 0.4px; color: var(--pf-text-muted); padding: 10px 12px; border-bottom: 1px solid var(--pf-border);
    }
    .data-table th.right, .data-table td.right { text-align: right; }
    .data-table td { padding: 12px; border-bottom: 1px solid var(--pf-border); }
    .data-table .primary { font-weight: 500; }
    .nowrap { white-space: nowrap; }
    .roles-cell { display: flex; flex-wrap: wrap; gap: 4px; }
    .role-chip { background: var(--pf-accent-soft); color: var(--pf-accent); border-radius: 999px; padding: 2px 8px; font-size: 11.5px; font-weight: 600; }
    .actions { display: flex; gap: 6px; justify-content: flex-end; flex-wrap: wrap; }
    .edit-row td { background: var(--pf-accent-soft); }
    .skeleton-table { display: flex; flex-direction: column; gap: 8px; padding: 12px 0; }
    .skeleton-row { height: 44px; border-radius: 8px;
      background: linear-gradient(90deg, #eef1f5 25%, #e2e7ee 37%, #eef1f5 63%);
      background-size: 400% 100%; animation: shimmer 1.3s ease-in-out infinite; }
    @keyframes shimmer { 0% { background-position: 100% 0; } 100% { background-position: 0 0; } }
    .state-block { display: flex; flex-direction: column; align-items: center; gap: 10px; padding: 48px 16px; text-align: center; color: var(--pf-text-muted); }
    .state-block .material-symbols-outlined { font-size: 40px; opacity: 0.6; }
    .state-block.error .material-symbols-outlined { color: var(--pf-danger); opacity: 1; }
  `]
})
export class AdminComponent {
  private readonly service = inject(AdminService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly allRoles = ALL_ROLES;
  readonly tab = signal<'users' | 'rules'>('users');

  // Users
  readonly usersState = signal<LoadState>('idle');
  readonly usersError = signal<ApiError | null>(null);
  readonly users = signal<AdminUser[]>([]);
  readonly showCreate = signal(false);
  readonly creating = signal(false);
  newUser = { email: '', displayName: '', password: '', roles: [] as string[] };

  readonly editingRolesFor = signal<string | null>(null);
  readonly roleDraft = signal<string[]>([]);

  // Rules
  readonly rulesState = signal<LoadState>('idle');
  readonly rulesError = signal<ApiError | null>(null);
  readonly rules = signal<Rules | null>(null);

  constructor() {
    this.loadUsers();
  }

  // ---------- Users ----------

  loadUsers(): void {
    this.usersState.set('loading');
    this.usersError.set(null);
    this.service.users({ page: 1, pageSize: 100 }).subscribe({
      next: result => { this.users.set(result.items); this.usersState.set('loaded'); },
      error: (err: ApiError) => { this.usersError.set(err); this.usersState.set('error'); }
    });
  }

  toggleNewRole(role: string): void {
    this.newUser.roles = this.newUser.roles.includes(role)
      ? this.newUser.roles.filter(r => r !== role)
      : [...this.newUser.roles, role];
  }

  create(): void {
    this.creating.set(true);
    this.service.createUser(this.newUser).subscribe({
      next: () => {
        this.toast(`Created ${this.newUser.email}`);
        this.newUser = { email: '', displayName: '', password: '', roles: [] };
        this.showCreate.set(false);
        this.creating.set(false);
        this.loadUsers();
      },
      error: (err: ApiError) => { this.creating.set(false); this.toast(err.title, true); }
    });
  }

  setActive(user: AdminUser, isActive: boolean): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '440px',
      data: {
        title: isActive ? 'Activate user' : 'Deactivate user',
        message: `${isActive ? 'Activate' : 'Deactivate'} ${user.email}?`,
        confirmLabel: isActive ? 'Activate' : 'Deactivate',
        tone: isActive ? 'primary' : 'warn'
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      this.service.setActive(user.id, isActive).subscribe({
        next: () => { this.toast(`${user.email} ${isActive ? 'activated' : 'deactivated'}`); this.loadUsers(); },
        error: (err: ApiError) => this.toast(err.title, true)
      });
    });
  }

  toggleEditRoles(user: AdminUser): void {
    if (this.editingRolesFor() === user.id) {
      this.editingRolesFor.set(null);
      return;
    }
    this.roleDraft.set([...user.roles]);
    this.editingRolesFor.set(user.id);
  }

  toggleDraftRole(role: string): void {
    const draft = this.roleDraft();
    this.roleDraft.set(draft.includes(role) ? draft.filter(r => r !== role) : [...draft, role]);
  }

  saveRoles(user: AdminUser): void {
    this.service.setRoles(user.id, this.roleDraft()).subscribe({
      next: () => { this.toast(`Roles updated for ${user.email}`); this.editingRolesFor.set(null); this.loadUsers(); },
      error: (err: ApiError) => this.toast(err.title, true)
    });
  }

  resetPassword(user: AdminUser): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '460px',
      data: {
        title: 'Reset password',
        message: `Enter a new temporary password for ${user.email} (min 10 characters).`,
        confirmLabel: 'Reset password',
        tone: 'primary',
        withNotes: true
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed || !result.notes) return;
      this.service.resetPassword(user.id, result.notes).subscribe({
        next: () => this.toast(`Password reset for ${user.email}`),
        error: (err: ApiError) => this.toast(err.title, true)
      });
    });
  }

  // ---------- Rules ----------

  showRules(): void {
    this.tab.set('rules');
    if (this.rules() === null) this.loadRules();
  }

  loadRules(): void {
    this.rulesState.set('loading');
    this.rulesError.set(null);
    this.service.rules().subscribe({
      next: rules => { this.rules.set(rules); this.rulesState.set('loaded'); },
      error: (err: ApiError) => { this.rulesError.set(err); this.rulesState.set('error'); }
    });
  }

  joinList(list: string[]): string { return (list ?? []).join(', '); }
  splitList(value: string): string[] {
    return value.split(',').map(s => s.trim()).filter(s => s.length > 0);
  }

  saveApproval(r: Rules): void {
    this.service.updateApproval({ ...r.approval.values, rowVersion: r.approval.rowVersion })
      .subscribe({ next: set => { r.approval = set; this.toast('Approval rules saved'); },
        error: (err: ApiError) => this.toast(err.title, true) });
  }

  saveScreening(r: Rules): void {
    this.service.updateScreening({ ...r.screening.values, rowVersion: r.screening.rowVersion })
      .subscribe({ next: set => { r.screening = set; this.toast('Screening rules saved'); },
        error: (err: ApiError) => this.toast(err.title, true) });
  }

  saveReconciliation(r: Rules): void {
    this.service.updateReconciliation({ ...r.reconciliation.values, rowVersion: r.reconciliation.rowVersion })
      .subscribe({ next: set => { r.reconciliation = set; this.toast('Reconciliation rules saved'); },
        error: (err: ApiError) => this.toast(err.title, true) });
  }

  saveProcessing(r: Rules): void {
    this.service.updateProcessing({ ...r.processing.values, rowVersion: r.processing.rowVersion })
      .subscribe({ next: set => { r.processing = set; this.toast('Processing rules saved'); },
        error: (err: ApiError) => this.toast(err.title, true) });
  }

  private toast(message: string, isError = false): void {
    this.snackBar.open(message, 'Dismiss', { duration: 4000, panelClass: isError ? 'pf-snack-error' : 'pf-snack-ok' });
  }
}
