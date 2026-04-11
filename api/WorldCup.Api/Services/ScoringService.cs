namespace WorldCup.Api.Services;

public sealed class ScoringService
{
    public int CalculatePoints(int predictedHome, int predictedAway, int actualHome, int actualAway)
    {
        var predictedOutcome = GetOutcome(predictedHome, predictedAway);
        var actualOutcome = GetOutcome(actualHome, actualAway);

        var points = 0;

        if (predictedOutcome == actualOutcome)
        {
            points += 2;
        }

        if (predictedHome == actualHome)
        {
            points += 1;
        }

        if (predictedAway == actualAway)
        {
            points += 1;
        }

        return points;
    }

    private static int GetOutcome(int homeGoals, int awayGoals)
    {
        if (homeGoals > awayGoals)
        {
            return 1;
        }

        if (homeGoals < awayGoals)
        {
            return -1;
        }

        return 0;
    }
}
