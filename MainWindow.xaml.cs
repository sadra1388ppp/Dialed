using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DialedApp;

public partial class MainWindow : Window
{
    private readonly StatsStore _stats = StatsStore.Load();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private GameSession _game = null!;
    private bool _isDaily;
    private int _countdown;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        ShowMenu();
    }

    private static Brush ToBrush(Hsb c)
    {
        var (r, g, b) = c.ToRgb();
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private void ShowPanel(Panel active)
    {
        foreach (var p in new Panel[] { MenuPanel, MemorizePanel, GuessPanel, RevealPanel, FinalPanel })
            p.Visibility = p == active ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowMenu()
    {
        HeaderText.Text = "";
        StatsText.Text = $"Games: {_stats.GamesPlayed}   Best: {_stats.BestTotal:0.0}%";
        ShowPanel(MenuPanel);
    }

    private void Solo_Click(object s, RoutedEventArgs e) => Start(false);

    private void Daily_Click(object s, RoutedEventArgs e)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (_stats.LastDaily == today)
        {
            MessageBox.Show("You already played today's daily. Come back tomorrow!", "Dialed");
            return;
        }
        _stats.LastDaily = today;
        _stats.Save();
        Start(true);
    }

    private void Start(bool daily)
    {
        _isDaily = daily;
        _game = GameSession.Create(daily);
        BeginRound();
    }

    private void BeginRound()
    {
        HeaderText.Text = $"{(_isDaily ? "Daily" : "Solo")} · Round {_game.Index + 1}/{GameSession.Rounds}";
        TargetSwatch.Background = ToBrush(_game.CurrentTarget);
        _countdown = 3;
        CountdownText.Text = _countdown.ToString();
        ShowPanel(MemorizePanel);
        _timer.Start();
    }

    private void Timer_Tick(object? s, EventArgs e)
    {
        if (--_countdown > 0) { CountdownText.Text = _countdown.ToString(); return; }
        _timer.Stop();
        HueSlider.Value = 180; SatSlider.Value = 50; BriSlider.Value = 50;
        UpdateGuessSwatch();
        ShowPanel(GuessPanel);
    }

    private Hsb CurrentGuess => new(HueSlider.Value, SatSlider.Value, BriSlider.Value);
    private void UpdateGuessSwatch()
    {
        if (!IsLoaded)
            return;

        var guess = CurrentGuess;

        GuessSwatch.Background = ToBrush(guess);
        HueValueText.Text = $"{guess.Hue:0}°";
        SatValueText.Text = $"{guess.Sat:0}%";
        BriValueText.Text = $"{guess.Bri:0}%";
        LiveColorHexText.Text = ToHex(guess);
        LiveColorSubText.Text = $"H {guess.Hue:0}° · S {guess.Sat:0}% · B {guess.Bri:0}%";

        UpdateControlGradients(guess);
    }

    private void Slider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateGuessSwatch();
    }

    private void UpdateControlGradients(Hsb guess)
    {
        var hue = ToRgbColor(guess);

        SatSlider.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(255, 255, 255), 0),
                new GradientStop(hue, 1)
            }
        };

        BriSlider.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0, 0, 0), 0),
                new GradientStop(hue, 1)
            }
        };
    }

    private static Color ToRgbColor(Hsb c)
    {
        var (r, g, b) = c.ToRgb();
        return Color.FromRgb(r, g, b);
    }

    private static string ToHex(Hsb c)
    {
        var (r, g, b) = c.ToRgb();
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private void Confirm_Click(object s, RoutedEventArgs e)
    {
        var r = _game.Submit(CurrentGuess);
        RevealTarget.Background = ToBrush(r.Target);
        RevealGuess.Background = ToBrush(r.Guess);
        ScoreText.Text = $"{r.ScorePercent:0.0}%";
        DetailText.Text = $"Accuracy {r.ScorePercent:0.0}%  ·  CIEDE2000 ΔE {r.DeltaE:0.00}";
        NextBtn.Content = _game.IsFinished ? "See results" : "Next";
        ShowPanel(RevealPanel);
    }

    private void Next_Click(object s, RoutedEventArgs e)
    {
        if (!_game.IsFinished) { BeginRound(); return; }

        bool newBest = _game.AverageScorePercent > _stats.BestTotal;
        if (newBest) _stats.BestTotal = _game.AverageScorePercent;
        _stats.GamesPlayed++;
        _stats.Save();

        HeaderText.Text = "Results";
        FinalScore.Text = $"{_game.AverageScorePercent:0.0}%";
        FinalDetail.Text = $"Average accuracy · CIEDE2000 perceptual scoring" + (newBest ? "\nNew personal best!" : "") +
                           "\n\n" + string.Join("  ·  ", _game.Results.Select(r => $"{r.ScorePercent:0.0}%"));
        ShowPanel(FinalPanel);
    }

    private void Menu_Click(object s, RoutedEventArgs e) => ShowMenu();
}
