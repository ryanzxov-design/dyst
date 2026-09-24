using Robust.Shared.Serialization;

namespace Content.Shared._Dystopia.Laws;

/// <summary>
/// Статья Свода законов Города.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CityLaw
{
    /// <summary>Внутренний идентификатор (не номер статьи). Назначается автоматически.</summary>
    [DataField]
    public int Id;

    /// <summary>Номер статьи, как его видят игроки.</summary>
    [DataField]
    public int Number;

    [DataField]
    public string Title = string.Empty;

    [DataField]
    public string Text = string.Empty;

    /// <summary>Санкция за нарушение. Может быть пустой.</summary>
    [DataField]
    public string Sanction = string.Empty;

    /// <summary>Время принятия от начала смены. У Основного закона — 00:00.</summary>
    [DataField]
    public TimeSpan EnactedAt = TimeSpan.Zero;

    public CityLaw Clone()
    {
        return new CityLaw
        {
            Id = Id,
            Number = Number,
            Title = Title,
            Text = Text,
            Sanction = Sanction,
            EnactedAt = EnactedAt,
        };
    }
}

/// <summary>
/// Класс санкций: правовое и дисциплинарное наказание.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CitySanctionClass
{
    /// <summary>Обозначение класса: I, II, III...</summary>
    [DataField]
    public string Class = string.Empty;

    [DataField]
    public string Legal = string.Empty;

    [DataField]
    public string Disciplinary = string.Empty;

    public CitySanctionClass Clone()
    {
        return new CitySanctionClass { Class = Class, Legal = Legal, Disciplinary = Disciplinary };
    }
}

/// <summary>
/// Свод законов Города: статьи и шкала санкций. Вешается на станцию Города.
/// Начальное содержимое (Основной закон) задаётся в прототипе станции.
/// </summary>
[RegisterComponent]
public sealed partial class CityLawsComponent : Component
{
    [DataField]
    public List<CityLaw> Laws = new();

    [DataField]
    public List<CitySanctionClass> Sanctions = new();

    /// <summary>Общее положение к шкале санкций.</summary>
    [DataField]
    public string GeneralProvision = string.Empty;

    [ViewVariables]
    public int NextId = 1;
}

/// <summary>Программа КПК «Свод законов».</summary>
[RegisterComponent]
public sealed partial class CityLawsCartridgeComponent : Component
{
}

/// <summary>Метка для КПК: при появлении в него устанавливается программа «Свод законов».</summary>
[RegisterComponent]
public sealed partial class CityLawsPreinstallComponent : Component
{
}

/// <summary>Состояние программы «Свод законов».</summary>
[Serializable, NetSerializable]
public sealed class CityLawsUiState(List<CityLaw> laws, List<CitySanctionClass> sanctions, string generalProvision)
    : BoundUserInterfaceState
{
    public readonly List<CityLaw> Laws = laws;
    public readonly List<CitySanctionClass> Sanctions = sanctions;
    public readonly string GeneralProvision = generalProvision;
}
