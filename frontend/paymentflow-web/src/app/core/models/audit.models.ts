export interface AuditEvent {
  id: string;
  userId?: string;
  email?: string;
  eventType: string;
  succeeded: boolean;
  ipAddress?: string;
  details?: string;
  occurredAtUtc: string;
}

export interface AuditEventTypeGroup {
  group: string;
  types: string[];
}

export interface AuditQuery {
  page?: number;
  pageSize?: number;
  eventType?: string;
  succeeded?: boolean;
  userQuery?: string;
  fromUtc?: string;
  toUtc?: string;
  search?: string;
}
