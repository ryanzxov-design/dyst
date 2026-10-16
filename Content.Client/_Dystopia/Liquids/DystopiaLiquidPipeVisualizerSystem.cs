// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: отсечки на жидкостной трубе — цвет смеси в сети (сервер шлёт его редко и ступенями).

using Content.Shared._Dystopia.Liquids;
using Robust.Client.GameObjects;

namespace Content.Client._Dystopia.Liquids;

public sealed partial class DystopiaLiquidPipeVisualizerSystem : VisualizerSystem<DystopiaLiquidPipeComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DystopiaLiquidPipeComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !SpriteSystem.LayerMapTryGet((uid, args.Sprite), DystopiaLiquidVisualLayers.Marks, out var layer, false))
        {
            return;
        }

        if (!AppearanceSystem.TryGetData<Color>(uid, DystopiaLiquidVisuals.Color, out var color, args.Component))
            color = component.EmptyColor;

        // Прозрачность не трогаем — ею управляет показ труб под полом (Т-сканер).
        SpriteSystem.LayerSetColor((uid, args.Sprite), layer, color.WithAlpha(args.Sprite[layer].Color.A));
    }
}
