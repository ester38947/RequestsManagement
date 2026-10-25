namespace RequestsManagement.Dal.Seed;

/// <summary>
/// Seeds demo data into the requests collection in bulk. Safe to run repeatedly.
/// </summary>
public interface ISeeder
{
    /// <summary>Inserts <paramref name="count"/> new requests and returns how many were actually created.</summary>
    Task<long> SeedRequestsAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// Deletes all requests and status history so the next seed recreates the test data from scratch.
    /// Indexes are preserved (DeleteMany, not Drop).
    /// </summary>
    Task ResetAsync(CancellationToken ct = default);

    /// <summary>Returns the number of requests currently stored.</summary>
    Task<long> CountRequestsAsync(CancellationToken ct = default);
}
