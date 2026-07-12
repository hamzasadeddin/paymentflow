export enum ApprovalSubjectType {
  Payment = 1,
  Beneficiary = 2
}

export enum ApprovalOutcome {
  Approved = 1,
  Rejected = 2
}

export interface ApprovalQueueItem {
  subjectType: ApprovalSubjectType;
  subjectId: string;
  reference: string;
  title: string;
  amount?: number;
  currency: string;
  makerUserId?: string;
  makerEmail?: string;
  requiredApprovals: number;
  approvalsReceived: number;
  createdAtUtc: string;
}

export interface ApprovalQueue {
  payments: ApprovalQueueItem[];
  beneficiaries: ApprovalQueueItem[];
}

export interface ApprovalDecision {
  id: string;
  approverUserId: string;
  approverEmail?: string;
  decision: ApprovalOutcome;
  notes?: string;
  decidedAtUtc: string;
}
