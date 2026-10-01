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
        StatsText.Text = $"Games: {_stats.GamesPlayed}   Best: {_stats.BestTotal:0.00} / {GameSession.Rounds * 10}";
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
    private void UpdateGuessSwatch() => GuessSwatch.Background = ToBrush(CurrentGuess);
    private void Slider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded) UpdateGuessSwatch();
    }

    private void Confirm_Click(object s, RoutedEventArgs e)
    {
        var r = _game.Submit(CurrentGuess);
        RevealTarget.Background = ToBrush(r.Target);
        RevealGuess.Background = ToBrush(r.Guess);
        ScoreText.Text = r.Score.ToString("0.00");
        DetailText.Text = $"Target H{r.Target.Hue:0} S{r.Target.Sat:0} B{r.Target.Bri:0}  ·  Yours H{r.Guess.Hue:0} S{r.Guess.Sat:0} B{r.Guess.Bri:0}  ·  ΔE {r.DeltaE:0.0}";
        NextBtn.Content = _game.IsFinished ? "See results" : "Next";
        ShowPanel(RevealPanel);
    }

    private void Next_Click(object s, RoutedEventArgs e)
    {
        if (!_game.IsFinished) { BeginRound(); return; }

        bool newBest = _game.Total > _stats.BestTotal;
        if (newBest) _stats.BestTotal = _game.Total;
        _stats.GamesPlayed++;
        _stats.Save();

        HeaderText.Text = "Results";
        FinalScore.Text = $"{_game.Total:0.00}";
        FinalDetail.Text = $"out of {GameSession.Rounds * 10}" + (newBest ? "\nNew personal best!" : "") +
                           "\n\n" + string.Join("  ·  ", _game.Results.Select(r => r.Score.ToString("0.0")));
        ShowPanel(FinalPanel);
    }

    private void Menu_Click(object s, RoutedEventArgs e) => ShowMenu();
}
