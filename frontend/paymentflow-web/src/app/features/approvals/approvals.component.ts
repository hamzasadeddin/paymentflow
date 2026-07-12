import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { ApprovalService } from '../../core/services/approval.service';
import { AuthService } from '../../core/services/auth.service';
import { ApprovalQueueItem } from '../../core/models/approval.models';
import { AppRoles, ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/confirm-dialog.component';

type Kind = 'payment' | 'beneficiary';

@Component({
  selector: 'pf-approvals',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatSnackBarModule, MatDialogModule, StatusChipComponent],
  template: `
    <div class="page-header">
      <div>
        <h2>Approvals</h2>
        <p class="pf-muted">Maker-checker queue. You cannot approve items you created; larger payments need two approvers.</p>
      </div>
    </div>

    <div class="pf-card">
      @switch (state()) {
        @case ('loading') {
          <div class="skeleton-table">
            @for (row of [1,2,3,4]; track row) { <div class="skeleton-row"></div> }
          </div>
        }
        @case ('error') {
          <div class="state-block error">
            <span class="material-symbols-outlined">error</span>
            <p>{{ error()?.title ?? 'Could not load the approvals queue.' }}</p>
            <button mat-stroked-button (click)="load()">Retry</button>
          </div>
        }
        @case ('empty') {
          <div class="state-block">
            <span class="material-symbols-outlined">task_alt</span>
            <p>Nothing is awaiting approval right now.</p>
          </div>
        }
        @default {
          @if (payments().length > 0) {
            <h3 class="section-title">Payments</h3>
            <table class="data-table">
              <thead>
                <tr>
                  <th>Reference</th>
                  <th>Details</th>
                  <th class="right">Amount</th>
                  <th>Created by</th>
                  <th>Progress</th>
                  @if (canApprove()) { <th class="right">Actions</th> }
                </tr>
              </thead>
              <tbody>
                @for (item of payments(); track item.subjectId) {
                  <tr>
                    <td class="primary mono">{{ item.reference }}</td>
                    <td>{{ item.title }}</td>
                    <td class="right mono">{{ item.amount | number:'1.2-2' }} {{ item.currency }}</td>
                    <td class="maker">{{ makerLabel(item) }}</td>
                    <td>
                      <pf-status-chip
                        [label]="progressLabel(item)"
                        [tone]="item.requiredApprovals > 1 ? 'info' : 'warning'" />
                    </td>
                    @if (canApprove()) {
                      <td class="right actions">
                        <button mat-stroked-button color="primary" (click)="review('payment', item, 'approve')">Approve</button>
                        <button mat-stroked-button color="warn" (click)="review('payment', item, 'reject')">Reject</button>
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          }

          @if (beneficiaries().length > 0) {
            <h3 class="section-title">Beneficiaries</h3>
            <table class="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Currency</th>
                  <th>Created by</th>
                  <th>Progress</th>
                  @if (canApprove()) { <th class="right">Actions</th> }
                </tr>
              </thead>
              <tbody>
                @for (item of beneficiaries(); track item.subjectId) {
                  <tr>
                    <td class="primary">{{ item.reference }}</td>
                    <td>{{ item.currency }}</td>
                    <td class="maker">{{ makerLabel(item) }}</td>
                    <td><pf-status-chip [label]="progressLabel(item)" tone="warning" /></td>
                    @if (canApprove()) {
                      <td class="right actions">
                        <button mat-stroked-button color="primary" (click)="review('beneficiary', item, 'approve')">Approve</button>
                        <button mat-stroked-button color="warn" (click)="review('beneficiary', item, 'reject')">Reject</button>
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          }
        }
      }
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 20px; }
    .page-header h2 { margin: 0 0 4px; }
    .page-header p { margin: 0; }
    .section-title { margin: 8px 0 4px; font-size: 13px; text-transform: uppercase; letter-spacing: 0.4px; color: var(--pf-text-muted); }
    .data-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
    .data-table th {
      text-align: left; font-size: 12px; font-weight: 600; text-transform: uppercase;
      letter-spacing: 0.4px; color: var(--pf-text-muted); padding: 10px 12px;
      border-bottom: 1px solid var(--pf-border);
    }
    .data-table th.right, .data-table td.right { text-align: right; }
    .data-table td { padding: 12px; border-bottom: 1px solid var(--pf-border); }
    .data-table .primary { font-weight: 500; }
    .data-table .maker { color: var(--pf-text-muted); font-size: 13px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 13px; }
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
export class ApprovalsComponent {
  private readonly service = inject(ApprovalService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly payments = signal<ApprovalQueueItem[]>([]);
  readonly beneficiaries = signal<ApprovalQueueItem[]>([]);

  readonly canApprove = computed(() =>
    this.auth.hasAnyRole([AppRoles.Administrator, AppRoles.PaymentApprover]));

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.error.set(null);
    this.service.queue().subscribe({
      next: queue => {
        this.payments.set(queue.payments);
        this.beneficiaries.set(queue.beneficiaries);
        const empty = queue.payments.length === 0 && queue.beneficiaries.length === 0;
        this.state.set(empty ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  progressLabel(item: ApprovalQueueItem): string {
    if (item.requiredApprovals <= 1) return 'Pending approval';
    return `${item.approvalsReceived} / ${item.requiredApprovals} approved`;
  }

  /** The maker's email if known, else a short id, else a dash. */
  makerLabel(item: ApprovalQueueItem): string {
    return item.makerEmail ?? item.makerUserId ?? '—';
  }

  review(kind: Kind, item: ApprovalQueueItem, action: 'approve' | 'reject'): void {
    const isApprove = action === 'approve';
    this.dialog.open(ConfirmDialogComponent, {
      width: '440px',
      data: {
        title: isApprove ? 'Approve' : 'Reject',
        message: `${isApprove ? 'Approve' : 'Reject'} "${item.reference}"?`,
        confirmLabel: isApprove ? 'Approve' : 'Reject',
        tone: isApprove ? 'primary' : 'warn',
        withNotes: true
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      this.dispatch(kind, item.subjectId, action, result.notes).subscribe({
        next: () => { this.toast(`${item.reference} ${isApprove ? 'approved' : 'rejected'}`); this.load(); },
        error: (err: ApiError) => this.toast(err.title, true)
      });
    });
  }

  private dispatch(kind: Kind, id: string, action: 'approve' | 'reject', notes?: string): Observable<unknown> {
    if (kind === 'payment') {
      return action === 'approve'
        ? this.service.approvePayment(id, notes)
        : this.service.rejectPayment(id, notes);
    }
    return action === 'approve'
      ? this.service.approveBeneficiary(id, notes)
      : this.service.rejectBeneficiary(id, notes);
  }

  private toast(message: string, isError = false): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 4000,
      panelClass: isError ? 'pf-snack-error' : 'pf-snack-ok'
    });
  }
}
