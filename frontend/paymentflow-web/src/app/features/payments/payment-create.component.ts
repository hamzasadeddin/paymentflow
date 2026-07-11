import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PaymentService } from '../../core/services/payment.service';
import { CustomerService } from '../../core/services/customer.service';
import { BeneficiaryService } from '../../core/services/beneficiary.service';
import { CustomerSummary, AccountSummary } from '../../core/models/customer.models';
import { Beneficiary, BeneficiaryStatus } from '../../core/models/beneficiary.models';
import { ApiError } from '../../core/models/auth.models';

@Component({
  selector: 'pf-payment-create',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './payment-create.component.html',
  styleUrl: './payment-create.component.scss'
})
export class PaymentCreateComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly payments = inject(PaymentService);
  private readonly customers = inject(CustomerService);
  private readonly beneficiaries = inject(BeneficiaryService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly error = signal<ApiError | null>(null);

  readonly customerList = signal<CustomerSummary[]>([]);
  readonly accounts = signal<AccountSummary[]>([]);
  readonly approvedBeneficiaries = signal<Beneficiary[]>([]);

  readonly form = this.formBuilder.nonNullable.group({
    customerId: ['', Validators.required],
    sourceAccountId: ['', Validators.required],
    beneficiaryId: ['', Validators.required],
    amount: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(0.01)]
    }),
    description: ['']
  });

  /** Signal-backed mirror of the sourceAccountId form control so computeds react to it. */
  readonly selectedAccountId = signal<string>('');

  /** Currency is dictated by the chosen source account. */
  readonly selectedAccount = computed(() =>
    this.accounts().find(a => a.id === this.selectedAccountId()));

  /** Only beneficiaries whose currency matches the source account can be paid. */
  readonly payableBeneficiaries = computed(() => {
    const currency = this.selectedAccount()?.currency;
    return currency
      ? this.approvedBeneficiaries().filter(b => b.currency === currency)
      : this.approvedBeneficiaries();
  });

  constructor() {
    this.customers.list({ pageSize: 100, status: 1 }).subscribe({
      next: result => this.customerList.set(result.items),
      error: (err: ApiError) => this.error.set(err)
    });
  }

  onCustomerChange(customerId: string): void {
    this.form.patchValue({ sourceAccountId: '', beneficiaryId: '' });
    this.selectedAccountId.set('');
    this.accounts.set([]);
    this.approvedBeneficiaries.set([]);
    if (!customerId) return;

    this.customers.accounts(customerId).subscribe({
      next: accounts => this.accounts.set(accounts.filter(a => a.status === 1)),
      error: (err: ApiError) => this.error.set(err)
    });
    this.beneficiaries.list({ customerId, status: BeneficiaryStatus.Approved, pageSize: 100 }).subscribe({
      next: result => this.approvedBeneficiaries.set(result.items),
      error: (err: ApiError) => this.error.set(err)
    });
  }

  onAccountChange(): void {
    // Mirror the chosen account id into the signal so selectedAccount() recomputes.
    this.selectedAccountId.set(this.form.controls.sourceAccountId.value);

    // Clear a beneficiary that no longer matches the account currency.
    const stillValid = this.payableBeneficiaries()
      .some(b => b.id === this.form.controls.beneficiaryId.value);
    if (!stillValid) this.form.patchValue({ beneficiaryId: '' });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const account = this.selectedAccount();
    if (!account) return;

    this.saving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    this.payments.create({
      sourceAccountId: value.sourceAccountId,
      beneficiaryId: value.beneficiaryId,
      amount: Number(value.amount),
      currency: account.currency,
      description: value.description || undefined
    }).subscribe({
      next: created => this.router.navigate(['/payments'], { queryParams: { created: created.id } }),
      error: (err: ApiError) => {
        this.error.set(err);
        this.applyFieldErrors(err);
        this.saving.set(false);
      }
    });
  }

  private applyFieldErrors(err: ApiError): void {
    if (!err.fieldErrors) return;
    for (const [field, messages] of Object.entries(err.fieldErrors)) {
      const control = this.form.get(field.charAt(0).toLowerCase() + field.slice(1));
      if (control) control.setErrors({ server: messages[0] });
    }
  }
}
