using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;

namespace WorldCup.Api.Controllers;

/// <summary>
/// Én gangs eksport av det som er verdt å ta vare på når VM er over: hvor mange poeng
/// hver spiller endte på, i hver liga. Leverer en selvstendig HTML-fil (all CSS inline)
/// som kan lagres og åpnes i en nettleser hvor som helst – uten database eller app.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminBackupController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("/api/admin/backup/overview")]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        // Poeng er globale per bruker (samme totalsum i alle ligaer spilleren er med i),
        // så vi aggregerer én gang per bruker og kobler det på hvert ligamedlemskap.
        var pointsByUser = await dbContext.Predictions
            .Where(p => p.Points != null)
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => (int?)p.Points) ?? 0,
                ScoredMatches = g.Count(),
            })
            .ToDictionaryAsync(x => x.UserId, x => x, ct);

        var scoredMatchCount = await dbContext.MatchResults.CountAsync(ct);

        var groups = await dbContext.BettingGroups
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.IsPaid, g.EntryFee })
            .ToListAsync(ct);

        // Ekte medlemmer per liga (systembrukere som «Dommeren» filtreres bort).
        var members = await dbContext.BettingGroupMembers
            .Where(m => !m.User.IsSystem)
            .Select(m => new
            {
                m.BettingGroupId,
                m.UserId,
                m.User.Name,
                m.HasPaid,
            })
            .ToListAsync(ct);

        var membersByGroup = members
            .GroupBy(m => m.BettingGroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var leagues = new List<LeagueStanding>();
        foreach (var group in groups)
        {
            var groupMembers = membersByGroup.GetValueOrDefault(group.Id) ?? [];

            var rows = groupMembers
                .Select(m =>
                {
                    var agg = pointsByUser.GetValueOrDefault(m.UserId);
                    return new StandingRow(
                        Name: m.Name,
                        TotalPoints: agg?.TotalPoints ?? 0,
                        ScoredMatches: agg?.ScoredMatches ?? 0,
                        HasPaid: m.HasPaid);
                })
                .OrderByDescending(r => r.TotalPoints)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Standard competition ranking (1-2-2-4): lik poengsum deler plassering.
            var ranked = new List<RankedStandingRow>();
            int rank = 0;
            int? lastPoints = null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (lastPoints is null || rows[i].TotalPoints != lastPoints)
                {
                    rank = i + 1;
                    lastPoints = rows[i].TotalPoints;
                }
                ranked.Add(new RankedStandingRow(rank, rows[i]));
            }

            leagues.Add(new LeagueStanding(group.Name, group.IsPaid, group.EntryFee, ranked));
        }

        var html = BuildHtml(leagues, scoredMatchCount);
        var fileName = $"vm-2026-backup-{DateTime.UtcNow:yyyy-MM-dd}.html";

        return File(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8", fileName);
    }

    private static string BuildHtml(IReadOnlyList<LeagueStanding> leagues, int scoredMatchCount)
    {
        var generatedAt = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm 'UTC'");
        var totalPlayers = leagues.Sum(l => l.Rows.Count);

        var sb = new StringBuilder();
        sb.Append("""
<!doctype html>
<html lang="no">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>VM 2026 – Sluttoppgjør</title>
<style>
  :root { color-scheme: light; }
  * { box-sizing: border-box; }
  body {
    margin: 0; padding: 2rem 1rem;
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    background: #f4f5f7; color: #1a1a2e; line-height: 1.5;
  }
  .wrap { max-width: 820px; margin: 0 auto; }
  header { text-align: center; margin-bottom: 2.5rem; }
  h1 { font-size: 1.9rem; margin: 0 0 .35rem; letter-spacing: -.02em; }
  .sub { color: #5c5f73; font-size: .95rem; }
  .meta { margin-top: .75rem; font-size: .8rem; color: #8a8da0; }
  .league { background: #fff; border: 1px solid #e2e4ee; border-radius: 14px; padding: 1.25rem 1.5rem 1.5rem; margin-bottom: 1.5rem; box-shadow: 0 1px 3px rgba(20,20,50,.04); }
  .league h2 { font-size: 1.25rem; margin: 0 0 .15rem; display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
  .badge { font-size: .7rem; font-weight: 600; padding: .15rem .5rem; border-radius: 999px; background: #eef2ff; color: #4338ca; }
  .badge.paid { background: #dcfce7; color: #15803d; }
  .count { font-size: .85rem; color: #8a8da0; margin: 0 0 1rem; }
  table { width: 100%; border-collapse: collapse; }
  th, td { text-align: left; padding: .55rem .5rem; }
  th { font-size: .72rem; text-transform: uppercase; letter-spacing: .04em; color: #8a8da0; border-bottom: 2px solid #eceef5; }
  td { border-bottom: 1px solid #f0f1f7; font-size: .95rem; }
  tr:last-child td { border-bottom: none; }
  .rank { width: 2.5rem; color: #8a8da0; font-variant-numeric: tabular-nums; }
  .pts { text-align: right; font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; }
  .matches { text-align: right; color: #8a8da0; font-variant-numeric: tabular-nums; width: 5rem; }
  .paid-yes { color: #15803d; } .paid-no { color: #b45309; }
  .top1 td { background: #fffbeb; } .top1 .rank::after { content: " 🥇"; }
  .top2 .rank::after { content: " 🥈"; } .top3 .rank::after { content: " 🥉"; }
  .empty { color: #8a8da0; font-style: italic; padding: .5rem; }
  footer { text-align: center; color: #a0a2b3; font-size: .78rem; margin-top: 2rem; }
  @media print { body { background: #fff; } .league { box-shadow: none; break-inside: avoid; } }
</style>
</head>
<body>
<div class="wrap">
<header>
  <h1>⚽ VM 2026 – Sluttoppgjør</h1>
""");
        sb.Append("  <div class=\"sub\">Poeng per spiller i hver liga</div>\n");
        sb.Append($"  <div class=\"meta\">{totalPlayers} spillerplasser · {leagues.Count} ligaer · {scoredMatchCount} kamper med resultat · generert {Enc(generatedAt)}</div>\n");
        sb.Append("</header>\n");

        if (leagues.Count == 0)
        {
            sb.Append("<p class=\"empty\">Ingen ligaer funnet.</p>\n");
        }

        foreach (var league in leagues)
        {
            sb.Append("<section class=\"league\">\n");
            sb.Append("  <h2>").Append(Enc(league.Name));
            if (league.IsPaid)
            {
                sb.Append($" <span class=\"badge paid\">Betalt · {league.EntryFee:0.##} kr</span>");
            }
            sb.Append("</h2>\n");
            sb.Append($"  <p class=\"count\">{league.Rows.Count} {(league.Rows.Count == 1 ? "deltaker" : "deltakere")}</p>\n");

            if (league.Rows.Count == 0)
            {
                sb.Append("  <p class=\"empty\">Ingen deltakere.</p>\n</section>\n");
                continue;
            }

            sb.Append("  <table>\n    <thead><tr>");
            sb.Append("<th class=\"rank\">#</th><th>Spiller</th>");
            if (league.IsPaid) sb.Append("<th>Betalt</th>");
            sb.Append("<th class=\"matches\">Kamper</th><th class=\"pts\">Poeng</th>");
            sb.Append("</tr></thead>\n    <tbody>\n");

            foreach (var r in league.Rows)
            {
                var cls = r.Rank switch { 1 => " class=\"top1\"", 2 => " class=\"top2\"", 3 => " class=\"top3\"", _ => "" };
                sb.Append("      <tr").Append(cls).Append('>');
                sb.Append($"<td class=\"rank\">{r.Rank}</td>");
                sb.Append("<td>").Append(Enc(r.Row.Name)).Append("</td>");
                if (league.IsPaid)
                {
                    sb.Append(r.Row.HasPaid
                        ? "<td class=\"paid-yes\">Ja</td>"
                        : "<td class=\"paid-no\">Nei</td>");
                }
                sb.Append($"<td class=\"matches\">{r.Row.ScoredMatches}</td>");
                sb.Append($"<td class=\"pts\">{r.Row.TotalPoints}</td>");
                sb.Append("</tr>\n");
            }

            sb.Append("    </tbody>\n  </table>\n</section>\n");
        }

        sb.Append("<footer>FIFA World Cup 2026 Tipping – backup av sluttresultater.</footer>\n");
        sb.Append("</div>\n</body>\n</html>\n");

        return sb.ToString();
    }

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private sealed record StandingRow(string Name, int TotalPoints, int ScoredMatches, bool HasPaid);

    private sealed record RankedStandingRow(int Rank, StandingRow Row);

    private sealed record LeagueStanding(string Name, bool IsPaid, decimal EntryFee, IReadOnlyList<RankedStandingRow> Rows);
}
