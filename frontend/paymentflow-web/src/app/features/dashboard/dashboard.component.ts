import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'pf-dashboard',
  standalone: true,
  template: `
    <h2>Welcome back, {{ auth.currentUser()?.displayName }}</h2>
    <p class="pf-muted">Signed in as {{ auth.currentUser()?.roles?.join(', ') }}.</p>

    <div class="placeholder-grid">
      @for (card of placeholders; track card.title) {
        <div class="pf-card placeholder">
          <span class="material-symbols-outlined">{{ card.icon }}</span>
          <h3>{{ card.title }}</h3>
          <p class="pf-muted">{{ card.phase }}</p>
        </div>
      }
    </div>
  `,
  styles: [`
    h2 { margin: 0 0 4px; }
    .placeholder-grid {
      margin-top: 24px; display: grid; gap: 16px;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
    }
    .placeholder { text-align: left; }
    .placeholder .material-symbols-outlined { color: var(--pf-accent); font-size: 28px; }
    .placeholder h3 { margin: 8px 0 4px; font-size: 15px; }
    .placeholder p { margin: 0; font-size: 12.5px; }
  `]
})
export class DashboardComponent {
  readonly auth = inject(AuthService);

  readonly placeholders = [
    { icon: 'payments', title: 'Payment status overview', phase: 'Arrives with Phase 06 dashboards' },
    { icon: 'task_alt', title: 'Approval backlog', phase: 'Arrives with Phase 04 approvals' },
    { icon: 'policy', title: 'Compliance holds', phase: 'Arrives with Phase 04 compliance' },
    { icon: 'balance', title: 'Reconciliation exceptions', phase: 'Arrives with Phase 06 reconciliation' }
  ];
}
