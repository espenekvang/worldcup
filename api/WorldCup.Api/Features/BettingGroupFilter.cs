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
/// The group id is supplied via <see cref="BettingGroupFeatureContext"/> by the caller
/// (typically a controller that has already resolved the active group from the
/// <c>X-Group-Id</c> request header).
/// </summary>
/// <remarks>
/// This class deliberately implements only <see cref="IContextualFeatureFilter{T}"/>.
/// Newer versions of Microsoft.FeatureManagement reject a single filter class that
/// implements more than one feature filter interface, which previously crashed the
/// app on startup with: "A single feature filter cannot implement more than one
/// feature filter interface."
/// </remarks>
[FilterAlias("BettingGroup")]
public sealed class BettingGroupFilter : IContextualFeatureFilter<BettingGroupFeatureContext>
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context, BettingGroupFeatureContext appContext)
        => Task.FromResult(IsEnabled(context, appContext.GroupId));

    private static bool IsEnabled(FeatureFilterEvaluationContext context, Guid? groupId)
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
