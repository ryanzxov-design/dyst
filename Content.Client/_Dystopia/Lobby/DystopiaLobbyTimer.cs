// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — большая кнопка «ГОТОВ К СМЕНЕ» под досье. Внутри — обычная кнопка готовности лобби;
// обёртка рисует надпись пиксельным шрифтом, подсказку с отсчётом до начала смены (мигающее двоеточие) и полосу
// из блоков по ширине кнопки. Записан — кнопка залита цветом люминофора. Во время смены — «вступить» и сколько
// смена уже идёт.

using System.Numerics;
using Content.Client.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Lobby;

/// <summary>Отсчёт смены: надпись, время и доля пройденного ожидания (−1 — полосы нет).</summary>
public sealed class DystopiaLobbyClock
{
    private double _longest;

    public void Read(ClientGameTicker ticker, IGameTiming timing, DystopiaLobbyThemeController theme,
        out string label, out string time, out float progress)
    {
        progress = -1f;
        if (ticker.IsGameStarted)
        {
            var round = timing.CurTime - ticker.RoundStartTimeSpan;
            label = theme.Text("timer-running");
            time = $"{(int) round.TotalHours:D2}:{round.Minutes:D2}";
            return;
        }

        if (ticker.Paused)
        {
            label = theme.Text("timer-paused");
            time = "--:--";
            return;
        }

        var left = (ticker.StartTime - timing.CurTime).TotalSeconds;
        if (left > _longest)
            _longest = left;

        if (left <= 0)
        {
            label = theme.Text("timer-soon");
            time = "00:00";
            progress = 1f;
            return;
        }

        label = theme.Text("timer");
        var span = TimeSpan.FromSeconds(left);
        time = span.TotalHours >= 1
            ? $"{(int) span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";
        progress = _longest > 0 ? (float) (1 - left / _longest) : 0f;
    }
}

public sealed partial class DystopiaLobbyReadyPanel : DystopiaButtonShell
{
    private const int CaptionSize = 22;
    private const int MinCaptionSize = 12;
    private const int TextSize = 15;
    private const float Pad = 18f;

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly DystopiaLobbyClock _clock = new();
    private ClientGameTicker? _ticker;

    public DystopiaLobbyReadyPanel()
    {
        IoCManager.InjectDependencies(this);
        MinHeight = 140f;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Button is not { } button)
            return;

        _ticker ??= _entities.SystemOrNull<ClientGameTicker>();
        var theme = Term;
        var s = UIScale;
        var size = new Vector2(PixelSize.X, PixelSize.Y);

        // no / yes / join — по ней берутся надписи темы (ready-no, ready-yes-hint…)
        var part = !button.ToggleMode ? "join" : button.Pressed ? "yes" : "no";
        var disabled = IsDisabled(button);
        var on = part == "yes" && !disabled;
        var hovered = !disabled && button.IsHovered;

        // корпус: заливка (записан), подсветка (наведение), рамка с мягким свечением внутрь
        var t = MathF.Max(2f, MathF.Round(2f * s));
        if (on)
        {
            DystopiaTerm.Rect(handle, 0f, 0f, size.X, size.Y, theme.Fg.WithAlpha(hovered ? 0.85f : 1f));
        }
        else
        {
            if (hovered)
                DystopiaTerm.Rect(handle, 0f, 0f, size.X, size.Y, theme.Fg.WithAlpha(0.14f));

            Border(handle, size, MathF.Round(6f * s), theme.Fg.WithAlpha(disabled ? 0.03f : 0.08f));
        }

        Border(handle, size, t, disabled ? theme.Dim.WithAlpha(0.6f) : theme.Fg);

        var ink = on ? DystopiaLobbyThemeController.Ink : disabled ? theme.Dim : theme.Fg;
        var glow = !on;
        var room = size.X - 2f * Pad * s;
        if (room <= 0f)
            return;

        // надпись: пиксельный шрифт, кегль уменьшается, пока строка не влезет
        var caption = theme.Corrupt(theme.Text($"ready-{part}"));
        var capSize = CaptionSize;
        var big = theme.Font(true, capSize);
        while (capSize > MinCaptionSize && DystopiaTerm.Measure(big, caption, s, 3f) > room)
        {
            capSize -= 2;
            big = theme.Font(true, capSize);
        }

        caption = DystopiaTerm.Fit(big, caption, s, room, 3f);

        // подсказка и отсчёт
        var hint = theme.Text($"ready-{part}-hint");
        var progress = -1f;
        var colonAt = -1;
        if (_ticker != null)
        {
            _clock.Read(_ticker, _timing, theme, out var label, out var time, out progress);
            var head = $"{hint}  ·  {label} ";
            var colon = time.IndexOf(':');
            colonAt = colon >= 0 ? head.Length + colon : -1;
            hint = head + time;
        }

        var small = theme.Font(false, TextSize);
        var full = theme.Corrupt(hint);
        var line = DystopiaTerm.Fit(small, full, s, room, 1f);
        if (line.Length != full.Length)
            colonAt = -1;

        // полоса — столько блоков, сколько влезает в ширину кнопки
        string? bar = null;
        if (progress >= 0f)
        {
            var block = DystopiaTerm.Measure(small, "█", s, 1f);
            var blocks = block > 0f ? Math.Clamp((int) (room / block) - 2, 6, 48) : 6;
            var filled = Math.Clamp((int) MathF.Round(progress * blocks), 0, blocks);
            bar = "[" + new string('█', filled) + new string('░', blocks - filled) + "]";
        }

        var capH = DystopiaTerm.LineHeight(big, s);
        var lineH = DystopiaTerm.LineHeight(small, s);
        var gap = 8f * s;
        var total = capH + gap + lineH + (bar != null ? gap + lineH : 0f);
        var y = MathF.Round((size.Y - total) / 2f);

        var capW = DystopiaTerm.Measure(big, caption, s, 3f);
        DystopiaTerm.Glowing(handle, theme, big, new Vector2((size.X - capW) / 2f, y), caption, s, ink, 3f, glow);
        y += capH + gap;

        // двоеточие отсчёта мигает: в погашенной фазе на его месте пробел той же ширины в моноширинном шрифте
        var lineW = DystopiaTerm.Measure(small, line, s, 1f);
        if (colonAt >= 0 && colonAt < line.Length && !theme.Blink)
            line = line[..colonAt] + " " + line[(colonAt + 1)..];

        DystopiaTerm.Glowing(handle, theme, small, new Vector2((size.X - lineW) / 2f, y), line, s,
            on ? ink : ink.WithAlpha(0.85f), 1f, glow);
        y += lineH + gap;

        if (bar == null)
            return;

        var barW = DystopiaTerm.Measure(small, bar, s, 1f);
        DystopiaTerm.Glowing(handle, theme, small, new Vector2((size.X - barW) / 2f, y), bar, s, ink, 1f, glow);
    }
}
