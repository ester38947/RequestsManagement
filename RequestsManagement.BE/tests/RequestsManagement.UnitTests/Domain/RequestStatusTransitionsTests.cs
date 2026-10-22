using RequestsManagement.Dto.Enums;

namespace RequestsManagement.UnitTests.Domain;

public sealed class RequestStatusTransitionsTests
{
    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.InProgress)]
    [InlineData(RequestStatus.New, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Completed)]
    [InlineData(RequestStatus.Waiting, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Completed)]
    public void AllowedTransitions_AreValid(RequestStatus from, RequestStatus to)
    {
        Assert.True(RequestStatusTransitions.IsValid(from, to));
    }

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.New)]
    [InlineData(RequestStatus.New, RequestStatus.Completed)]
    [InlineData(RequestStatus.InProgress, RequestStatus.New)]
    [InlineData(RequestStatus.Waiting, RequestStatus.New)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Completed, RequestStatus.Completed)]
    public void OtherTransitions_AreRejected(RequestStatus from, RequestStatus to)
    {
        Assert.False(RequestStatusTransitions.IsValid(from, to));
    }

    [Fact]
    public void New_IsNeverAReachableTarget()
    {
        Assert.False(RequestStatusTransitions.IsReachableTarget(RequestStatus.New));
        Assert.True(RequestStatusTransitions.IsReachableTarget(RequestStatus.Completed));
    }
}
