import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CustomerService } from '../../core/services/customer.service';
import { CustomerType } from '../../core/models/customer.models';
import { ApiError } from '../../core/models/auth.models';

@Component({
  selector: 'pf-customer-create',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './customer-create.component.html',
  styleUrl: './customer-create.component.scss'
})
export class CustomerCreateComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly customerService = inject(CustomerService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly error = signal<ApiError | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    type: [CustomerType.Individual, Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', Validators.email],
    phoneNumber: [''],
    countryCode: ['', [Validators.minLength(2), Validators.maxLength(2)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    this.customerService.create({
      type: value.type,
      name: value.name,
      email: value.email || undefined,
      phoneNumber: value.phoneNumber || undefined,
      countryCode: value.countryCode ? value.countryCode.toUpperCase() : undefined
    }).subscribe({
      next: created => this.router.navigate(['/customers', created.id]),
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
      const control = this.form.get(field);
      if (control) control.setErrors({ server: messages[0] });
    }
  }
}
