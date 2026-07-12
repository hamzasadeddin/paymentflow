import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { BeneficiaryService } from '../../core/services/beneficiary.service';
import { CustomerService } from '../../core/services/customer.service';
import { CustomerSummary } from '../../core/models/customer.models';
import { ApiError } from '../../core/models/auth.models';

/** Supported ISO 4217 codes — mirrors the backend allow-list. */
const SUPPORTED_CURRENCIES = ['USD', 'EUR', 'GBP', 'JOD', 'AED', 'SAR', 'JPY', 'CHF', 'CAD', 'AUD'];

@Component({
  selector: 'pf-beneficiary-create',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './beneficiary-create.component.html',
  styleUrl: './beneficiary-create.component.scss'
})
export class BeneficiaryCreateComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly beneficiaries = inject(BeneficiaryService);
  private readonly customers = inject(CustomerService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly customerList = signal<CustomerSummary[]>([]);
  readonly currencies = SUPPORTED_CURRENCIES;

  readonly form = this.formBuilder.nonNullable.group({
    customerId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    accountNumber: ['', [Validators.required, Validators.minLength(6)]],
    bankName: [''],
    bankIdentifierCode: [''],
    currency: ['', Validators.required],
    countryCode: ['']
  });

  constructor() {
    this.customers.list({ pageSize: 100, status: 1 }).subscribe({
      next: result => this.customerList.set(result.items),
      error: (err: ApiError) => this.error.set(err)
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    this.beneficiaries.create({
      customerId: value.customerId,
      name: value.name.trim(),
      accountNumber: value.accountNumber.trim(),
      bankName: value.bankName.trim() || undefined,
      bankIdentifierCode: value.bankIdentifierCode.trim() || undefined,
      currency: value.currency,
      countryCode: value.countryCode.trim() || undefined
    }).subscribe({
      next: created => this.router.navigate(['/beneficiaries'], { queryParams: { created: created.id } }),
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
