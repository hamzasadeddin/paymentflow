import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then(m => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'customers',
        loadComponent: () => import('./features/customers/customers.component').then(m => m.CustomersComponent)
      },
      {
        path: 'customers/new',
        loadComponent: () => import('./features/customers/customer-create.component').then(m => m.CustomerCreateComponent)
      },
      {
        path: 'customers/:id',
        loadComponent: () => import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent)
      },
      {
        path: 'beneficiaries',
        loadComponent: () => import('./features/beneficiaries/beneficiaries.component').then(m => m.BeneficiariesComponent)
      },
      {
        path: 'payments',
        loadComponent: () => import('./features/payments/payments.component').then(m => m.PaymentsComponent)
      },
      {
        path: 'payments/new',
        loadComponent: () => import('./features/payments/payment-create.component').then(m => m.PaymentCreateComponent)
      },
      {
        path: 'beneficiaries/new',
        loadComponent: () => import('./features/beneficiaries/beneficiary-create.component').then(m => m.BeneficiaryCreateComponent)
      },
      {
        path: 'approvals',
        loadComponent: () => import('./features/approvals/approvals.component').then(m => m.ApprovalsComponent)
      }
      // Future phases: compliance, reconciliation, audit-logs, admin.
    ]
  },
  { path: '**', redirectTo: '' }
];
