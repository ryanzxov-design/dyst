// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: оформление лобби-терминала. Какую тему видит игрок, решает должность на высоком приоритете у выбранного
// персонажа: тема, в чьём списке Jobs она есть, иначе тема с Default. Цвета люминофора и надписи (локаль
// dystopia-lobby-<Text>-<часть>, запасная — dystopia-lobby-<часть>) — здесь; рисует клиент (_Dystopia/Lobby).

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Dystopia.Lobby;

[Prototype("dystopiaLobbyTheme")]
public sealed partial class DystopiaLobbyThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Тема для всех, чья должность не попала ни в одну другую.</summary>
    [DataField]
    public bool Default;

    /// <summary>Должности (на высоком приоритете), для которых эта тема.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();

    /// <summary>Часть ключей локали: dystopia-lobby-&lt;Text&gt;-title и т.п.</summary>
    [DataField]
    public string Text = "citizen";

    /// <summary>Основной цвет люминофора.</summary>
    [DataField]
    public Color Foreground = Color.FromHex("#7DFF9B");

    /// <summary>Приглушённый цвет (подписи, рамки).</summary>
    [DataField]
    public Color Dim = Color.FromHex("#3FA457");

    /// <summary>Свечение вокруг текста.</summary>
    [DataField]
    public Color Glow = Color.FromHex("#50FF788C");

    /// <summary>Цвет экрана в центре.</summary>
    [DataField]
    public Color Background = Color.FromHex("#071A0C");

    /// <summary>Тревожный цвет (запреты, сигнальные огни).</summary>
    [DataField]
    public Color Alert = Color.FromHex("#FF5A4A");

    /// <summary>Дым из труб силуэта.</summary>
    [DataField]
    public Color Smoke = Color.FromHex("#7DFF9B");

    /// <summary>Повреждённый терминал: порча букв, разрывы, статика, наросты на силуэте, заголовок-перевёртыш.</summary>
    [DataField]
    public bool Corrupted;

    /// <summary>Строки загрузки узла, печатаются по одной.</summary>
    [DataField]
    public List<DystopiaLobbyBootLine> Boot = new();
}

[DataDefinition]
public sealed partial class DystopiaLobbyBootLine
{
    [DataField(required: true)]
    public LocId Text;

    /// <summary>Статус справа обычным цветом.</summary>
    [DataField]
    public LocId? Ok;

    /// <summary>Статус справа тревожным цветом.</summary>
    [DataField]
    public LocId? Bad;
}
