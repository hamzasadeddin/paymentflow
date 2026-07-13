import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { PaymentService } from '../../core/services/payment.service';
import { ReconciliationService } from '../../core/services/reconciliation.service';
import { PaymentsHubService, PaymentStatusChange } from '../../core/realtime/payments-hub.service';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { paymentStatusDisplay } from '../../shared/status-maps';
import { PaymentStatusCount } from '../../core/models/payment.models';
import { ReconciliationRun, BreakStatus } from '../../core/models/reconciliation.models';

@Component({
  selector: 'pf-dashboard',
  standalone: true,
  imports: [DatePipe, RouterLink, StatusChipComponent],
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

    <div class="summary-grid">
      <!-- Payment status overview -->
      <div class="pf-card summary">
        <div class="summary-head">
          <span class="material-symbols-outlined">payments</span>
          <h3>Payment status overview</h3>
          <a routerLink="/payments" class="more">View all</a>
        </div>
        @switch (summaryState()) {
          @case ('loading') { <div class="skeleton"></div> }
          @case ('error')   { <p class="pf-muted">Couldn't load payment totals.</p> }
          @default {
            <div class="total">{{ summaryTotal() }} <span class="pf-muted">payments</span></div>
            @if (nonZeroCounts().length === 0) {
              <p class="pf-muted">No payments yet.</p>
            } @else {
              <ul class="chip-list">
                @for (c of nonZeroCounts(); track c.status) {
                  <li>
                    <pf-status-chip [label]="pStatus(c.status).label" [tone]="pStatus(c.status).tone" />
                    <span class="count mono">{{ c.count }}</span>
                  </li>
                }
              </ul>
            }
          }
        }
      </div>

      <!-- Reconciliation exceptions -->
      <div class="pf-card summary">
        <div class="summary-head">
          <span class="material-symbols-outlined">balance</span>
          <h3>Reconciliation exceptions</h3>
          <a routerLink="/reconciliation" class="more">Open</a>
        </div>
        @switch (reconState()) {
          @case ('loading') { <div class="skeleton"></div> }
          @case ('error')   { <p class="pf-muted">Couldn't load reconciliation status.</p> }
          @case ('none')    { <p class="pf-muted">No reconciliation runs yet. Run one from the Reconciliation page.</p> }
          @default {
            <div class="total" [class.warn]="openBreaks() > 0">
              {{ openBreaks() }} <span class="pf-muted">open break{{ openBreaks() === 1 ? '' : 's' }}</span>
            </div>
            <p class="pf-muted latest">
              Latest run {{ latestRun()?.runReference }} · {{ latestRun()?.matchedCount }} matched,
              {{ latestRun()?.breakCount }} total break{{ latestRun()?.breakCount === 1 ? '' : 's' }}
            </p>
          }
        }
      </div>
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

    .summary-grid {
      margin-top: 24px; display: grid; gap: 16px;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    }
    .summary-head { display: flex; align-items: center; gap: 8px; }
    .summary-head h3 { margin: 0; font-size: 15px; }
    .summary-head .material-symbols-outlined { color: var(--pf-accent); font-size: 22px; }
    .summary-head .more {
      margin-left: auto; font-size: 12.5px; text-decoration: none;
      color: var(--pf-accent); font-weight: 500;
    }
    .summary-head .more:hover { text-decoration: underline; }
    .total { margin: 14px 0 4px; font-size: 26px; font-weight: 600; }
    .total span { font-size: 13px; font-weight: 400; }
    .total.warn { color: var(--pf-danger, #c62828); }
    .latest { margin: 4px 0 0; font-size: 12.5px; }
    .chip-list { list-style: none; margin: 10px 0 0; padding: 0; display: flex; flex-wrap: wrap; gap: 8px 14px; }
    .chip-list li { display: flex; align-items: center; gap: 6px; }
    .chip-list .count { font-weight: 600; }
    .skeleton {
      height: 64px; margin-top: 12px; border-radius: 8px;
      background: linear-gradient(90deg, #eef1f5 25%, #e2e7ee 37%, #eef1f5 63%);
      background-size: 400% 100%; animation: shimmer 1.3s ease-in-out infinite;
    }
    @keyframes shimmer { 0% { background-position: 100% 0; } 100% { background-position: 0 0; } }
  `]
})
export class DashboardComponent {
  readonly auth = inject(AuthService);
  readonly hub = inject(PaymentsHubService);
  private readonly payments = inject(PaymentService);
  private readonly reconciliation = inject(ReconciliationService);
  private readonly destroyRef = inject(DestroyRef);
  private seq = 0;

  readonly statusDisplay = paymentStatusDisplay;
  readonly pStatus = paymentStatusDisplay;
  readonly activity = signal<{ key: number; change: PaymentStatusChange }[]>([]);

  // Payment status overview
  readonly summaryState = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly summaryTotal = signal(0);
  readonly nonZeroCounts = signal<PaymentStatusCount[]>([]);

  // Reconciliation exceptions
  readonly reconState = signal<'loading' | 'loaded' | 'none' | 'error'>('loading');
  readonly latestRun = signal<ReconciliationRun | null>(null);
  readonly openBreaks = signal(0);

  constructor() {
    this.hub.ensureStarted();
    this.hub.changes$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(change => {
      this.activity.update(list => [{ key: this.seq++, change }, ...list].slice(0, 8));
      // A status change may create/settle a payment — refresh the overview.
      this.loadPaymentSummary();
    });

    this.loadPaymentSummary();
    this.loadReconciliation();
  }

  private loadPaymentSummary(): void {
    this.payments.statusSummary().subscribe({
      next: summary => {
        this.summaryTotal.set(summary.total);
        this.nonZeroCounts.set(summary.counts.filter(c => c.count > 0));
        this.summaryState.set('loaded');
      },
      error: () => this.summaryState.set('error')
    });
  }

  private loadReconciliation(): void {
    this.reconciliation.runs().subscribe({
      next: runs => {
        if (runs.length === 0) {
          this.reconState.set('none');
          return;
        }
        const latest = runs[0]; // runs come back most-recent first
        this.latestRun.set(latest);
        this.reconciliation.breaks(latest.id, BreakStatus.Open).subscribe({
          next: breaks => { this.openBreaks.set(breaks.length); this.reconState.set('loaded'); },
          error: () => this.reconState.set('error')
        });
      },
      error: () => this.reconState.set('error')
    });
  }
}
