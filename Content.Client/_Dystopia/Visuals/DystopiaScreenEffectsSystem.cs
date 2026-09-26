using Content.Shared._Dystopia;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Dystopia.Visuals;

/// <summary>
/// Экранные эффекты Dystopia: «картинка Города» и взгляд через маски.
/// Включаются и выключаются клиентскими настройками:
///   cvar dystopia.screen_filter false  — выключить «картинку Города»
///   cvar dystopia.visor_effects false  — выключить эффекты масок
/// </summary>
public sealed partial class DystopiaScreenEffectsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private CityScreenOverlay _cityOverlay = default!;
    private VisorOverlay _visorOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _cityOverlay = new CityScreenOverlay();
        _visorOverlay = new VisorOverlay();

        _cfg.OnValueChanged(DystopiaCVars.ScreenFilter, OnScreenFilterChanged, true);
        _cfg.OnValueChanged(DystopiaCVars.VisorEffects, OnVisorChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(DystopiaCVars.ScreenFilter, OnScreenFilterChanged);
        _cfg.UnsubValueChanged(DystopiaCVars.VisorEffects, OnVisorChanged);
        _overlayManager.RemoveOverlay(_cityOverlay);
        _overlayManager.RemoveOverlay(_visorOverlay);
    }

    private void OnScreenFilterChanged(bool enabled)
    {
        if (enabled)
            _overlayManager.AddOverlay(_cityOverlay);
        else
            _overlayManager.RemoveOverlay(_cityOverlay);
    }

    private void OnVisorChanged(bool enabled)
    {
        if (enabled)
            _overlayManager.AddOverlay(_visorOverlay);
        else
            _overlayManager.RemoveOverlay(_visorOverlay);
    }
}
