export enum CustomerType { Individual = 1, Business = 2 }
export enum CustomerStatus { Active = 1, Inactive = 2, Suspended = 3 }
export enum AccountStatus { Active = 1, Frozen = 2, Closed = 3 }

export interface CustomerSummary {
  id: string;
  customerReference: string;
  name: string;
  type: CustomerType;
  status: CustomerStatus;
  email?: string;
  countryCode?: string;
  accountCount: number;
  rowVersion: string;
}

export interface AccountSummary {
  id: string;
  maskedNumber: string;
  currency: string;
  status: AccountStatus;
  availableBalance: number;
  ledgerBalance: number;
  dailyLimit: number;
  rowVersion: string;
}

export interface CustomerDetail {
  id: string;
  customerReference: string;
  name: string;
  type: CustomerType;
  status: CustomerStatus;
  email?: string;
  phoneNumber?: string;
  countryCode?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
  accounts: AccountSummary[];
}

export interface CreateCustomerRequest {
  type: CustomerType;
  name: string;
  email?: string;
  phoneNumber?: string;
  countryCode?: string;
}
