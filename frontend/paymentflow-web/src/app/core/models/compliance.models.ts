export enum ComplianceCategory {
  Sanctions = 1,
  Limit = 2,
  Manual = 3
}

export enum ComplianceCaseStatus {
  Open = 1,
  Cleared = 2,
  Rejected = 3
}

export interface ComplianceCase {
  id: string;
  paymentId: string;
  paymentReference: string;
  sourceAccountId: string;
  sourceAccountMaskedNumber: string;
  beneficiaryName: string;
  amount: number;
  currency: string;
  category: ComplianceCategory;
  reason: string;
  raisedByUserId?: string;
  status: ComplianceCaseStatus;
  reviewedByUserId?: string;
  reviewedAtUtc?: string;
  reviewNotes?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
}
