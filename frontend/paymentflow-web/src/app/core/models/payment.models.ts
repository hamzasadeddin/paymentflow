export enum PaymentStatus {
  Draft = 1,
  PendingApproval = 2,
  Approved = 3,
  Processing = 4,
  Completed = 5,
  Failed = 6,
  Cancelled = 7,
  Rejected = 8
}

export interface Payment {
  id: string;
  paymentReference: string;
  sourceAccountId: string;
  sourceAccountMaskedNumber: string;
  beneficiaryId: string;
  beneficiaryName: string;
  amount: number;
  currency: string;
  status: PaymentStatus;
  description?: string;
  createdByUserId?: string;
  requiredApprovals: number;
  reviewNotes?: string;
  reviewedAtUtc?: string;
  failureReason?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
}

export interface CreatePaymentRequest {
  sourceAccountId: string;
  beneficiaryId: string;
  amount: number;
  currency: string;
  description?: string;
}
