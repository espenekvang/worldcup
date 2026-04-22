namespace WorldCup.Api.DTOs;

public record BettingGroupResponse(Guid Id, string Name, int MemberCount, DateTime CreatedAt);
public record BettingGroupDetailResponse(Guid Id, string Name, DateTime CreatedAt, List<BettingGroupMemberResponse> Members);
public record BettingGroupMemberResponse(Guid UserId, string Name, string Email, string? Picture, DateTime JoinedAt);
public record CreateBettingGroupRequest(string Name, bool JoinGroup = true);
public record UpdateBettingGroupRequest(string Name);
public record AddGroupMemberRequest(string Email);
