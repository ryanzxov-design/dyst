using Robust.Shared.Configuration;

namespace Content.Shared._Dystopia;

/// <summary>Настройки Dystopia. Клиентские — меняются командой cvar в консоли клиента (F3/~).</summary>
[CVarDefs]
public sealed class DystopiaCVars
{
    /// <summary>«Картинка Города»: приглушённые холодные цвета.</summary>
    public static readonly CVarDef<bool> ScreenFilter =
        CVarDef.Create("dystopia.screen_filter", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Эффекты масок и противогазов.</summary>
    public static readonly CVarDef<bool> VisorEffects =
        CVarDef.Create("dystopia.visor_effects", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
