using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Visuals;

/// <summary>
/// «Картинка Города»: мягкая выборочная цветокоррекция (гасит тёплые цвета, не трогает свет и чёрное).
/// Основной холод Города даёт свет ламп и планеты, а не этот фильтр. Рисуется поверх мира (не интерфейса).
/// </summary>
public sealed partial class CityScreenOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "DystopiaCityGrade";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    // Подобранные значения «картинки Города»: мягко и выборочно, чтобы фильтр не ощущался наложенным.
    public float BaseDesaturation = 0.12f;
    public float WarmDesaturation = 0.38f;
    public float Coldness = 0.6f;

    public CityScreenOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 10;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Только для основного вида игрока (не для камер и прочих вьюпортов).
        return _entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eye) &&
               args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("baseDesaturation", BaseDesaturation);
        _shader.SetParameter("warmDesaturation", WarmDesaturation);
        _shader.SetParameter("coldness", Coldness);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
