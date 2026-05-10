using Microsoft.FeatureManagement;

namespace WorldCup.Api.Features;

/// <summary>
/// Feature filter that enables a feature only for a configured set of betting groups (ligaer).
///
/// Configuration example (appsettings.json):
/// "FeatureManagement": {
///   "AiPredictions": {
///     "EnabledFor": [
///       {
///         "Name": "BettingGroup",
///         "Parameters": {
///           "GroupIds": [ "11111111-1111-1111-1111-111111111111" ]
///         }
///       }
///     ]
///   }
/// }
///
/// The group id is taken from <see cref="BettingGroupFeatureContext"/> when the caller passes
/// it explicitly (preferred), otherwise the filter falls back to reading the
/// <c>X-Group-Id</c> request header through <see cref="IHttpContextAccessor"/>.
/// </summary>
[FilterAlias("BettingGroup")]
public sealed class BettingGroupFilter(IHttpContextAccessor httpContextAccessor)
    : IContextualFeatureFilter<BettingGroupFeatureContext>, IFeatureFilter
{
    private const string GroupHeaderName = "X-Group-Id";

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context, BettingGroupFeatureContext appContext)
        => Task.FromResult(IsEnabled(context, appContext.GroupId));

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var groupId = ResolveGroupIdFromHttpContext();
        return Task.FromResult(IsEnabled(context, groupId));
    }

    private bool IsEnabled(FeatureFilterEvaluationContext context, Guid? groupId)
    {
        if (groupId is null || groupId == Guid.Empty)
        {
            return false;
        }

        var settings = context.Parameters.Get<BettingGroupFilterSettings>() ?? new BettingGroupFilterSettings();

        if (settings.GroupIds is null || settings.GroupIds.Count == 0)
        {
            return false;
        }

        foreach (var raw in settings.GroupIds)
        {
            if (Guid.TryParse(raw, out var allowed) && allowed == groupId)
            {
                return true;
            }
        }

        return false;
    }

    private Guid? ResolveGroupIdFromHttpContext()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null) return null;

        if (!http.Request.Headers.TryGetValue(GroupHeaderName, out var values))
        {
            return null;
        }

        var raw = values.ToString();
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }
}

public sealed class BettingGroupFilterSettings
{
    public List<string> GroupIds { get; set; } = [];
}

/// <summary>
/// Strongly-typed context object used when evaluating features programmatically for a specific group.
/// </summary>
public sealed class BettingGroupFeatureContext
{
    public Guid GroupId { get; init; }
}
