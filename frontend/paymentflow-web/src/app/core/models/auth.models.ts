export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AuthSession {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  user: AuthUser;
}

export interface ApiError {
  status: number;
  title: string;
  code?: string;
  correlationId?: string;
  fieldErrors?: Record<string, string[]>;
}

export const AppRoles = {
  Administrator: 'Administrator',
  OperationsAnalyst: 'OperationsAnalyst',
  PaymentApprover: 'PaymentApprover',
  ComplianceOfficer: 'ComplianceOfficer',
  ReadOnlyAuditor: 'ReadOnlyAuditor'
} as const;
