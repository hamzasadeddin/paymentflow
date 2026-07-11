import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PaymentService } from '../../core/services/payment.service';
import { AuthService } from '../../core/services/auth.service';
import { Payment, PaymentStatus } from '../../core/models/payment.models';
import { AppRoles, ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { MaskedValueComponent } from '../../shared/masked-value.component';
import { paymentStatusDisplay } from '../../shared/status-maps';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/confirm-dialog.component';

@Component({
  selector: 'pf-payments',
  standalone: true,
  imports: [
    FormsModule, RouterLink, DecimalPipe, MatButtonModule, MatFormFieldModule, MatInputModule, MatMenuModule,
    MatSelectModule, MatSnackBarModule, MatDialogModule, StatusChipComponent, MaskedValueComponent
  ],
  templateUrl: './payments.component.html',
  styleUrl: './payments.component.scss'
})
export class PaymentsComponent {
  private readonly service = inject(PaymentService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly payments = signal<Payment[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 10;
  readonly search = signal('');
  readonly statusFilter = signal<PaymentStatus | ''>('');

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly statusDisplay = paymentStatusDisplay;
  readonly PaymentStatus = PaymentStatus;

  readonly canManage = computed(() =>
    this.auth.hasAnyRole([AppRoles.Administrator, AppRoles.OperationsAnalyst]));
  readonly canApprove = computed(() =>
    this.auth.hasAnyRole([AppRoles.Administrator, AppRoles.PaymentApprover]));

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.error.set(null);
    this.service.list({
      page: this.page(),
      pageSize: this.pageSize,
      search: this.search() || undefined,
      status: this.statusFilter() || undefined,
      sortBy: 'date',
      sortDescending: true
    }).subscribe({
      next: result => {
        this.payments.set(result.items);
        this.totalCount.set(result.totalCount);
        this.state.set(result.items.length === 0 ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  applyFilters(): void { this.page.set(1); this.load(); }

  /** Whether the current user has any available action for this payment's status. */
  hasActions(p: Payment): boolean {
    const manageable = this.canManage()
      && (p.status === PaymentStatus.Draft || p.status === PaymentStatus.PendingApproval);
    const reviewable = this.canApprove() && p.status === PaymentStatus.PendingApproval;
    return manageable || reviewable;
  }

  changePage(delta: number): void {
    const next = this.page() + delta;
    if (next >= 1 && next <= this.totalPages()) { this.page.set(next); this.load(); }
  }

  submit(p: Payment): void {
    this.service.submitForApproval(p.id).subscribe({
      next: () => { this.toast(`${p.paymentReference} submitted for approval`); this.load(); },
      error: (err: ApiError) => this.toast(err.title, true)
    });
  }

  cancel(p: Payment): void {
    this.confirm({
      title: 'Cancel payment',
      message: `Cancel "${p.paymentReference}"? This cannot be undone.`,
      confirmLabel: 'Cancel payment',
      tone: 'warn'
    }, () => this.service.cancel(p.id), `${p.paymentReference} cancelled`);
  }

  review(p: Payment, action: 'approve' | 'reject'): void {
    const isApprove = action === 'approve';
    this.dialog.open(ConfirmDialogComponent, {
      width: '440px',
      data: {
        title: isApprove ? 'Approve payment' : 'Reject payment',
        message: `${isApprove ? 'Approve' : 'Reject'} "${p.paymentReference}" `
          + `(${p.amount} ${p.currency} to ${p.beneficiaryName})?`,
        confirmLabel: isApprove ? 'Approve' : 'Reject',
        tone: isApprove ? 'primary' : 'warn',
        withNotes: true
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      const call = isApprove
        ? this.service.approve(p.id, result.notes)
        : this.service.reject(p.id, result.notes);
      call.subscribe({
        next: () => { this.toast(`${p.paymentReference} ${isApprove ? 'approved' : 'rejected'}`); this.load(); },
        error: (err: ApiError) => this.toast(err.title, true)
      });
    });
  }

  private confirm(
    data: { title: string; message: string; confirmLabel: string; tone: 'primary' | 'warn' },
    call: () => import('rxjs').Observable<Payment>,
    successMessage: string
  ): void {
    this.dialog.open(ConfirmDialogComponent, { width: '440px', data })
      .afterClosed().subscribe((result?: ConfirmDialogResult) => {
        if (!result?.confirmed) return;
        call().subscribe({
          next: () => { this.toast(successMessage); this.load(); },
          error: (err: ApiError) => this.toast(err.title, true)
        });
      });
  }

  private toast(message: string, isError = false): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 4000,
      panelClass: isError ? 'pf-snack-error' : 'pf-snack-ok'
    });
  }
}
