namespace RequestsManagement.Dal.Indexes;

/// <summary>
/// Creates/ensures indexes at startup. Idempotent: recreating the same index does not fail.
/// </summary>
public interface IIndexInitializer
{
    Task EnsureIndexesAsync(CancellationToken ct = default);
}
