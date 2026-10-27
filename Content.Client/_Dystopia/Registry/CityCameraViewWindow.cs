// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: «Смотреть» в консоли социоучёта — картинка с камеры (или маски Стражи), которая отметила человека.
// Сервер подписывает смотрящего на обзор камеры; картинка берётся из глаза камеры, как в ванильной консоли камер.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Client.Eye;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Registry;

public sealed class CityCameraViewWindow : CityWindow
{
    [Dependency] private IEntityManager _entMan = default!;

    private readonly ScalingViewport _view;
    private readonly Label _status;
    private readonly FixedEye _blank = new();
    private EntityUid? _camera;
    private string _title = string.Empty;

    public CityCameraViewWindow()
    {
        IoCManager.InjectDependencies(this);

        WindowTitle = Loc.GetString("dystopia-registry-camera-title");
        Subtitle = Loc.GetString("dystopia-registry-camera-subtitle");
        Slogan = Loc.GetString("dystopia-registry-camera-slogan");
        MinSize = new Vector2(540, 620);
        SetSize = new Vector2(560, 640);

        _status = CityUi.MakeLabel(string.Empty, CityUi.Glow, CityUi.Regular(12));
        Contents.AddChild(_status);

        var frame = CityUi.MakePanel(CityUi.Input, CityUi.Frame, 2);
        frame.VerticalExpand = true;
        _view = new ScalingViewport
        {
            ViewportSize = new Vector2i(500, 500),
            MinSize = new Vector2(500, 500),
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalExpand = true,
            VerticalExpand = true,
            Eye = _blank,
            Visible = false,
        };
        frame.AddChild(_view);
        Contents.AddChild(frame);
    }

    public EntityUid? Camera => _camera;

    public void SetCamera(EntityUid camera, string title)
    {
        _camera = camera;
        _status.Text = Loc.GetString("dystopia-registry-camera-connecting", ("camera", title));
        _view.Eye = _blank;
        _view.Visible = false;
        _title = title;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // камера может дойти до клиента не сразу — ждём её глаз
        if (_camera is not { } camera || _view.Eye != _blank)
            return;

        if (!_entMan.TryGetComponent<EyeComponent>(camera, out var eye) || eye.Eye == null)
            return;

        _entMan.System<EyeLerpingSystem>().AddEye(camera, eye);
        _view.Eye = eye.Eye;
        _view.Visible = true;
        _status.Text = Loc.GetString("dystopia-registry-camera-connected", ("camera", _title));
    }
}
