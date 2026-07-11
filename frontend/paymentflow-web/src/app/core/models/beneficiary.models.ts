export enum BeneficiaryStatus { Draft = 1, PendingApproval = 2, Approved = 3, Rejected = 4 }

export interface Beneficiary {
  id: string;
  customerId: string;
  name: string;
  maskedNumber: string;
  bankName?: string;
  bankIdentifierCode?: string;
  currency: string;
  countryCode?: string;
  status: BeneficiaryStatus;
  reviewNotes?: string;
  reviewedAtUtc?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
}

export interface CreateBeneficiaryRequest {
  customerId: string;
  name: string;
  accountNumber: string;
  bankName?: string;
  bankIdentifierCode?: string;
  currency: string;
  countryCode?: string;
}
