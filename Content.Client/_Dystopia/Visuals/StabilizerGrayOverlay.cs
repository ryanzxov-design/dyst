using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Visuals;

/// <summary>Серый экран рядом со стабилизатором. Сила задаётся системой (StabilizerGraySystem).</summary>
public sealed partial class StabilizerGrayOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "DystopiaStabilizerGray";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly ShaderInstance _shader;

    public float Amount;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public StabilizerGrayOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 12; // поверх «картинки Города» и визора
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Amount > 0.01f &&
               _entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eye) &&
               args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("amount", Amount);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
