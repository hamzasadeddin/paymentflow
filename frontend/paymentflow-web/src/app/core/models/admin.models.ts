export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
  createdAtUtc: string;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  password: string;
  roles: string[];
}

// ---------- Rule sets ----------

export interface ApprovalRuleValues {
  autoApproveBelow: number;
  dualApprovalAtOrAbove: number;
}

export interface ScreeningRuleValues {
  autoScreenOnSubmit: boolean;
  watchlistBeneficiaryNames: string[];
  watchlistCountryCodes: string[];
  singlePaymentReviewLimit: number;
}

export interface ReconciliationRuleValues {
  introduceSyntheticBreaks: boolean;
  dropReferenceEndingIn: string;
  phantomAmount: number;
  amountDriftMinorUnits: number;
}

export interface ProcessingRuleValues {
  autoProcessEnabled: boolean;
  pollingIntervalSeconds: number;
  batchSize: number;
  simulatedLatencyMsMin: number;
  simulatedLatencyMsMax: number;
  failOnCents: number;
}

export interface RuleSet<T> {
  values: T;
  isOverridden: boolean;
  updatedByUserId?: string;
  updatedAtUtc?: string;
  rowVersion?: string;
}

export interface Rules {
  approval: RuleSet<ApprovalRuleValues>;
  screening: RuleSet<ScreeningRuleValues>;
  reconciliation: RuleSet<ReconciliationRuleValues>;
  processing: RuleSet<ProcessingRuleValues>;
}
