namespace WorldCup.Api.DTOs;

public record BettingGroupResponse(
    Guid Id,
    string Name,
    int MemberCount,
    DateTime CreatedAt,
    bool IsPaid,
    decimal EntryFee,
    decimal PrizePot,
    int PaidMemberCount,
    bool CurrentUserHasPaid);

public record BettingGroupDetailResponse(Guid Id, string Name, DateTime CreatedAt, List<BettingGroupMemberResponse> Members);

public record BettingGroupMemberResponse(
    Guid UserId,
    string Name,
    string Email,
    string? Picture,
    bool IsGroupAdmin,
    DateTime JoinedAt,
    bool HasPaid,
    DateTime? PaidAt);

public record CreateBettingGroupRequest(
    string Name,
    bool JoinGroup = true,
    bool IsPaid = false,
    decimal EntryFee = 0m);

public record UpdateBettingGroupRequest(string Name, bool? IsPaid = null, decimal? EntryFee = null);
public record AddGroupMemberRequest(string Email);
public record SetGroupAdminRequest(bool IsGroupAdmin);
public record SetMemberPaidRequest(bool HasPaid);
