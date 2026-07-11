import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { BeneficiaryService } from '../../core/services/beneficiary.service';
import { AuthService } from '../../core/services/auth.service';
import { Beneficiary, BeneficiaryStatus } from '../../core/models/beneficiary.models';
import { AppRoles, ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { MaskedValueComponent } from '../../shared/masked-value.component';
import { beneficiaryStatusDisplay } from '../../shared/status-maps';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/confirm-dialog.component';

@Component({
  selector: 'pf-beneficiaries',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatMenuModule,
    MatSelectModule, MatSnackBarModule, MatDialogModule, StatusChipComponent, MaskedValueComponent
  ],
  templateUrl: './beneficiaries.component.html',
  styleUrl: './beneficiaries.component.scss'
})
export class BeneficiariesComponent {
  private readonly service = inject(BeneficiaryService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly beneficiaries = signal<Beneficiary[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 10;
  readonly search = signal('');
  readonly statusFilter = signal<BeneficiaryStatus | ''>('');

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly statusDisplay = beneficiaryStatusDisplay;
  readonly BeneficiaryStatus = BeneficiaryStatus;

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
      status: this.statusFilter() || undefined
    }).subscribe({
      next: result => {
        this.beneficiaries.set(result.items);
        this.totalCount.set(result.totalCount);
        this.state.set(result.items.length === 0 ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  applyFilters(): void { this.page.set(1); this.load(); }

  changePage(delta: number): void {
    const next = this.page() + delta;
    if (next >= 1 && next <= this.totalPages()) { this.page.set(next); this.load(); }
  }

  submit(b: Beneficiary): void {
    this.service.submitForApproval(b.id).subscribe({
      next: () => { this.toast(`${b.name} submitted for approval`); this.load(); },
      error: (err: ApiError) => this.toast(err.title, true)
    });
  }

  review(b: Beneficiary, action: 'approve' | 'reject'): void {
    const isApprove = action === 'approve';
    this.dialog.open(ConfirmDialogComponent, {
      width: '440px',
      data: {
        title: isApprove ? 'Approve beneficiary' : 'Reject beneficiary',
        message: `${isApprove ? 'Approve' : 'Reject'} "${b.name}"?`,
        confirmLabel: isApprove ? 'Approve' : 'Reject',
        tone: isApprove ? 'primary' : 'warn',
        withNotes: true
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      const call = isApprove
        ? this.service.approve(b.id, result.notes)
        : this.service.reject(b.id, result.notes);
      call.subscribe({
        next: () => { this.toast(`${b.name} ${isApprove ? 'approved' : 'rejected'}`); this.load(); },
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
