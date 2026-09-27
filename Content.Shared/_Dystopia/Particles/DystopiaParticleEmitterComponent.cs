using System.Numerics;

namespace Content.Shared._Dystopia.Particles;

/// <summary>
/// Излучатель пиксельных частиц. Частицы — чисто визуальные, их рисует клиент (DystopiaParticleSystem).
/// На одной сущности может быть несколько излучателей (огонь + искры + дым).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaParticleEmitterComponent : Component
{
    [DataField]
    public List<DystopiaParticleEmitter> Emitters = new();

    /// <summary>Выключен ли излучатель (частицы перестают появляться, живые — доживают).</summary>
    [DataField]
    public bool Enabled = true;
}

/// <summary>Параметры одного излучателя частиц.</summary>
[DataDefinition]
public sealed partial class DystopiaParticleEmitter
{
    /// <summary>Сколько частиц в секунду.</summary>
    [DataField]
    public float Rate = 60f;

    /// <summary>Время жизни частицы, секунды: мин, макс.</summary>
    [DataField]
    public Vector2 Lifetime = new(0.3f, 0.6f);

    /// <summary>Собственная скорость частицы, клеток в секунду: мин, макс.</summary>
    [DataField]
    public Vector2 Speed = new(0.5f, 1.5f);

    /// <summary>Направление вылета, градусы (0 — вдоль движения источника, если AlignToVelocity, иначе на север).</summary>
    [DataField]
    public float Direction;

    /// <summary>Разброс направления, градусы (360 — во все стороны).</summary>
    [DataField]
    public float Spread = 360f;

    /// <summary>Направлять частицы по движению источника.</summary>
    [DataField]
    public bool AlignToVelocity = true;

    /// <summary>Какую долю скорости источника частица наследует.</summary>
    [DataField]
    public float InheritVelocity = 0.5f;

    /// <summary>Постоянное ускорение (снос), клеток/с².</summary>
    [DataField]
    public Vector2 Drift = Vector2.Zero;

    /// <summary>Торможение (чем больше, тем быстрее частица останавливается).</summary>
    [DataField]
    public float Drag = 2f;

    /// <summary>Разброс точки появления вокруг источника, клетки.</summary>
    [DataField]
    public float Jitter = 0.1f;

    /// <summary>Цвет по возрасту частицы (от рождения к смерти), с прозрачностью.</summary>
    [DataField]
    public List<Color> Colors = new() { Color.White };

    /// <summary>Размер в пикселях спрайта: при рождении, при смерти.</summary>
    [DataField]
    public Vector2 Size = new(2f, 1f);

    /// <summary>Светящееся (аддитивное) смешивание — для огня и искр. Выключить для дыма.</summary>
    [DataField]
    public bool Additive = true;

    /// <summary>Мерцание яркости, 0..1.</summary>
    [DataField]
    public float Flicker;

    // Рабочее поле: накопитель дробных частиц между кадрами.
    [ViewVariables]
    public float Accumulator;
}
