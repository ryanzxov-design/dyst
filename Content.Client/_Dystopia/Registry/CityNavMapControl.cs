// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: навигационная карта Города в цветах терминала (основа карт соционадзора).
// Ванильная карта прибита к квадрату 648×648 и рисуется вокруг его центра: в колонке консоли она выталкивала соседей
// за край окна, а в растянутом окне рисовалась в углу и обрезалась. Здесь карта занимает столько места, сколько ей
// дали: рисунок вписывается в доступный квадрат под панелью и центрируется, а щелчки и перетаскивание переводятся
// обратно в координаты ванильной карты. Панель над картой тянется по ширине, подписи кнопок короче.

using System.Numerics;
using Content.Client._Dystopia.UserInterface;
using Content.Client.Pinpointer.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client._Dystopia.Registry;

[Virtual]
public class CityNavMapControl : NavMapControl
{
    /// <summary>Панель над картой (масштаб, кнопки) — рисунок карты начинается под ней.</summary>
    private Control? _top;

    /// <summary>Во сколько раз рисунок меньше (больше) ванильного квадрата и где его левый верхний угол.</summary>
    private float _scale = 1f;
    private Vector2 _origin;

    public CityNavMapControl()
    {
        WallColor = CityUi.Frame;
        TileColor = CityUi.Panel;
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(0.9f));
        RectClipContent = true;

        // без прибитого размера: карта берёт место, какое даёт окно
        SetSize = new Vector2(float.NaN, float.NaN);
        MinSize = new Vector2(200, 200);

        foreach (var child in Children)
        {
            // верхний столбик (панель + пустое поле) — во всю ширину карты
            LayoutContainer.SetAnchorPreset(child, LayoutContainer.LayoutPreset.TopWide);
            if (child.ChildCount > 0)
                _top = child.GetChild(0);
        }

        FitTopPanel(this);
    }

    /// <summary>Панель над картой: не шире самой карты, короткие подписи.</summary>
    private static void FitTopPanel(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is PanelContainer panel && !float.IsNaN(panel.SetWidth))
            {
                panel.SetWidth = float.NaN;
                panel.HorizontalExpand = true;
            }

            switch (child)
            {
                case CheckBox box:
                    box.Text = Loc.GetString("dystopia-surveillance-map-beacons");
                    break;
                case Button button:
                    button.Text = Loc.GetString("dystopia-surveillance-map-recenter");
                    break;
            }

            FitTopPanel(child);
        }
    }

    /// <summary>Вписать ванильный квадрат в место под панелью.</summary>
    private void UpdateFit()
    {
        var top = _top?.PixelHeight ?? 0;
        var width = (float) PixelWidth;
        var height = Math.Max(1f, PixelHeight - top);
        var side = Math.Max(1f, Math.Min(width, height));

        _scale = SizeFull > 0 ? side / SizeFull : 1f;
        _origin = new Vector2((width - side) / 2f, top + (height - side) / 2f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        UpdateFit();

        var old = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(_scale) * Matrix3x2.CreateTranslation(_origin) * old);
        base.Draw(handle);
        handle.SetTransform(old);
    }

    /// <summary>Точка экрана → точка, куда она попала бы на ванильной карте.</summary>
    private GUIBoundKeyEventArgs Unscale(GUIBoundKeyEventArgs args)
    {
        var local = args.PointerLocation.Position - GlobalPixelPosition;
        var mapped = (local - _origin) / _scale;
        var pointer = new ScreenCoordinates(GlobalPixelPosition + mapped, args.PointerLocation.Window);

        return new GUIBoundKeyEventArgs(args.Function, args.State, pointer, args.CanFocus,
            mapped / UIScale, mapped);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        var inner = Unscale(args);
        base.KeyBindDown(inner);
        if (inner.Handled)
            args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        var inner = Unscale(args);
        base.KeyBindUp(inner);
        if (inner.Handled)
            args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        // перетаскивание: карта под курсором должна идти вровень с ним и в уменьшенном виде
        var before = Offset;
        base.MouseMove(args);
        if (Offset != before && _scale > 0f)
            Offset = before + (Offset - before) / _scale;
    }
}
