namespace Content.Shared._Dystopia.FleshCult;

/// <summary>
/// Колыбель Плоти: сердце логова. Её кормят трупами и биомассой.
/// Накопив достаточно плоти, Колыбель готова родить Посланника Плоти (этап 5).
/// Уничтожение Колыбели — победа Правительства Города (этап 6).
/// </summary>
[RegisterComponent]
public sealed partial class DystopiaFleshCradleComponent : Component
{
    /// <summary>Накопленная биомасса.</summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Biomass;

    /// <summary>Сколько биомассы нужно для рождения Посланника.</summary>
    [DataField]
    public float EnvoyThreshold = 3000f;

    /// <summary>Сколько Посланников Колыбель готова родить (ждут, пока появится механика Посланника).</summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int EnvoysReady;

    /// <summary>Биомасса с тела гуманоида.</summary>
    [DataField]
    public float HumanoidBiomass = 100f;

    /// <summary>Биомасса с тела любого другого существа.</summary>
    [DataField]
    public float CreatureBiomass = 30f;

    /// <summary>Сколько секунд Колыбель поглощает тело.</summary>
    [DataField]
    public float FeedDelay = 3f;
}

/// <summary>Кусок биомассы: предмет, который можно скормить Колыбели.</summary>
[RegisterComponent]
public sealed partial class DystopiaBiomassComponent : Component
{
    [DataField]
    public float Amount = 10f;
}
