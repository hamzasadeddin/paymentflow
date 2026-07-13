import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { PaymentsHubService, PaymentStatusChange } from '../../core/realtime/payments-hub.service';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { paymentStatusDisplay } from '../../shared/status-maps';

@Component({
  selector: 'pf-dashboard',
  standalone: true,
  imports: [DatePipe, StatusChipComponent],
  template: `
    <h2>Welcome back, {{ auth.currentUser()?.displayName }}</h2>
    <p class="pf-muted">Signed in as {{ auth.currentUser()?.roles?.join(', ') }}.</p>

    <div class="pf-card live">
      <div class="live-head">
        <h3>Live payment activity</h3>
        <span class="dot" [class.on]="hub.connected()"
              [title]="hub.connected() ? 'Connected' : 'Connecting…'"></span>
      </div>
      @if (activity().length === 0) {
        <p class="pf-muted empty">Waiting for payment activity… approve or process a payment to see it appear here in real time.</p>
      } @else {
        <ul class="feed">
          @for (item of activity(); track item.key) {
            <li>
              <span class="mono ref">{{ item.change.paymentReference }}</span>
              <pf-status-chip
                [label]="statusDisplay(item.change.status).label"
                [tone]="statusDisplay(item.change.status).tone" />
              @if (item.change.failureReason) {
                <span class="reason pf-muted">{{ item.change.failureReason }}</span>
              }
              <span class="time pf-muted">{{ item.change.updatedAtUtc | date:'HH:mm:ss' }}</span>
            </li>
          }
        </ul>
      }
    </div>

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
    .live { margin-top: 24px; }
    .live-head { display: flex; align-items: center; gap: 10px; }
    .live-head h3 { margin: 0; font-size: 15px; }
    .dot { width: 9px; height: 9px; border-radius: 50%; background: var(--pf-text-muted); opacity: 0.5; }
    .dot.on { background: var(--pf-success, #2e7d32); opacity: 1; }
    .empty { margin: 12px 0 0; }
    .feed { list-style: none; margin: 12px 0 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
    .feed li { display: flex; align-items: center; gap: 12px; padding: 8px 0; border-bottom: 1px solid var(--pf-border); }
    .feed .ref { min-width: 150px; }
    .feed .reason { flex: 1; font-size: 12.5px; }
    .feed .time { margin-left: auto; font-size: 12px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 13px; }
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
  readonly hub = inject(PaymentsHubService);
  private readonly destroyRef = inject(DestroyRef);
  private seq = 0;

  readonly statusDisplay = paymentStatusDisplay;
  readonly activity = signal<{ key: number; change: PaymentStatusChange }[]>([]);

  readonly placeholders = [
    { icon: 'payments', title: 'Payment status overview', phase: 'Arrives with Phase 06 dashboards' },
    { icon: 'balance', title: 'Reconciliation exceptions', phase: 'Arrives with Phase 06 reconciliation' }
  ];

  constructor() {
    this.hub.ensureStarted();
    this.hub.changes$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(change => {
      this.activity.update(list =>
        [{ key: this.seq++, change }, ...list].slice(0, 8));
    });
  }
}
