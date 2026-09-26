namespace Content.Shared._Dystopia.Visuals;

/// <summary>
/// Взгляд через маску: пока маска надета (и не приспущена), экран владельца окрашивается цветом стёкол.
/// Цвет слабый в центре экрана и сильнее к краям. Параметры задаются в прототипе маски.
/// Эффект рисует клиент (VisorOverlay).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaVisorComponent : Component
{
    /// <summary>Цвет стёкол.</summary>
    [DataField]
    public Color Tint = Color.White;

    /// <summary>Сила цвета в центре экрана, 0..1.</summary>
    [DataField]
    public float CenterStrength = 0.12f;

    /// <summary>Сила цвета у краёв экрана, 0..1.</summary>
    [DataField]
    public float EdgeStrength = 0.65f;

    /// <summary>Мутность стекла, 0..1.</summary>
    [DataField]
    public float Haze = 0.1f;

    /// <summary>Лёгкое затемнение у самых краёв (оправа), 0..1.</summary>
    [DataField]
    public float Darken = 0.15f;

    /// <summary>Перевод картинки в оттенки цвета стёкол (визор Стражи), 0..1.</summary>
    [DataField]
    public float Monochrome;

    /// <summary>Усиление резкости краёв, 0..1 (помогает читать детали в монохроме).</summary>
    [DataField]
    public float Sharpen;

    /// <summary>Подъём яркости монохромного визора (0 — яркость как без маски).</summary>
    [DataField]
    public float Brightness;

    /// <summary>Насколько монохром слабее в центре экрана, 0..1 (0 — одинаково везде).</summary>
    [DataField]
    public float CenterRelief;

    /// <summary>Горизонтальные полоски визора, 0..1 (0 — нет полосок).</summary>
    [DataField]
    public float Scanlines;

    /// <summary>Шаг полосок в пикселях экрана.</summary>
    [DataField]
    public float LinePeriod = 6f;

    /// <summary>Затемнение тёмной полоски, 0..1.</summary>
    [DataField]
    public float LineStrength = 0.3f;
}
