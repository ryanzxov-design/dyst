// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — небольшая кнопка в рамке; при наведении заливается цветом люминофора.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaTerminalButton : BaseButton
{
    private const int TextSize = 14;
    private DystopiaLobbyThemeController? _theme;

    /// <summary>Часть ключа локали темы.</summary>
    public string? TextKey { get; set; }

    public string? Text { get; set; }

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    private string Caption()
    {
        var text = TextKey != null ? Term.Text(TextKey) : Text ?? string.Empty;
        return Term.Corrupt(text);
    }

    private int _version = -1;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_version == Term.Version)
            return;

        _version = Term.Version;
        InvalidateMeasure();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var font = Term.Font(false, TextSize);
        var text = TextKey != null ? Term.Text(TextKey) : Text ?? string.Empty;
        return new Vector2(DystopiaTerm.Measure(font, text, 1f, 1f) + 28f, 34f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var scale = UIScale;
        var size = PixelSize;
        var t = MathF.Max(1f, MathF.Round(scale));
        var disabled = DrawMode == DrawModeEnum.Disabled;
        var active = !disabled && DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;
        var edge = disabled ? theme.Dim : theme.Fg;

        if (active)
        {
            DystopiaTerm.Rect(handle, 0f, 0f, size.X, size.Y, theme.Fg);
        }
        else
        {
            DystopiaTerm.Rect(handle, 0f, 0f, size.X, t, edge);
            DystopiaTerm.Rect(handle, 0f, size.Y - t, size.X, size.Y, edge);
            DystopiaTerm.Rect(handle, 0f, 0f, t, size.Y, edge);
            DystopiaTerm.Rect(handle, size.X - t, 0f, size.X, size.Y, edge);
        }

        var font = theme.Font(false, TextSize);
        var text = Caption();
        var w = DystopiaTerm.Measure(font, text, scale, 1f);
        var pos = new Vector2((size.X - w) / 2f, (size.Y - DystopiaTerm.LineHeight(font, scale)) / 2f);
        if (active)
            DystopiaTerm.Plain(handle, font, pos, text, scale, DystopiaLobbyThemeController.Ink, 1f);
        else
            DystopiaTerm.Glowing(handle, theme, font, pos, text, scale, edge, 1f);
    }
}
