// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: кардиограмма стражника во вкладке «Дозор» — состояние по датчику маски без слов. Норма — ровный пульс,
// ранен — чаще, тяжёлое — слабые неровные частые удары, мёртв — прямая, маска снята — шум и «нет сигнала».
// Flash — вспышка на каждом ударе: по ней мигает метка стражника на карте.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Registry;

public sealed class CityHeartbeatControl : Control
{
    [Dependency] private IRobustRandom _random = default!;

    private const int Samples = 110;

    /// <summary>Сколько точек кардиограммы проходит за секунду.</summary>
    private const float Speed = 60f;

    private readonly float[] _samples = new float[Samples];
    private float _phase;
    private float _carry;
    private CityGuardHealth _health = CityGuardHealth.NoData;

    /// <summary>Вспышка удара: 1 — только что ударило, к 0 — гаснет.</summary>
    public float Flash { get; private set; }

    public CityGuardHealth Health
    {
        get => _health;
        set => _health = value;
    }

    public CityHeartbeatControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(110, 28);
        SetSize = new Vector2(110, 28);
        _phase = _random.NextFloat();
    }

    public static Color ColorOf(CityGuardHealth health)
    {
        return health switch
        {
            CityGuardHealth.Ok => CityUi.Accent,
            CityGuardHealth.Hurt => Color.FromHex("#E8B84E"),
            CityGuardHealth.Critical => Color.FromHex("#FF5A4E"),
            CityGuardHealth.Dead => CityUi.Dim,
            _ => CityUi.Muted,
        };
    }

    private float Bpm => _health switch
    {
        CityGuardHealth.Ok => 72f,
        CityGuardHealth.Hurt => 110f,
        CityGuardHealth.Critical => 150f * (0.7f + _random.NextFloat() * 0.6f),
        _ => 0f,
    };

    /// <summary>Форма одного удара (QRS и волна T), фаза 0..1.</summary>
    private float Beat(float phase)
    {
        if (_health is CityGuardHealth.Dead or CityGuardHealth.NoData)
            return 0f;

        var x = phase % 1f;
        var a = _health == CityGuardHealth.Critical ? 0.45f : 1f;
        if (x < 0.05f)
            return -0.15f * a;
        if (x < 0.09f)
            return 1f * a;
        if (x < 0.13f)
            return -0.45f * a;
        if (x > 0.3f && x < 0.42f)
            return 0.22f * a * MathF.Sin((x - 0.3f) / 0.12f * MathF.PI);
        return 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var dt = MathF.Min(args.DeltaSeconds, 0.1f);
        var before = _phase % 1f;
        _phase += Bpm / 60f * dt;
        Flash = _phase % 1f < before && Bpm > 0 ? 1f : MathF.Max(0f, Flash - dt * 3f);

        _carry += dt * Speed;
        while (_carry >= 1f)
        {
            _carry -= 1f;
            Array.Copy(_samples, 1, _samples, 0, Samples - 1);
            _samples[^1] = _health == CityGuardHealth.NoData
                ? (_random.NextFloat() - 0.5f) * 0.18f
                : Beat(_phase) + (_health == CityGuardHealth.Critical ? (_random.NextFloat() - 0.5f) * 0.1f : 0f);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), CityUi.Bg);
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), CityUi.Line, false);

        var color = ColorOf(_health);
        if (_health == CityGuardHealth.NoData)
            color = color.WithAlpha(0.5f);

        var mid = size.Y * 0.55f;
        var amp = size.Y * 0.4f;
        var step = size.X / (float) (Samples - 1);
        var prev = new Vector2(0, mid - _samples[0] * amp);
        for (var i = 1; i < Samples; i++)
        {
            var point = new Vector2(i * step, mid - _samples[i] * amp);
            handle.DrawLine(prev, point, color);
            prev = point;
        }

        if (_health == CityGuardHealth.NoData)
        {
            var text = Loc.GetString("dystopia-dozor-no-signal");
            var font = CityUi.Regular(9);
            var width = 0f;
            foreach (var rune in text.EnumerateRunes())
            {
                width += font.GetCharMetrics(rune, UIScale)?.Advance ?? 0;
            }
            handle.DrawString(font, new Vector2((size.X - width) / 2, size.Y * 0.2f), text, CityUi.Dim);
        }
    }
}
