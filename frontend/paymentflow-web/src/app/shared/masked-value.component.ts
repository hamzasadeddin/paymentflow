import { Component, Input, signal } from '@angular/core';

/**
 * Shows a pre-masked value with an optional reveal toggle. Reveal only works
 * when a full value is explicitly supplied (privileged callers); by default the
 * component never has the sensitive data at all.
 */
@Component({
  selector: 'pf-masked-value',
  standalone: true,
  template: `
    <span class="masked">
      <span class="value">{{ revealed() && fullValue ? fullValue : maskedValue }}</span>
      @if (fullValue) {
        <button type="button" class="toggle" (click)="revealed.set(!revealed())"
                [attr.aria-label]="revealed() ? 'Hide value' : 'Reveal value'">
          <span class="material-symbols-outlined">{{ revealed() ? 'visibility_off' : 'visibility' }}</span>
        </button>
      }
    </span>
  `,
  styles: [`
    .masked { display: inline-flex; align-items: center; gap: 6px; font-variant-numeric: tabular-nums; }
    .toggle { border: none; background: none; cursor: pointer; color: var(--pf-text-muted); padding: 0; display: inline-flex; }
    .toggle .material-symbols-outlined { font-size: 18px; }
  `]
})
export class MaskedValueComponent {
  @Input({ required: true }) maskedValue = '';
  @Input() fullValue?: string;
  readonly revealed = signal(false);
}
