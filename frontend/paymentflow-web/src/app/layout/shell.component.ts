import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../core/services/auth.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  enabled: boolean;
}

@Component({
  selector: 'pf-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatMenuModule],
  template: `
    <div class="shell">
      <aside class="sidebar" [class.collapsed]="collapsed()">
        <div class="brand">
          <span class="material-symbols-outlined brand-mark">account_balance</span>
          @if (!collapsed()) { <span class="brand-name">PaymentFlow</span> }
        </div>
        <nav>
          @for (item of navItems; track item.route) {
            @if (item.enabled) {
              <a class="nav-item" [routerLink]="item.route" routerLinkActive="active">
                <span class="material-symbols-outlined">{{ item.icon }}</span>
                @if (!collapsed()) { <span>{{ item.label }}</span> }
              </a>
            } @else {
              <span class="nav-item disabled" [title]="item.label + ' — coming in a later phase'">
                <span class="material-symbols-outlined">{{ item.icon }}</span>
                @if (!collapsed()) { <span>{{ item.label }}</span> }
              </span>
            }
          }
        </nav>
      </aside>

      <div class="main">
        <header class="topbar">
          <button mat-icon-button type="button" class="collapse-btn" (click)="collapsed.set(!collapsed())"
                  aria-label="Toggle navigation">
            <span class="material-symbols-outlined">menu</span>
          </button>
          <span class="env-badge">Demo environment — fictional data</span>
          <span class="spacer"></span>
          <button mat-button [matMenuTriggerFor]="userMenu" class="user-btn">
            <span class="material-symbols-outlined">account_circle</span>
            {{ auth.currentUser()?.displayName }}
          </button>
          <mat-menu #userMenu="matMenu">
            <div class="menu-roles">{{ auth.currentUser()?.roles?.join(', ') }}</div>
            <button mat-menu-item (click)="logout()">
              <span class="material-symbols-outlined">logout</span> Sign out
            </button>
          </mat-menu>
        </header>
        <main class="content"><router-outlet /></main>
      </div>
    </div>
  `,
  styles: [`
    .shell { display: flex; height: 100vh; }
    .sidebar {
      width: 232px; background: var(--pf-sidebar); color: var(--pf-sidebar-text);
      display: flex; flex-direction: column; transition: width 150ms ease;
      flex-shrink: 0;
    }
    .sidebar.collapsed { width: 64px; }
    .brand { display: flex; align-items: center; gap: 10px; padding: 18px 16px; color: #fff; }
    .brand-mark { color: #6ea8e8; }
    .brand-name { font-weight: 600; letter-spacing: 0.2px; }
    nav { display: flex; flex-direction: column; gap: 2px; padding: 8px; }
    .nav-item {
      display: flex; align-items: center; gap: 12px; padding: 10px 12px;
      border-radius: 8px; color: var(--pf-sidebar-text); text-decoration: none;
      font-size: 13.5px;
    }
    .nav-item:hover { background: rgba(255,255,255,0.06); }
    .nav-item.active { background: var(--pf-accent); color: #fff; }
    .nav-item.disabled { opacity: 0.35; cursor: default; }
    .main { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .topbar {
      display: flex; align-items: center; gap: 12px; padding: 0 16px; height: 56px;
      background: var(--pf-surface); border-bottom: 1px solid var(--pf-border);
    }
    .collapse-btn { border: none; background: none; cursor: pointer; color: var(--pf-text-muted); }
    .env-badge {
      font-size: 12px; color: var(--pf-warning); background: #fdf3d7;
      padding: 3px 10px; border-radius: 999px;
    }
    .spacer { flex: 1; }
    .user-btn { display: flex; align-items: center; gap: 6px; }
    .menu-roles { padding: 8px 16px; font-size: 12px; color: var(--pf-text-muted); }
    .content { flex: 1; overflow: auto; padding: 24px; }
    @media (max-width: 768px) {
      .sidebar { width: 64px; }
      .sidebar .brand-name, .sidebar nav span:not(.material-symbols-outlined) { display: none; }
    }
  `]
})
export class ShellComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly collapsed = signal(false);

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard', enabled: true },
    { label: 'Customers', icon: 'group', route: '/customers', enabled: true },
    { label: 'Beneficiaries', icon: 'contacts', route: '/beneficiaries', enabled: true },
    { label: 'Payments', icon: 'payments', route: '/payments', enabled: true },
    { label: 'Approvals', icon: 'task_alt', route: '/approvals', enabled: true },
    { label: 'Compliance', icon: 'policy', route: '/compliance', enabled: true },
    { label: 'Reconciliation', icon: 'balance', route: '/reconciliation', enabled: true },
    { label: 'Audit logs', icon: 'history', route: '/audit-logs', enabled: false },
    { label: 'Administration', icon: 'settings', route: '/admin', enabled: false }
  ];

  logout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.router.navigate(['/login'])
    });
  }
}
