// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — надпись люминофором. Текст — часть ключа темы (TextKey), готовая строка (Text) или
// функция (Getter). Цвет — роль в палитре темы; у повреждённого терминала буквы портятся (если Corrupt).

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Lobby;

public enum DystopiaTermColor : byte
{
    Fg,
    Dim,
    Alert,
}

public sealed class DystopiaTermLabel : Control
{
    private DystopiaLobbyThemeController? _theme;
    private string _shown = string.Empty;

    /// <summary>Часть ключа локали темы (dystopia-lobby-&lt;тема&gt;-&lt;TextKey&gt;).</summary>
    public string? TextKey { get; set; }

    /// <summary>Готовая строка (если нет TextKey).</summary>
    public string? Text { get; set; }

    /// <summary>Строка, которую берут каждый кадр (приоритетнее всего).</summary>
    public Func<string>? Getter { get; set; }

    public int FontSize { get; set; } = 15;
    public bool Pixel { get; set; }
    public DystopiaTermColor Tone { get; set; } = DystopiaTermColor.Fg;
    public bool Glow { get; set; } = true;
    public bool Pulse { get; set; }
    public bool Corrupt { get; set; } = true;
    public bool Center { get; set; }

    /// <summary>Разрядка между буквами, точек.</summary>
    public float Spacing { get; set; }

    /// <summary>Не требовать ширину под текст: длинный обрезается многоточием.</summary>
    public bool Clip { get; set; }

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    private string Source()
    {
        if (Getter != null)
            return Getter();

        if (TextKey != null)
            return Term.Text(TextKey);

        return Text ?? string.Empty;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var text = Source();
        if (Corrupt)
            text = Term.Corrupt(text);

        if (text.Length != _shown.Length && !Clip)
            InvalidateMeasure();

        _shown = text;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var font = Term.Font(Pixel, FontSize);
        var text = _shown.Length > 0 ? _shown : Source();
        var height = DystopiaTerm.LineHeight(font, 1f);
        if (Clip)
            return new Vector2(0f, height);

        return new Vector2(DystopiaTerm.Measure(font, text, 1f, Spacing) + 2f, height);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (_shown.Length == 0)
            return;

        var font = Term.Font(Pixel, FontSize);
        var scale = UIScale;
        var color = Tone switch
        {
            DystopiaTermColor.Dim => Term.Dim,
            DystopiaTermColor.Alert => Term.Alert,
            _ => Term.Fg,
        };

        if (Pulse)
            color = color.WithAlpha(color.A * Term.Pulse);

        var text = Clip ? DystopiaTerm.Fit(font, _shown, scale, PixelSize.X, Spacing) : _shown;
        var x = 0f;
        if (Center)
            x = MathF.Max(0f, (PixelSize.X - DystopiaTerm.Measure(font, text, scale, Spacing)) / 2f);

        var y = MathF.Max(0f, (PixelSize.Y - DystopiaTerm.LineHeight(font, scale)) / 2f);
        DystopiaTerm.Glowing(handle, Term, font, new Vector2(x, y), text, scale, color, Spacing, Glow);
    }
}
