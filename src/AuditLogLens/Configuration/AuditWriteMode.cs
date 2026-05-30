namespace AuditLogLens.Configuration;

/// <summary>
/// Controls transaction handling for audit records created by the EF Core interceptor.
/// </summary>
public enum AuditWriteMode
{
    /// <summary>
    /// Writes audit records after the main save has completed.
    /// </summary>
    NonTransactional = 0,

    /// <summary>
    /// Writes business changes and audit records in one transaction when AuditLogLens owns the transaction.
    /// </summary>
    Transactional = 1
}
