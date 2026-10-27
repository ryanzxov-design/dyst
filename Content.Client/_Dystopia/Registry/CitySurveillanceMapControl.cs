// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: карта камер соционадзора — навигационная карта Города в цветах терминала, на ней камеры (кружок),
// камеры закрытых зон (треугольник) и надетые маски Стражи (ромб). Щелчок по метке — показать эту камеру.

using Content.Client._Dystopia.UserInterface;
using Content.Client.Pinpointer.UI;
using Content.Client.Resources;
using Content.Shared._Dystopia.Registry;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;

namespace Content.Client._Dystopia.Registry;

public sealed class CitySurveillanceMapControl : NavMapControl
{
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public static readonly Color MaskColor = Color.FromHex("#E8C66A");
    public static readonly Color RestrictedColor = Color.FromHex("#FF7A6E");
    public static readonly Color OffColor = Color.FromHex("#40546A");

    private readonly Texture _wall;
    private readonly Texture _restricted;
    private readonly Texture _mask;

    public event Action<NetEntity>? CameraSelected;

    public CitySurveillanceMapControl()
    {
        IoCManager.InjectDependencies(this);

        WallColor = CityUi.Frame;
        TileColor = CityUi.Panel;
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(0.9f));

        _wall = _resources.GetTexture("/Textures/Interface/NavMap/beveled_circle.png");
        _restricted = _resources.GetTexture("/Textures/Interface/NavMap/beveled_triangle.png");
        _mask = _resources.GetTexture("/Textures/Interface/NavMap/beveled_diamond.png");

        TrackedEntitySelectedAction += entity =>
        {
            if (entity is { } net)
                CameraSelected?.Invoke(net);
        };
    }

    public void SetCameras(IEnumerable<CitySurveillanceCamera> cameras, NetEntity? selected)
    {
        TrackedEntities.Clear();
        if (MapUid is not { } grid)
            return;

        foreach (var camera in cameras)
        {
            if (_entMan.GetEntity(camera.Grid) != grid)
                continue;

            var texture = camera.Kind switch
            {
                CityCameraKind.Mask => _mask,
                CityCameraKind.Restricted => _restricted,
                _ => _wall,
            };

            var color = !camera.Active ? OffColor
                : camera.Entity == selected ? CityUi.Glow
                : camera.Kind switch
                {
                    CityCameraKind.Mask => MaskColor,
                    CityCameraKind.Restricted => RestrictedColor,
                    _ => CityUi.Accent,
                };

            TrackedEntities[camera.Entity] = new NavMapBlip(
                new EntityCoordinates(grid, camera.Position),
                texture,
                color,
                camera.Entity == selected,
                camera.Active);
        }
    }
}
