import { Injectable, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';
import { PaymentStatus } from '../models/payment.models';

/** Server push payload for a payment status transition (camelCase over the wire). */
export interface PaymentStatusChange {
  paymentId: string;
  paymentReference: string;
  status: PaymentStatus;
  failureReason?: string;
  updatedAtUtc: string;
}

/**
 * Thin wrapper around the SignalR connection to <c>/hubs/payments</c>. Starts
 * lazily (once, when a feature first needs it), authenticates with the current
 * access token, and auto-reconnects. Consumers subscribe to {@link changes$}.
 */
@Injectable({ providedIn: 'root' })
export class PaymentsHubService {
  private readonly auth = inject(AuthService);
  private connection?: HubConnection;
  private starting?: Promise<void>;

  /** Latest connection state, for optional UI indicators. */
  readonly connected = signal(false);

  /** Emits every payment status change pushed by the server. */
  readonly changes$ = new Subject<PaymentStatusChange>();

  /** Idempotent: builds and starts the connection the first time it is called. */
  ensureStarted(): void {
    if (this.connection || this.starting) return;

    const connection = new HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        accessTokenFactory: () => this.auth.accessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('paymentStatusChanged', (change: PaymentStatusChange) => {
      this.changes$.next(change);
    });

    connection.onreconnected(() => this.connected.set(true));
    connection.onreconnecting(() => this.connected.set(false));
    connection.onclose(() => this.connected.set(false));

    this.connection = connection;
    this.starting = connection
      .start()
      .then(() => this.connected.set(connection.state === HubConnectionState.Connected))
      .catch(() => {
        // A failed start (e.g. offline) shouldn't break the page; reconnect will retry.
        this.connected.set(false);
      })
      .finally(() => { this.starting = undefined; });
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = undefined;
      this.connected.set(false);
    }
  }
}
