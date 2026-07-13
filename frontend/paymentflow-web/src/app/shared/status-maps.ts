import { AccountStatus, CustomerStatus } from '../core/models/customer.models';
import { BeneficiaryStatus } from '../core/models/beneficiary.models';
import { PaymentStatus } from '../core/models/payment.models';
import { ComplianceCaseStatus, ComplianceCategory } from '../core/models/compliance.models';
import { BreakStatus, BreakType } from '../core/models/reconciliation.models';

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';
interface Display { label: string; tone: Tone; }

export const customerStatusDisplay = (s: CustomerStatus): Display => ({
  [CustomerStatus.Active]: { label: 'Active', tone: 'success' as Tone },
  [CustomerStatus.Inactive]: { label: 'Inactive', tone: 'neutral' as Tone },
  [CustomerStatus.Suspended]: { label: 'Suspended', tone: 'danger' as Tone }
}[s]);

export const accountStatusDisplay = (s: AccountStatus): Display => ({
  [AccountStatus.Active]: { label: 'Active', tone: 'success' as Tone },
  [AccountStatus.Frozen]: { label: 'Frozen', tone: 'warning' as Tone },
  [AccountStatus.Closed]: { label: 'Closed', tone: 'neutral' as Tone }
}[s]);

export const beneficiaryStatusDisplay = (s: BeneficiaryStatus): Display => ({
  [BeneficiaryStatus.Draft]: { label: 'Draft', tone: 'neutral' as Tone },
  [BeneficiaryStatus.PendingApproval]: { label: 'Pending approval', tone: 'warning' as Tone },
  [BeneficiaryStatus.Approved]: { label: 'Approved', tone: 'success' as Tone },
  [BeneficiaryStatus.Rejected]: { label: 'Rejected', tone: 'danger' as Tone }
}[s]);

export const paymentStatusDisplay = (s: PaymentStatus): Display => ({
  [PaymentStatus.Draft]: { label: 'Draft', tone: 'neutral' as Tone },
  [PaymentStatus.PendingApproval]: { label: 'Pending approval', tone: 'warning' as Tone },
  [PaymentStatus.Approved]: { label: 'Approved', tone: 'info' as Tone },
  [PaymentStatus.Processing]: { label: 'Processing', tone: 'info' as Tone },
  [PaymentStatus.Completed]: { label: 'Completed', tone: 'success' as Tone },
  [PaymentStatus.Failed]: { label: 'Failed', tone: 'danger' as Tone },
  [PaymentStatus.Cancelled]: { label: 'Cancelled', tone: 'neutral' as Tone },
  [PaymentStatus.Rejected]: { label: 'Rejected', tone: 'danger' as Tone }
}[s]);

export const customerTypeLabel = (t: number): string => (t === 2 ? 'Business' : 'Individual');

export const complianceStatusDisplay = (s: ComplianceCaseStatus): Display => ({
  [ComplianceCaseStatus.Open]: { label: 'Open', tone: 'warning' as Tone },
  [ComplianceCaseStatus.Cleared]: { label: 'Cleared', tone: 'success' as Tone },
  [ComplianceCaseStatus.Rejected]: { label: 'Rejected', tone: 'danger' as Tone }
}[s]);

export const complianceCategoryLabel = (c: ComplianceCategory): string => ({
  [ComplianceCategory.Sanctions]: 'Sanctions',
  [ComplianceCategory.Limit]: 'Limit',
  [ComplianceCategory.Manual]: 'Manual'
}[c]);

export const breakStatusDisplay = (s: BreakStatus): Display => ({
  [BreakStatus.Open]: { label: 'Open', tone: 'warning' as Tone },
  [BreakStatus.Resolved]: { label: 'Resolved', tone: 'success' as Tone },
  [BreakStatus.Ignored]: { label: 'Ignored', tone: 'neutral' as Tone }
}[s]);

export const breakTypeLabel = (t: BreakType): string => ({
  [BreakType.MissingFromStatement]: 'Missing from statement',
  [BreakType.MissingFromLedger]: 'Missing from ledger',
  [BreakType.AmountMismatch]: 'Amount mismatch'
}[t]);
