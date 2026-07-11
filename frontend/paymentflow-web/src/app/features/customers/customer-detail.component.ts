import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { CustomerService } from '../../core/services/customer.service';
import { CustomerDetail } from '../../core/models/customer.models';
import { LoadState } from '../../core/models/paged.models';
import { ApiError } from '../../core/models/auth.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { MaskedValueComponent } from '../../shared/masked-value.component';
import {
  accountStatusDisplay, customerStatusDisplay, customerTypeLabel
} from '../../shared/status-maps';

@Component({
  selector: 'pf-customer-detail',
  standalone: true,
  imports: [RouterLink, MatButtonModule, StatusChipComponent, MaskedValueComponent],
  templateUrl: './customer-detail.component.html',
  styleUrl: './customer-detail.component.scss'
})
export class CustomerDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly customerService = inject(CustomerService);
  readonly router = inject(Router);

  readonly state = signal<LoadState>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly customer = signal<CustomerDetail | null>(null);

  readonly statusDisplay = customerStatusDisplay;
  readonly accountStatusDisplay = accountStatusDisplay;
  readonly typeLabel = customerTypeLabel;

  constructor() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.customerService.get(id).subscribe({
      next: c => { this.customer.set(c); this.state.set('loaded'); },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  formatMoney(amount: number, currency: string): string {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
  }
}
