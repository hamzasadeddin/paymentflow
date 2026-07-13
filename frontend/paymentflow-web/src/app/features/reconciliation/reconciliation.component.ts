import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { ReconciliationService } from '../../core/services/reconciliation.service';
import { AuthService } from '../../core/services/auth.service';
import { ReconciliationBreak, ReconciliationRun } from '../../core/models/reconciliation.models';
import { AppRoles, ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/confirm-dialog.component';
import { breakStatusDisplay, breakTypeLabel } from '../../shared/status-maps';

@Component({
  selector: 'pf-reconciliation',
  standalone: true,
  imports: [DecimalPipe, DatePipe, MatButtonModule, MatSnackBarModule, MatDialogModule, StatusChipComponent],
  template: `
    <div class="page-header">
      <div>
        <h2>Reconciliation</h2>
        <p class="pf-muted">Match settled payments against the external statement, then resolve any breaks.</p>
      </div>
      @if (canReconcile()) {
        <button mat-flat-button color="primary" (click)="runNow()" [disabled]="running()">
          <span class="material-symbols-outlined">play_arrow</span>
          {{ running() ? 'Running…' : 'Run reconciliation' }}
        </button>
      }
    </div>

    <div class="pf-card">
      @switch (state()) {
        @case ('loading') {
          <div class="skeleton-table">
            @for (row of [1,2,3]; track row) { <div class="skeleton-row"></div> }
          </div>
        }
        @case ('error') {
          <div class="state-block error">
            <span class="material-symbols-outlined">error</span>
            <p>{{ error()?.title ?? 'Could not load reconciliation runs.' }}</p>
            <button mat-stroked-button (click)="load()">Retry</button>
          </div>
        }
        @case ('empty') {
          <div class="state-block">
            <span class="material-symbols-outlined">balance</span>
            <p>No reconciliation runs yet. Run one to compare the ledger with the statement.</p>
          </div>
        }
        @case ('loaded') {
          <table class="data-table runs">
            <thead>
              <tr>
                <th>Run</th>
                <th>Statement date</th>
                <th class="right">Matched</th>
                <th class="right">Breaks</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (run of runs(); track run.id) {
                <tr [class.selected]="run.id === selectedRunId()">
                  <td class="primary">{{ run.runReference }}</td>
                  <td>{{ run.statementDateUtc | date:'medium' }}</td>
                  <td class="right mono">{{ run.matchedCount }}</td>
                  <td class="right mono">{{ run.breakCount }}</td>
                  <td class="right">
                    <button mat-stroked-button (click)="selectRun(run)">
                      {{ run.id === selectedRunId() ? 'Hide breaks' : 'View breaks' }}
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>

          @if (selectedRunId()) {
            <h3 class="section-title">Breaks</h3>
            @if (breaks().length === 0) {
              <p class="pf-muted empty-breaks">No breaks on this run — the ledger and statement agree.</p>
            } @else {
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Type</th>
                    <th>Reference</th>
                    <th class="right">Ledger</th>
                    <th class="right">Statement</th>
                    <th>Status</th>
                    @if (canReconcile()) { <th class="right">Actions</th> }
                  </tr>
                </thead>
                <tbody>
                  @for (b of breaks(); track b.id) {
                    <tr>
                      <td class="primary">{{ typeLabel(b.type) }}</td>
                      <td class="mono">{{ b.paymentReference ?? b.statementReference ?? '—' }}</td>
                      <td class="right mono">{{ b.ledgerAmount != null ? (b.ledgerAmount | number:'1.2-2') : '—' }}</td>
                      <td class="right mono">{{ b.statementAmount != null ? (b.statementAmount | number:'1.2-2') : '—' }}</td>
                      <td><pf-status-chip [label]="statusLabel(b)" [tone]="statusTone(b)" /></td>
                      @if (canReconcile()) {
                        <td class="right actions">
                          @if (b.status === 1) {
                            <button mat-stroked-button color="primary" (click)="review(b, 'resolve')">Resolve</button>
                            <button mat-stroked-button (click)="review(b, 'ignore')">Ignore</button>
                          } @else {
                            <span class="pf-muted">—</span>
                          }
                        </td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            }
          }
        }
      }
    </div>
  `,
  styles: [`
    .page-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
    .page-header h2 { margin: 0 0 4px; }
    .page-header p { margin: 0; }
    .section-title { margin: 24px 0 8px; font-size: 13px; text-transform: uppercase; letter-spacing: 0.4px; color: var(--pf-text-muted); }
    .empty-breaks { padding: 8px 0 4px; }
    .data-table { width: 100%; border-collapse: collapse; }
    .data-table.runs { margin-bottom: 4px; }
    .data-table th {
      text-align: left; font-size: 12px; font-weight: 600; text-transform: uppercase;
      letter-spacing: 0.4px; color: var(--pf-text-muted); padding: 10px 12px;
      border-bottom: 1px solid var(--pf-border);
    }
    .data-table th.right, .data-table td.right { text-align: right; }
    .data-table td { padding: 12px; border-bottom: 1px solid var(--pf-border); }
    .data-table tr.selected { background: var(--pf-accent-soft); }
    .data-table .primary { font-weight: 500; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 13px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .skeleton-table { display: flex; flex-direction: column; gap: 8px; padding: 12px 0; }
    .skeleton-row {
      height: 44px; border-radius: 8px;
      background: linear-gradient(90deg, #eef1f5 25%, #e2e7ee 37%, #eef1f5 63%);
      background-size: 400% 100%; animation: shimmer 1.3s ease-in-out infinite;
    }
    @keyframes shimmer { 0% { background-position: 100% 0; } 100% { background-position: 0 0; } }
    .state-block {
      display: flex; flex-direction: column; align-items: center; gap: 10px;
      padding: 48px 16px; text-align: center; color: var(--pf-text-muted);
    }
    .state-block .material-symbols-outlined { font-size: 40px; opacity: 0.6; }
    .state-block.error .material-symbols-outlined { color: var(--pf-danger); opacity: 1; }
  `]
})
export class ReconciliationComponent {
  private readonly service = inject(ReconciliationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly runs = signal<ReconciliationRun[]>([]);
  readonly breaks = signal<ReconciliationBreak[]>([]);
  readonly selectedRunId = signal<string | null>(null);
  readonly running = signal(false);

  readonly canReconcile = computed(() => this.auth.hasAnyRole(
    [AppRoles.Administrator, AppRoles.ComplianceOfficer, AppRoles.OperationsAnalyst]));

  constructor() {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.error.set(null);
    this.service.runs().subscribe({
      next: rows => {
        this.runs.set(rows);
        this.state.set(rows.length === 0 ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  runNow(): void {
    this.running.set(true);
    this.service.run().subscribe({
      next: run => {
        this.toast(`${run.runReference}: ${run.matchedCount} matched, ${run.breakCount} break(s)`);
        this.running.set(false);
        this.load();
        this.selectRunById(run.id);
      },
      error: (err: ApiError) => { this.running.set(false); this.toast(err.title, true); }
    });
  }

  typeLabel = breakTypeLabel;
  statusLabel(b: ReconciliationBreak): string { return breakStatusDisplay(b.status).label; }
  statusTone(b: ReconciliationBreak): 'neutral' | 'success' | 'warning' | 'danger' | 'info' {
    return breakStatusDisplay(b.status).tone;
  }

  selectRun(run: ReconciliationRun): void {
    if (this.selectedRunId() === run.id) {
      this.selectedRunId.set(null);
      this.breaks.set([]);
      return;
    }
    this.selectRunById(run.id);
  }

  private selectRunById(runId: string): void {
    this.selectedRunId.set(runId);
    this.service.breaks(runId).subscribe({
      next: rows => this.breaks.set(rows),
      error: (err: ApiError) => this.toast(err.title, true)
    });
  }

  review(b: ReconciliationBreak, action: 'resolve' | 'ignore'): void {
    const isResolve = action === 'resolve';
    this.dialog.open(ConfirmDialogComponent, {
      width: '460px',
      data: {
        title: isResolve ? 'Resolve break' : 'Ignore break',
        message: `${isResolve ? 'Resolve' : 'Ignore'} this ${this.typeLabel(b.type).toLowerCase()} break?`,
        confirmLabel: isResolve ? 'Resolve' : 'Ignore',
        tone: 'primary',
        withNotes: true
      }
    }).afterClosed().subscribe((result?: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      this.dispatch(b.id, action, result.notes).subscribe({
        next: () => {
          this.toast(`Break ${isResolve ? 'resolved' : 'ignored'}`);
          const runId = this.selectedRunId();
          if (runId) this.selectRunById(runId);
          this.load();
        },
        error: (err: ApiError) => this.toast(err.title, true)
      });
    });
  }

  private dispatch(id: string, action: 'resolve' | 'ignore', notes?: string): Observable<unknown> {
    return action === 'resolve' ? this.service.resolve(id, notes) : this.service.ignore(id, notes);
  }

  private toast(message: string, isError = false): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 4000,
      panelClass: isError ? 'pf-snack-error' : 'pf-snack-ok'
    });
  }
}
