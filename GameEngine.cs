using System.IO;
using System.Text.Json;

namespace Tinto;

public sealed record RoundResult(Hsb Target, Hsb Guess, double DeltaE, double ScorePercent);

public sealed class GameSession
{
    public const int Rounds = 5;

    private readonly Hsb[] _targets;
    private readonly List<RoundResult> _results = new();

    private GameSession(Hsb[] targets) => _targets = targets;

    public int Index => _results.Count;
    public bool IsFinished => _results.Count == Rounds;
    public Hsb CurrentTarget => _targets[Index];
    public IReadOnlyList<RoundResult> Results => _results;

    // Sum is retained for internal bookkeeping; use AverageScorePercent for the player's final grade.
    public double Total => _results.Sum(r => r.ScorePercent);
    public double AverageScorePercent =>
        _results.Count == 0 ? 0.0 : _results.Average(r => r.ScorePercent);

    // Daily mode uses a date-based seed so everyone gets the same five colours.
    public static GameSession Create(bool daily)
    {
        var now = DateTime.UtcNow;
        var rng = daily
            ? new Random(now.Year * 10000 + now.Month * 100 + now.Day)
            : new Random();

        var targets = Enumerable.Range(0, Rounds)
            .Select(_ => new Hsb(
                rng.Next(0, 360),
                rng.Next(15, 101),
                rng.Next(20, 101)))
            .ToArray();

        return new GameSession(targets);
    }

    public RoundResult Submit(Hsb guess)
    {
        double deltaE = ColorMath.DeltaE(CurrentTarget, guess);
        double scorePercent = ColorMath.ScorePercentFromDeltaE(deltaE);

        var result = new RoundResult(
            CurrentTarget,
            guess,
            deltaE,
            scorePercent);

        _results.Add(result);
        return result;
    }
}

public sealed class StatsStore
{
    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tinto",
        "stats.json");

    public int Version { get; set; } = 2;
    public double BestTotal { get; set; }
    public int GamesPlayed { get; set; }
    public string LastDaily { get; set; } = "";

    public static StatsStore Load()
    {
        try
        {
            var stats = JsonSerializer.Deserialize<StatsStore>(
                File.ReadAllText(PathOnDisk)) ?? new();

            // Version 1 stored the sum of five 0-10 round scores (0-50).
            // Convert that legacy value once to the new 0-100 percentage scale.
            if (stats.Version < 2)
            {
                stats.BestTotal = Math.Clamp(stats.BestTotal * 2.0, 0.0, 100.0);
                stats.Version = 2;
            }

            return stats;
        }
        catch
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Version = 2;
            Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
            File.WriteAllText(
                PathOnDisk,
                JsonSerializer.Serialize(this));
        }
        catch
        {
            // Statistics are non-critical and should never block the game.
        }
    }
}
