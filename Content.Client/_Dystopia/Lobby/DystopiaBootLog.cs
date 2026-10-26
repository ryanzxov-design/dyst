// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — журнал загрузки узла: строки темы печатаются по одной (статус справа), потом
// приглашение с мигающим курсором. При входе в лобби и при смене темы печатается заново.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaBootLog : Control
{
    private const int TextSize = 13;
    private const float LineDelay = 0.38f;
    private DystopiaLobbyThemeController? _theme;
    private string _themeId = string.Empty;
    private float _start = -1f;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaBootLog()
    {
        MinHeight = 156f;
        RectClipContent = true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _start = -1f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_start < 0f || _themeId != Term.Theme.ID)
        {
            _themeId = Term.Theme.ID;
            _start = Term.Time;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var scale = UIScale;
        var font = theme.Font(false, TextSize);
        var line = DystopiaTerm.LineHeight(font, scale);
        var lines = theme.Theme.Boot;
        var shown = Math.Min(lines.Count, (int) ((theme.Time - _start) / LineDelay));
        var y = 0f;

        for (var i = 0; i < shown; i++)
        {
            var boot = lines[i];
            DystopiaTerm.Glowing(handle, theme, font, new Vector2(0f, y), theme.Corrupt(Loc.GetString(boot.Text)), scale, theme.Fg.WithAlpha(0.7f));
            string? status = null;
            var color = theme.Fg;
            if (boot.Bad is { } bad)
            {
                status = Loc.GetString(bad);
                color = theme.Alert;
            }
            else if (boot.Ok is { } ok)
            {
                status = Loc.GetString(ok);
            }

            if (status != null)
            {
                status = $"[ {status} ]";
                var w = DystopiaTerm.Measure(font, status, scale);
                DystopiaTerm.Glowing(handle, theme, font, new Vector2(PixelSize.X - w - 4f * scale, y), status, scale, color);
            }

            y += line;
        }

        if (shown < lines.Count)
            return;

        var ready = theme.Corrupt(theme.Text("booted"));
        var x = DystopiaTerm.Glowing(handle, theme, font, new Vector2(0f, y), ready, scale, theme.Fg);
        if (theme.Blink)
            DystopiaTerm.Plain(handle, font, new Vector2(x, y), "█", scale, theme.Fg);
    }
}
