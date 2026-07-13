import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { ComplianceService } from '../../core/services/compliance.service';
import { AuthService } from '../../core/services/auth.service';
import { ComplianceCase } from '../../core/models/compliance.models';
import { AppRoles, ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/confirm-dialog.component';
import { complianceStatusDisplay, complianceCategoryLabel } from '../../shared/status-maps';

@Component({
  selector: 'pf-compliance',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatSnackBarModule, MatDialogModule, StatusChipComponent],
  template: `
    <div class="page-header">
      <div>
        <h2>Compliance</h2>
        <p class="pf-muted">Payments held for sanctions/limit review. A hold blocks settlement until an officer clears it.</p>
      </div>
    </div>

    <div class="pf-card">
      @switch (state()) {
        @case ('loading') {
          <div class="skeleton-table">
            @for (row of [1,2,3]; track row) { <div class="skeleton-row"></div> }
          </div>
        }
        @case ('error') {
          <div class="state-block error">
            <span class="material-symbols-outlined">error</span>
            <p>{{ error()?.title ?? 'Could not load the compliance queue.' }}</p>
            <button mat-stroked-button (click)="load()">Retry</button>
          </div>
        }
        @case ('empty') {
          <div class="state-block">
            <span class="material-symbols-outlined">verified_user</span>
            <p>No open compliance holds. Everything is clear.</p>
          </div>
        }
        @case ('loaded') {
          <table class="data-table">
            <thead>
              <tr>
                <th>Payment</th>
                <th>Category</th>
                <th>Reason</th>
                <th class="right">Amount</th>
                <th>Source account</th>
                <th>Status</th>
                @if (canManage()) { <th class="right">Actions</th> }
              </tr>
            </thead>
            <tbody>
              @for (item of cases(); track item.id) {
                <tr>
                  <td class="primary">{{ item.paymentReference }}</td>
                  <td>{{ categoryLabel(item.category) }}</td>
                  <td class="reason">{{ item.reason }}</td>
                  <td class="right mono">{{ item.amount | number:'1.2-2' }} {{ item.currency }}</td>
                  <td class="mono">
                    {{ accountDisplay(item) }}
                    @if (canReveal()) {
                      <button type="button" class="reveal" (click)="reveal(item)"
                              [attr.aria-label]="'Reveal account number'">
                        <span class="material-symbols-outlined">{{ isRevealed(item) ? 'visibility_off' : 'visibility' }}</span>
                      </button>
                    }
                  </td>
                  <td><pf-status-chip [label]="statusLabel(item)" [tone]="statusTone(item)" /></td>
                  @if (canManage()) {
                    <td class="right actions">
                      <button mat-stroked-button color="primary" (click)="review(item, 'clear')">Clear</button>
                      <button mat-stroked-button color="warn" (click)="review(item, 'reject')">Reject</button>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        }
      }
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 20px; }
    .page-header h2 { margin: 0 0 4px; }
    .page-header p { margin: 0; }
    .data-table { width: 100%; border-collapse: collapse; }
    .data-table th {
      text-align: left; font-size: 12px; font-weight: 600; text-transform: uppercase;
      letter-spacing: 0.4px; color: var(--pf-text-muted); padding: 10px 12px;
      border-bottom: 1px solid var(--pf-border);
    }
    .data-table th.right, .data-table td.right { text-align: right; }
    .data-table td { padding: 12px; border-bottom: 1px solid var(--pf-border); vertical-align: top; }
    .data-table .primary { font-weight: 500; }
    .data-table .reason { color: var(--pf-text-muted); font-size: 13px; max-width: 320px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 13px; }
    .reveal { border: none; background: none; cursor: pointer; color: var(--pf-text-muted); padding: 0 0 0 4px; vertical-align: middle; }
    .reveal .material-symbols-outlined { font-size: 16px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .skeleton-table { display: flex; flex-direction: column; gap: 8px; padding: 12px 0; }
    .skeleton-row {
      height: 44px; border-radius: 8px;
      background: linear-gradient(90deg, #eef1f5 25%, #e2e7ee 37%, #eef1f5 63%);
      background-size: 400% 100%; animation: shimmer 1.3s ease-in-out infinite;
    }
    @keyframes shimmer { 0% { background-position: 100% 0; } 100% { background-position: 0 0; } }
    .state-block {
      display: flex; flex-direction: column; align-items: center; gap: 10px;
      padding: 48px 16px; text-align: center; color: var(--pf-text-muted);
    }
    .state-block .material-symbols-outlined { font-size: 40px; opacity: 0.6; }
    .state-block.error .material-symbols-outlined { color: var(--pf-danger); opacity: 1; }
  `]
})
export class ComplianceComponent {
  private readonly service = inject(ComplianceService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly cases = signal<ComplianceCase[]>([]);
  readonly revealed = signal<Record<string, string>>({});

  readonly canManage = computed(() =>
    this.auth.hasAnyRole([AppRoles.Administrator, AppRoles.ComplianceOfficer]));
  // Reveal is gated on the same roles server-side (CanRevealAccountNumbers).
  readonly canReveal = this.canManage;

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.error.set(null);
    this.revealed.set({});
    this.service.queue().subscribe({
      next: rows => {
        this.cases.set(rows);
        this.state.set(rows.length === 0 ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  categoryLabel = complianceCategoryLabel;
  statusLabel(item: ComplianceCase): string { return complianceStatusDisplay(item.status).label; }
  statusTone(item: ComplianceCase): 'neutral' | 'success' | 'warning' | 'danger' | 'info' {
    return complianceStatusDisplay(item.status).tone;
  }

  isRevealed(item: ComplianceCase): boolean {
    return Object.prototype.hasOwnProperty.call(this.revealed(), item.sourceAccountId);
  }

  accountDisplay(item: ComplianceCase): string {
    return this.isRevealed(item) ? this.revealed()[item.sourceAccountId] : item.sourceAccountMaskedNumber;
  }

  reveal(item: ComplianceCase): void {
    // Toggle off if already revealed.
    if (this.revealed()[item.sourceAccountId]) {
      const next = { ...this.revealed() };
      delete next[item.sourceAccountId];
      this.revealed.set(next);
      return;
    }
    this.service.revealAccountNumber(item.sourceAccountId).subscribe({
      next: full => this.revealed.set({ ...this.revealed(), [item.sourceAccountId]: full }),
      error: (err: ApiError) => this.toast(err.title, true)
    });
  }

  review(item: ComplianceCase, action: 'clear' | 'reject'): void {
    const isClear = action === 'clear';
    this.dialog.open(ConfirmDialogComponent, {
      width: '460px',
      data: {
        title: isClear ? 'Clear compliance hold' : 'Reject compliance hold',
        message: isClear
          ? `Clear the hold on "${item.paymentReference}"? The payment will be free to settle.`
          : `Reject the hold on "${item.paymentReference}"? The payment stays blocked and should be cancelled.`,
        confirmLabel: isClear ? 'Clear' : 'Reject',
        tone: isClear ? 'primary' : 'warn',
        withNotes: true
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      this.dispatch(item.id, action, result.notes).subscribe({
        next: () => { this.toast(`${item.paymentReference} hold ${isClear ? 'cleared' : 'rejected'}`); this.load(); },
        error: (err: ApiError) => this.toast(err.title, true)
      });
    });
  }

  private dispatch(id: string, action: 'clear' | 'reject', notes?: string): Observable<unknown> {
    return action === 'clear' ? this.service.clear(id, notes) : this.service.reject(id, notes);
  }

  private toast(message: string, isError = false): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 4000,
      panelClass: isError ? 'pf-snack-error' : 'pf-snack-ok'
    });
  }
}
