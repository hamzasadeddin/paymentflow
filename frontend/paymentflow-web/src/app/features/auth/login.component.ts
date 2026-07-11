import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AuthService } from '../../core/services/auth.service';
import { ApiError } from '../../core/models/auth.models';

@Component({
  selector: 'pf-login',
  standalone: true,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatProgressBarModule],
  template: `
    <div class="login-page">
      <div class="login-card pf-card">
        <div class="brand">
          <span class="material-symbols-outlined">account_balance</span>
          <h1>PaymentFlow</h1>
        </div>
        <p class="pf-muted subtitle">Operations sign-in</p>

        @if (loading()) { <mat-progress-bar mode="indeterminate" /> }

        <form [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="username" />
            @if (form.controls.email.hasError('required')) { <mat-error>Enter your email</mat-error> }
            @if (form.controls.email.hasError('email')) { <mat-error>Enter a valid email</mat-error> }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="current-password" />
            @if (form.controls.password.hasError('required')) { <mat-error>Enter your password</mat-error> }
          </mat-form-field>

          @if (error()) {
            <p class="pf-error-text" role="alert">{{ error()!.title }}</p>
          }

          <button mat-flat-button color="primary" type="submit" [disabled]="loading()">
            Sign in
          </button>
        </form>

        <div class="demo-hint">
          <strong>Demo accounts</strong> (password in <code>appsettings.Development.json</code>):
          admin&#64;paymentflow.local, analyst&#64;, approver&#64;, compliance&#64;, auditor&#64;paymentflow.local
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: 100vh; display: grid; place-items: center;
      background: linear-gradient(160deg, var(--pf-sidebar) 0%, #1c3a63 100%);
      padding: 16px;
    }
    .login-card { width: 100%; max-width: 400px; }
    .brand { display: flex; align-items: center; gap: 10px; }
    .brand .material-symbols-outlined { color: var(--pf-accent); font-size: 30px; }
    h1 { font-size: 22px; margin: 0; }
    .subtitle { margin-top: 4px; }
    form { display: flex; flex-direction: column; gap: 8px; margin-top: 16px; }
    button[type=submit] { margin-top: 4px; height: 44px; }
    .demo-hint {
      margin-top: 20px; padding: 10px 12px; border-radius: 8px;
      background: var(--pf-accent-soft); font-size: 12px; color: var(--pf-text-muted);
    }
  `]
})
export class LoginComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        this.router.navigateByUrl(returnUrl);
      },
      error: (apiError: ApiError) => {
        this.loading.set(false);
        this.error.set(apiError);
      }
    });
  }
}
