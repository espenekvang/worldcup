using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using WorldCup.Api.Features;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/feature-flags")]
public class FeatureFlagsController(
    IFeatureManager featureManager,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Returns the evaluated state of every configured feature flag for the active group.
    /// The frontend calls this when the active group changes and stores the result in context.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, bool>>> GetFlags([FromQuery] Guid? groupId)
    {
        var resolvedGroupId = groupId
            ?? (Guid.TryParse(Request.Headers["X-Group-Id"].ToString(), out var headerId) ? headerId : Guid.Empty);

        var groupContext = new BettingGroupFeatureContext { GroupId = resolvedGroupId };
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in EnumerateFeatureNames())
        {
            // Use the contextual evaluation so the filter sees the explicit group id, not just the header.
            result[feature] = await featureManager.IsEnabledAsync(feature, groupContext);
        }

        return Ok(result);
    }

    private IEnumerable<string> EnumerateFeatureNames()
    {
        // Read the keys directly from configuration to include flags that are off for everyone.
        var section = configuration.GetSection("FeatureManagement");
        if (!section.Exists())
        {
            yield break;
        }

        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Key))
            {
                yield return child.Key;
            }
        }
    }
}
