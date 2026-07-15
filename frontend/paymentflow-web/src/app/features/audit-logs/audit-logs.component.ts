import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { AuditService } from '../../core/services/audit.service';
import { AuditEvent, AuditEventTypeGroup, AuditQuery } from '../../core/models/audit.models';
import { ApiError } from '../../core/models/auth.models';
import { LoadState } from '../../core/models/paged.models';
import { StatusChipComponent } from '../../shared/status-chip.component';

@Component({
  selector: 'pf-audit-logs',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, StatusChipComponent],
  template: `
    <div class="page-header">
      <div>
        <h2>Audit logs</h2>
        <p class="pf-muted">The security audit trail — every login, approval, compliance action, and admin change.</p>
      </div>
    </div>

    <div class="pf-card filters">
      <div class="filter">
        <label>Event type</label>
        <select [(ngModel)]="eventType" (change)="applyFilters()">
          <option value="">All events</option>
          @for (group of eventTypeGroups(); track group.group) {
            <optgroup [label]="group.group">
              @for (t of group.types; track t) { <option [value]="t">{{ t }}</option> }
            </optgroup>
          }
        </select>
      </div>
      <div class="filter">
        <label>Outcome</label>
        <select [(ngModel)]="succeeded" (change)="applyFilters()">
          <option [ngValue]="null">Any</option>
          <option [ngValue]="true">Success</option>
          <option [ngValue]="false">Failure</option>
        </select>
      </div>
      <div class="filter">
        <label>User (email)</label>
        <input type="text" [(ngModel)]="userQuery" (keyup.enter)="applyFilters()" placeholder="name@…" />
      </div>
      <div class="filter grow">
        <label>Search details</label>
        <input type="text" [(ngModel)]="search" (keyup.enter)="applyFilters()" placeholder="reference, reason, event…" />
      </div>
      <div class="filter-actions">
        <button mat-flat-button color="primary" (click)="applyFilters()">Apply</button>
        <button mat-stroked-button (click)="reset()">Reset</button>
      </div>
    </div>

    <div class="pf-card">
      @switch (state()) {
        @case ('loading') {
          <div class="skeleton-table">
            @for (row of [1,2,3,4,5]; track row) { <div class="skeleton-row"></div> }
          </div>
        }
        @case ('error') {
          <div class="state-block error">
            <span class="material-symbols-outlined">error</span>
            <p>{{ error()?.title ?? 'Could not load audit events.' }}</p>
            <button mat-stroked-button (click)="load()">Retry</button>
          </div>
        }
        @case ('empty') {
          <div class="state-block">
            <span class="material-symbols-outlined">history</span>
            <p>No audit events match these filters.</p>
          </div>
        }
        @case ('loaded') {
          <table class="data-table">
            <thead>
              <tr>
                <th>Time (UTC)</th>
                <th>Event</th>
                <th>Outcome</th>
                <th>User</th>
                <th>IP</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              @for (e of events(); track e.id) {
                <tr>
                  <td class="mono nowrap">{{ e.occurredAtUtc | date:'yyyy-MM-dd HH:mm:ss':'UTC' }}</td>
                  <td class="primary">{{ e.eventType }}</td>
                  <td><pf-status-chip [label]="e.succeeded ? 'Success' : 'Failure'"
                                      [tone]="e.succeeded ? 'success' : 'danger'" /></td>
                  <td>{{ e.email ?? '—' }}</td>
                  <td class="mono">{{ e.ipAddress ?? '—' }}</td>
                  <td class="details">{{ e.details ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>

          <div class="pager">
            <span class="pf-muted">
              Page {{ page() }} of {{ totalPages() }} · {{ totalCount() }} event(s)
            </span>
            <span class="spacer"></span>
            <button mat-stroked-button [disabled]="page() <= 1" (click)="goto(page() - 1)">Previous</button>
            <button mat-stroked-button [disabled]="page() >= totalPages()" (click)="goto(page() + 1)">Next</button>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 20px; }
    .page-header h2 { margin: 0 0 4px; }
    .page-header p { margin: 0; }
    .filters { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 14px; margin-bottom: 16px; }
    .filter { display: flex; flex-direction: column; gap: 4px; }
    .filter.grow { flex: 1; min-width: 180px; }
    .filter label { font-size: 12px; color: var(--pf-text-muted); font-weight: 600; }
    .filter select, .filter input {
      height: 36px; padding: 0 10px; border: 1px solid var(--pf-border); border-radius: 8px;
      background: var(--pf-surface); color: var(--pf-text); font-size: 13px;
    }
    .filter.grow input { width: 100%; }
    .filter-actions { display: flex; gap: 8px; }
    .data-table { width: 100%; border-collapse: collapse; }
    .data-table th {
      text-align: left; font-size: 12px; font-weight: 600; text-transform: uppercase;
      letter-spacing: 0.4px; color: var(--pf-text-muted); padding: 10px 12px;
      border-bottom: 1px solid var(--pf-border);
    }
    .data-table td { padding: 11px 12px; border-bottom: 1px solid var(--pf-border); vertical-align: top; }
    .data-table .primary { font-weight: 500; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 12.5px; }
    .nowrap { white-space: nowrap; }
    .details { color: var(--pf-text-muted); max-width: 420px; }
    .pager { display: flex; align-items: center; gap: 8px; padding-top: 14px; }
    .pager .spacer { flex: 1; }
    .skeleton-table { display: flex; flex-direction: column; gap: 8px; padding: 12px 0; }
    .skeleton-row {
      height: 42px; border-radius: 8px;
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
export class AuditLogsComponent {
  private readonly service = inject(AuditService);

  readonly state = signal<LoadState>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly events = signal<AuditEvent[]>([]);
  readonly eventTypeGroups = signal<AuditEventTypeGroup[]>([]);

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);

  // Filter model (bound to the controls).
  eventType = '';
  succeeded: boolean | null = null;
  userQuery = '';
  search = '';

  constructor() {
    this.service.eventTypes().subscribe({
      next: groups => this.eventTypeGroups.set(groups),
      error: () => this.eventTypeGroups.set([])
    });
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.error.set(null);

    const query: AuditQuery = {
      page: this.page(),
      pageSize: this.pageSize(),
      eventType: this.eventType || undefined,
      succeeded: this.succeeded ?? undefined,
      userQuery: this.userQuery || undefined,
      search: this.search || undefined
    };

    this.service.list(query).subscribe({
      next: result => {
        this.events.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(result.totalPages, 1));
        this.state.set(result.items.length === 0 ? 'empty' : 'loaded');
      },
      error: (err: ApiError) => { this.error.set(err); this.state.set('error'); }
    });
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  reset(): void {
    this.eventType = '';
    this.succeeded = null;
    this.userQuery = '';
    this.search = '';
    this.applyFilters();
  }

  goto(page: number): void {
    this.page.set(page);
    this.load();
  }
}
