// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: фиксированные многоблоки (дуговая печь, градирни, цистерны). Строятся по клеткам секциями каркаса —
// секции соединяются текстурами (IconSmooth). Когда из секций одного ключа сложился ровно нужный прямоугольник
// (строго W×H или, если разрешено, H×W) и в центре стоит узел (если нужен), секции исчезают, а в центре появляется
// готовая машина. Разборка машины возвращает её обратно в секции.

using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Industry;

/// <summary>Секция каркаса многоблока.</summary>
[RegisterComponent]
public sealed partial class DystopiaMultiblockFrameComponent : Component
{
    /// <summary>Ключ многоблока: собираются только секции с одинаковым ключом.</summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    /// <summary>Ширина × высота в клетках (ширина — по X).</summary>
    [DataField(required: true)]
    public int Width = 3;

    [DataField(required: true)]
    public int Height = 3;

    /// <summary>Что появляется из прямоугольника Width×Height.</summary>
    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>Что появляется из повёрнутого прямоугольника Height×Width (null — так не собирается).</summary>
    [DataField]
    public EntProtoId? ResultRotated;

    /// <summary>Это узел многоблока (ставится в центр).</summary>
    [DataField]
    public bool Core;

    /// <summary>Нужен ли узел в центре.</summary>
    [DataField]
    public bool RequireCore;
}

/// <summary>Готовый многоблок: из чего он собран (для разборки обратно в секции).</summary>
[RegisterComponent]
public sealed partial class DystopiaMultiblockComponent : Component
{
    /// <summary>Секция каркаса.</summary>
    [DataField(required: true)]
    public EntProtoId Frame;

    /// <summary>Узел в центре (null — все секции одинаковые).</summary>
    [DataField]
    public EntProtoId? CoreFrame;

    /// <summary>Размер в клетках, как стоит (ширина — по X).</summary>
    [DataField(required: true)]
    public int Width = 3;

    [DataField(required: true)]
    public int Height = 3;

    /// <summary>Подсказка при осмотре: где входы и выходы (ключ локализации).</summary>
    [DataField]
    public LocId? PortsHint;
}
