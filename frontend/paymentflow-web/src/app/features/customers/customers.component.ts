import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CustomerService } from '../../core/services/customer.service';
import { CustomerStatus, CustomerSummary } from '../../core/models/customer.models';
import { LoadState } from '../../core/models/paged.models';
import { ApiError } from '../../core/models/auth.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { customerStatusDisplay, customerTypeLabel } from '../../shared/status-maps';

@Component({
  selector: 'pf-customers',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, StatusChipComponent],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss'
})
export class CustomersComponent {
  private readonly customerService = inject(CustomerService);
  readonly router = inject(Router);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly customers = signal<CustomerSummary[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 10;
  readonly search = signal('');
  readonly statusFilter = signal<CustomerStatus | ''>('');
  readonly sortBy = signal<'createdAtUtc' | 'name' | 'status'>('createdAtUtc');
  readonly sortDescending = signal(true);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly statusDisplay = customerStatusDisplay;
  readonly typeLabel = customerTypeLabel;

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.error.set(null);
    this.customerService.list({
      page: this.page(),
      pageSize: this.pageSize,
      search: this.search() || undefined,
      status: this.statusFilter() || undefined,
      sortBy: this.sortBy(),
      sortDescending: this.sortDescending()
    }).subscribe({
      next: result => {
        this.customers.set(result.items);
        this.totalCount.set(result.totalCount);
        this.state.set(result.items.length === 0 ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => {
        this.error.set(err);
        this.state.set('error');
      }
    });
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  toggleSort(column: 'name' | 'status'): void {
    if (this.sortBy() === column) {
      this.sortDescending.set(!this.sortDescending());
    } else {
      this.sortBy.set(column);
      this.sortDescending.set(false);
    }
    this.load();
  }

  changePage(delta: number): void {
    const next = this.page() + delta;
    if (next >= 1 && next <= this.totalPages()) {
      this.page.set(next);
      this.load();
    }
  }

  open(customer: CustomerSummary): void {
    this.router.navigate(['/customers', customer.id]);
  }
}
