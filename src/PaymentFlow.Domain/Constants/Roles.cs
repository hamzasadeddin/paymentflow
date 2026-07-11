namespace PaymentFlow.Domain.Constants;

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string OperationsAnalyst = "OperationsAnalyst";
    public const string PaymentApprover = "PaymentApprover";
    public const string ComplianceOfficer = "ComplianceOfficer";
    public const string ReadOnlyAuditor = "ReadOnlyAuditor";

    public static readonly IReadOnlyList<string> All =
    [
        Administrator, OperationsAnalyst, PaymentApprover, ComplianceOfficer, ReadOnlyAuditor
    ];
}
