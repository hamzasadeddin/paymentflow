export enum BreakType {
  MissingFromStatement = 1,
  MissingFromLedger = 2,
  AmountMismatch = 3
}

export enum BreakStatus {
  Open = 1,
  Resolved = 2,
  Ignored = 3
}

export interface ReconciliationRun {
  id: string;
  runReference: string;
  statementDateUtc: string;
  runByUserId?: string;
  matchedCount: number;
  breakCount: number;
  completedAtUtc: string;
  createdAtUtc: string;
}

export interface ReconciliationBreak {
  id: string;
  runId: string;
  type: BreakType;
  paymentId?: string;
  paymentReference?: string;
  statementReference?: string;
  ledgerAmount?: number;
  statementAmount?: number;
  currency: string;
  status: BreakStatus;
  resolvedByUserId?: string;
  resolvedAtUtc?: string;
  resolutionNotes?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
}
