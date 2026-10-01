using System.IO;
using System.Text.Json;

namespace DialedApp;

public sealed record RoundResult(Hsb Target, Hsb Guess, double DeltaE, double Score);

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
    public double Total => _results.Sum(r => r.Score);

    // Daily mode uses a date-based seed so everyone gets the same five colours.
    public static GameSession Create(bool daily)
    {
        var now = DateTime.UtcNow;
        var rng = daily ? new Random(now.Year * 10000 + now.Month * 100 + now.Day) : new Random();
        var targets = Enumerable.Range(0, Rounds)
            .Select(_ => new Hsb(rng.Next(0, 360), rng.Next(15, 101), rng.Next(20, 101)))
            .ToArray();
        return new GameSession(targets);
    }

    public RoundResult Submit(Hsb guess)
    {
        double dE = ColorMath.DeltaE(CurrentTarget, guess);
        var result = new RoundResult(CurrentTarget, guess, dE, Math.Round(10 * Math.Exp(-dE / 25), 2));
        _results.Add(result);
        return result;
    }
}

public sealed class StatsStore
{
    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dialed", "stats.json");

    public double BestTotal { get; set; }
    public int GamesPlayed { get; set; }
    public string LastDaily { get; set; } = "";

    public static StatsStore Load()
    {
        try { return JsonSerializer.Deserialize<StatsStore>(File.ReadAllText(PathOnDisk)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
            File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(this));
        }
        catch { /* stats are best-effort */ }
    }
}
