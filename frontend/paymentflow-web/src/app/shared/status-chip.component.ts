import { Component, Input } from '@angular/core';

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

/** Consistent status pill used across payments, beneficiaries, accounts. */
@Component({
  selector: 'pf-status-chip',
  standalone: true,
  template: `<span class="chip" [class]="tone">{{ label }}</span>`,
  styles: [`
    .chip {
      display: inline-flex; align-items: center; gap: 4px;
      padding: 2px 10px; border-radius: 999px;
      font-size: 12px; font-weight: 600; line-height: 20px; white-space: nowrap;
    }
    .neutral { background: #eceff3; color: #4b5a6b; }
    .success { background: #e2f2e9; color: var(--pf-success); }
    .warning { background: #fdf0d5; color: var(--pf-warning); }
    .danger  { background: #fbe4e2; color: var(--pf-danger); }
    .info    { background: var(--pf-accent-soft); color: var(--pf-accent); }
  `]
})
export class StatusChipComponent {
  @Input({ required: true }) label = '';
  @Input() tone: Tone = 'neutral';
}
