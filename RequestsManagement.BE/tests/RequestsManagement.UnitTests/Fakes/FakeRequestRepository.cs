using RequestsManagement.Dal.Interfaces;
using RequestsManagement.Dal.Models;
using RequestsManagement.Dto.Enums;
using RequestsManagement.Dto.Requests;

namespace RequestsManagement.UnitTests.Fakes;

/// <summary>
/// Repository בזיכרון לבדיקות BL. מממש את אותו חוזה כמו ה-repository האמיתי:
/// העדכון מותנה בגרסה ובסטטוס, ו-Version עולה ב-1.
/// </summary>
internal sealed class FakeRequestRepository : IRequestRepository
{
    private readonly Dictionary<string, RequestModel> _items = new();

    public int SearchCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public List<StatusAuditEntry> Audit { get; } = [];

    /// <summary>
    /// מדמה עדכון מתחרה שמנצח בין הקריאה של ה-service לכתיבה שלו:
    /// לפני הכתיבה הגרסה ב-"DB" מקודמת, ולכן העדכון המותנה לא תואם.
    /// </summary>
    public bool SimulateConcurrentWriteBeforeUpdate { get; set; }

    public RequestModel Add(RequestStatus status, int version = 0, string? id = null)
    {
        var model = new RequestModel
        {
            Id = id ?? MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Title = "Test request",
            OrganizationName = "Test org",
            Status = status,
            Priority = RequestPriority.Medium,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Version = version,
        };
        _items[model.Id] = model;
        return model;
    }

    public RequestModel Get(string id) => _items[id];

    public Task<(IReadOnlyList<RequestModel> Items, long TotalCount)> SearchAsync(
        RequestSearchRequest request,
        CancellationToken ct = default)
    {
        SearchCalls++;
        IReadOnlyList<RequestModel> items = _items.Values.ToList();
        return Task.FromResult((items, (long)items.Count));
    }

    public Task<RequestModel?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_items.TryGetValue(id, out var m) ? Clone(m) : null);

    public Task<IReadOnlyList<RequestModel>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RequestModel>>(
            ids.Where(_items.ContainsKey).Select(id => Clone(_items[id])).ToList());

    public Task<RequestModel?> UpdateStatusWithAuditAsync(
        string id,
        int expectedVersion,
        RequestStatus expectedCurrentStatus,
        RequestStatus newStatus,
        string changedBy,
        CancellationToken ct = default)
    {
        UpdateCalls++;

        if (!_items.TryGetValue(id, out var stored))
        {
            return Task.FromResult<RequestModel?>(null);
        }

        if (SimulateConcurrentWriteBeforeUpdate)
        {
            stored.Status = RequestStatus.Waiting;
            stored.Version++;
        }

        if (stored.Version != expectedVersion || stored.Status != expectedCurrentStatus)
        {
            return Task.FromResult<RequestModel?>(null);
        }

        stored.Status = newStatus;
        stored.UpdatedAt = DateTime.UtcNow;
        stored.Version++;
        Audit.Add(new StatusAuditEntry
        {
            RequestId = id,
            PreviousStatus = expectedCurrentStatus,
            NewStatus = newStatus,
            ChangedAt = stored.UpdatedAt,
            ChangedBy = changedBy,
        });

        return Task.FromResult<RequestModel?>(Clone(stored));
    }

    public Task<RequestCounts> GetCountsAsync(CancellationToken ct = default) =>
        Task.FromResult(new RequestCounts(
            _items.Values.GroupBy(i => i.Status).ToDictionary(g => g.Key, g => (long)g.Count()),
            _items.Values.GroupBy(i => i.Priority).ToDictionary(g => g.Key, g => (long)g.Count())));

    private static RequestModel Clone(RequestModel m) => new()
    {
        Id = m.Id,
        Title = m.Title,
        OrganizationName = m.OrganizationName,
        Status = m.Status,
        Priority = m.Priority,
        AssignedTo = m.AssignedTo,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Version = m.Version,
    };
}
