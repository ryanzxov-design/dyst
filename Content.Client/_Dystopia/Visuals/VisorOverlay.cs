using Content.Shared._Dystopia.Visuals;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Visuals;

/// <summary>
/// Взгляд через маску: если на игроке надета (не приспущена) маска с DystopiaVisorComponent,
/// экран окрашивается цветом её стёкол (слабо в центре, сильнее к краям). Маска Стражи — синий визор с полосками.
/// </summary>
public sealed partial class VisorOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "DystopiaVisor";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly InventorySystem _inventory;
    private readonly ShaderInstance _shader;
    private DystopiaVisorComponent? _visor;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public VisorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _inventory = _entityManager.System<InventorySystem>();
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 11; // поверх «картинки Города»
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _visor = null;

        if (_playerManager.LocalEntity is not { } player ||
            !_entityManager.TryGetComponent(player, out EyeComponent? eye) ||
            args.Viewport.Eye != eye.Eye)
        {
            return false;
        }

        if (!_inventory.TryGetSlotEntity(player, "mask", out var mask) ||
            !_entityManager.TryGetComponent(mask, out DystopiaVisorComponent? visor))
        {
            return false;
        }

        // Приспущенная маска не мешает обзору.
        if (_entityManager.TryGetComponent(mask, out MaskComponent? maskComp) && maskComp.IsToggled)
            return false;

        _visor = visor;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || _visor == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("tint", new System.Numerics.Vector3(_visor.Tint.R, _visor.Tint.G, _visor.Tint.B));
        _shader.SetParameter("centerStrength", _visor.CenterStrength);
        _shader.SetParameter("edgeStrength", _visor.EdgeStrength);
        _shader.SetParameter("haze", _visor.Haze);
        _shader.SetParameter("darken", _visor.Darken);
        _shader.SetParameter("monochrome", _visor.Monochrome);
        _shader.SetParameter("sharpen", _visor.Sharpen);
        _shader.SetParameter("brightness", _visor.Brightness);
        _shader.SetParameter("centerRelief", _visor.CenterRelief);
        _shader.SetParameter("scanlines", _visor.Scanlines);
        _shader.SetParameter("linePeriod", _visor.LinePeriod);
        _shader.SetParameter("lineStrength", _visor.LineStrength);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
