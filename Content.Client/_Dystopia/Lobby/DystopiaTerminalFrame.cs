// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — рамка тонкой линией с заголовком, врезанным в верхнюю кромку. Содержимое — внутри
// с отступами (как у PanelContainer: все дети на одном прямоугольнике).

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaTerminalFrame : Control
{
    private const int TitleSize = 14;
    private DystopiaLobbyThemeController? _theme;

    /// <summary>Часть ключа локали темы для заголовка.</summary>
    public string? TitleKey { get; set; }

    public string? Title { get; set; }

    public float PadLeft { get; set; } = 18f;
    public float PadTop { get; set; } = 24f;
    public float PadRight { get; set; } = 18f;
    public float PadBottom { get; set; } = 14f;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    private string TitleText()
    {
        var text = TitleKey != null ? Term.Text(TitleKey) : Title ?? string.Empty;
        return Term.Corrupt(text);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var pad = new Vector2(PadLeft + PadRight, PadTop + PadBottom);
        var inner = Vector2.Max(Vector2.Zero, availableSize - pad);
        var size = Vector2.Zero;
        foreach (var child in Children)
        {
            child.Measure(inner);
            size = Vector2.Max(size, child.DesiredSize);
        }

        return size + pad;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var box = UIBox2.FromDimensions(PadLeft, PadTop,
            MathF.Max(0f, finalSize.X - PadLeft - PadRight), MathF.Max(0f, finalSize.Y - PadTop - PadBottom));
        foreach (var child in Children)
        {
            child.Arrange(box);
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        var font = Term.Font(false, TitleSize);
        var lineH = DystopiaTerm.LineHeight(font, scale);
        var top = MathF.Round(lineH / 2f);
        var size = PixelSize;
        var t = MathF.Max(1f, MathF.Round(scale));
        var dim = Term.Dim;

        var title = TitleText();
        var titleW = title.Length > 0 ? DystopiaTerm.Measure(font, title, scale, 2f) + 16f * scale : 0f;
        var gapStart = 14f * scale;

        // верхняя кромка с разрывом под заголовок
        if (titleW > 0f)
        {
            DystopiaTerm.Rect(handle, 0f, top, gapStart, top + t, dim);
            DystopiaTerm.Rect(handle, gapStart + titleW, top, size.X, top + t, dim);
        }
        else
        {
            DystopiaTerm.Rect(handle, 0f, top, size.X, top + t, dim);
        }

        DystopiaTerm.Rect(handle, 0f, size.Y - t, size.X, size.Y, dim);
        DystopiaTerm.Rect(handle, 0f, top, t, size.Y, dim);
        DystopiaTerm.Rect(handle, size.X - t, top, size.X, size.Y, dim);

        if (titleW > 0f)
            DystopiaTerm.Glowing(handle, Term, font, new Vector2(gapStart + 8f * scale, 0f), title, scale, Term.Fg, 2f);
    }
}
