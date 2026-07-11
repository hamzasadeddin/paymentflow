import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel: string;
  tone?: 'primary' | 'warn';
  withNotes?: boolean;
}

export interface ConfirmDialogResult {
  confirmed: boolean;
  notes?: string;
}

/** Reusable confirmation for sensitive actions (approve, reject, etc.). */
@Component({
  selector: 'pf-confirm-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule, FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
      @if (data.withNotes) {
        <mat-form-field appearance="outline" class="notes">
          <mat-label>Notes (optional)</mat-label>
          <textarea matInput [(ngModel)]="notes" rows="3"></textarea>
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="cancel()">Cancel</button>
      <button mat-flat-button [color]="data.tone ?? 'primary'" (click)="confirm()">{{ data.confirmLabel }}</button>
    </mat-dialog-actions>
  `,
  styles: [`.notes { width: 100%; margin-top: 8px; } p { color: var(--pf-text); }`]
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ConfirmDialogComponent, ConfirmDialogResult>);
  notes = '';

  confirm(): void { this.dialogRef.close({ confirmed: true, notes: this.notes || undefined }); }
  cancel(): void { this.dialogRef.close({ confirmed: false }); }
}
