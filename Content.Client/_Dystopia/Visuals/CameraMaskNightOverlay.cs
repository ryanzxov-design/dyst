// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: картинка с камеры-маски (консоль камер, соционадзор) — со слабым ночным зрением окуляров маски, как у её
// носителя: тьма чуть светлее. Подсветка берётся из ночного зрения самой маски.

using Content.Shared.Clothing.Components;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;

namespace Content.Client._Dystopia.Visuals;

public sealed partial class CameraMaskNightOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private Color _lighting;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public CameraMaskNightOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var viewportEye = args.Viewport.Eye;
        if (viewportEye == null)
            return false;

        // свой экран игрока — не наша забота (там работает обычное ночное зрение)
        if (_playerManager.LocalEntity is { } player &&
            _entityManager.TryGetComponent(player, out EyeComponent? own) && own.Eye == viewportEye)
        {
            return false;
        }

        var query = _entityManager.EntityQueryEnumerator<NightVisionComponent, EyeComponent>();
        while (query.MoveNext(out var uid, out var vision, out var eye))
        {
            if (eye.Eye == null || eye.Eye != viewportEye)
                continue;

            if (!vision.Enabled ||
                _entityManager.TryGetComponent(uid, out MaskComponent? mask) && mask.IsToggled)
            {
                return false;
            }

            _lighting = vision.LightingColor;
            return true;
        }

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.WorldHandle.DrawRect(args.WorldBounds, _lighting);
    }
}
