import { customerStatusDisplay, beneficiaryStatusDisplay, customerTypeLabel } from './status-maps';
import { CustomerStatus } from '../core/models/customer.models';
import { BeneficiaryStatus } from '../core/models/beneficiary.models';

describe('status-maps', () => {
  it('maps active customer to success tone', () => {
    expect(customerStatusDisplay(CustomerStatus.Active)).toEqual({ label: 'Active', tone: 'success' });
  });

  it('maps suspended customer to danger tone', () => {
    expect(customerStatusDisplay(CustomerStatus.Suspended).tone).toBe('danger');
  });

  it('maps pending beneficiary to warning tone', () => {
    expect(beneficiaryStatusDisplay(BeneficiaryStatus.PendingApproval)).toEqual({ label: 'Pending approval', tone: 'warning' });
  });

  it('labels customer type', () => {
    expect(customerTypeLabel(2)).toBe('Business');
    expect(customerTypeLabel(1)).toBe('Individual');
  });
});
