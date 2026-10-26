// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — «личное дело» выбранного персонажа (поля по теме), рамка-видоискатель вокруг его спрайта
// с лучом сканера и блок распределения (должности высокого, среднего и низкого приоритета).

using System.Linq;
using System.Numerics;
using Content.Client.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Dystopia.Lobby;

/// <summary>Поля дела: ключ темой (тускло), под ним значение — так влезает и в узкую колонку.</summary>
public sealed partial class DystopiaLobbyDossierFields : Control
{
    private const int KeySize = 13;
    private const int TextSize = 17;
    private const float ValueOffset = 18f;
    private const float Row = 50f;

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private DystopiaLobbyThemeController? _theme;
    private ClientGameTicker? _ticker;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaLobbyDossierFields()
    {
        IoCManager.InjectDependencies(this);
        MinHeight = Row * 5f - 6f;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var profile = theme.Profile;
        if (profile == null)
            return;

        _ticker ??= _entities.SystemOrNull<ClientGameTicker>();
        var ready = _ticker is { IsGameStarted: false, AreWeReady: true };
        var job = DystopiaLobbyJobs.Jobs(_proto, profile, JobPriority.High).FirstOrDefault() ?? "—";

        Field(handle, 0, "field-name", profile.Name, theme.Fg, false);
        Field(handle, 1, "field-age", profile.Age.ToString(), theme.Fg, false);
        Field(handle, 2, "field-job", job, theme.Fg, false);
        Field(handle, 3, "field-loyalty", theme.Text("loyalty"), theme.Corrupted ? theme.Alert : theme.Fg, true);
        Field(handle, 4, "field-status", theme.Text(ready ? "status-ready" : "status-wait"), theme.Fg, false);
    }

    private void Field(DrawingHandleScreen handle, int row, string key, string value, Color color, bool pulse)
    {
        var theme = Term;
        var s = UIScale;
        var room = PixelSize.X;
        if (room <= 0f)
            return;

        var y = row * Row * s;
        var keyFont = theme.Font(false, KeySize);
        var font = theme.Font(false, TextSize);
        DystopiaTerm.Glowing(handle, theme, keyFont, new Vector2(0f, y), theme.Corrupt(theme.Text(key)), s, theme.Dim, 1f);
        var text = DystopiaTerm.Fit(font, value, s, room);
        DystopiaTerm.Glowing(handle, theme, font, new Vector2(0f, y + ValueOffset * s), text, s,
            pulse ? color.WithAlpha(theme.Pulse) : color);
    }
}

/// <summary>Блок распределения: высокий / средний / низкий приоритет.</summary>
public sealed partial class DystopiaLobbyJobs : Control
{
    private const int TextSize = 15;
    private const float KeyWidth = 110f;
    private const float Row = 24f;

    [Dependency] private IPrototypeManager _proto = default!;

    private DystopiaLobbyThemeController? _theme;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaLobbyJobs()
    {
        IoCManager.InjectDependencies(this);
        MinHeight = Row * 3f;
        HorizontalExpand = true;
    }

    /// <summary>Названия должностей с данным приоритетом.</summary>
    public static IEnumerable<string> Jobs(IPrototypeManager proto, HumanoidCharacterProfile profile, JobPriority priority)
    {
        foreach (var (job, p) in profile.JobPriorities)
        {
            if (p != priority)
                continue;

            yield return proto.TryIndex(job, out var jobProto) ? jobProto.LocalizedName : job.Id;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var profile = theme.Profile;
        var s = UIScale;
        var font = theme.Font(false, TextSize);
        var rows = new[] { ("priority-high", JobPriority.High), ("priority-medium", JobPriority.Medium), ("priority-low", JobPriority.Low) };

        for (var i = 0; i < rows.Length; i++)
        {
            var (key, priority) = rows[i];
            var y = i * Row * s;
            DystopiaTerm.Glowing(handle, theme, font, new Vector2(0f, y), theme.Corrupt(theme.Text(key)), s, theme.Dim);
            var list = profile == null ? string.Empty : string.Join(", ", Jobs(_proto, profile, priority));
            if (list.Length == 0)
                list = "—";

            var room = PixelSize.X - KeyWidth * s;
            if (room <= 0f)
                continue;

            DystopiaTerm.Glowing(handle, theme, font, new Vector2(KeyWidth * s, y), DystopiaTerm.Fit(font, list, s, room), s, theme.Fg);
        }
    }
}

/// <summary>Рамка-видоискатель вокруг спрайта: фон строками развёртки, уголки.</summary>
public sealed class DystopiaPortraitFrame : Control
{
    private DystopiaLobbyThemeController? _theme;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaPortraitFrame()
    {
        MinSize = new Vector2(170f, 214f);
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var s = UIScale;
        var size = PixelSize;
        var t = MathF.Max(1f, MathF.Round(s));

        var stripe = theme.Fg.WithAlpha(0.07f);
        for (var y = 7f * s; y < size.Y; y += 8f * s)
        {
            DystopiaTerm.Rect(handle, 0f, y, size.X, y + t, stripe);
        }

        var dim = theme.Dim;
        DystopiaTerm.Rect(handle, 0f, 0f, size.X, t, dim);
        DystopiaTerm.Rect(handle, 0f, size.Y - t, size.X, size.Y, dim);
        DystopiaTerm.Rect(handle, 0f, 0f, t, size.Y, dim);
        DystopiaTerm.Rect(handle, size.X - t, 0f, size.X, size.Y, dim);

        // уголки видоискателя
        var m = 10f * s;
        var l = 16f * s;
        var w = 2f * s;
        var fg = theme.Fg;
        var bottom = size.Y - 30f * s;
        DystopiaTerm.Rect(handle, m, m, m + l, m + w, fg);
        DystopiaTerm.Rect(handle, m, m, m + w, m + l, fg);
        DystopiaTerm.Rect(handle, size.X - m - l, m, size.X - m, m + w, fg);
        DystopiaTerm.Rect(handle, size.X - m - w, m, size.X - m, m + l, fg);
        DystopiaTerm.Rect(handle, m, bottom - w, m + l, bottom, fg);
        DystopiaTerm.Rect(handle, m, bottom - l, m + w, bottom, fg);
        DystopiaTerm.Rect(handle, size.X - m - l, bottom - w, size.X - m, bottom, fg);
        DystopiaTerm.Rect(handle, size.X - m - w, bottom - l, size.X - m, bottom, fg);
    }
}

/// <summary>Поверх спрайта: луч сканера ходит вверх-вниз, внизу мигает «идентификация».</summary>
public sealed class DystopiaPortraitScan : Control
{
    private DystopiaLobbyThemeController? _theme;

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaPortraitScan()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var theme = Term;
        var s = UIScale;
        var size = PixelSize;

        var p = theme.Time % 2.6f / 2.6f;
        var tri = p < 0.5f ? p * 2f : 2f - p * 2f;
        var eased = tri * tri * (3f - 2f * tri);
        var y = eased * (size.Y - 6f * s);
        DystopiaTerm.Rect(handle, 0f, y - 4f * s, size.X, y + 10f * s, theme.Glow.WithAlpha(theme.Glow.A * 0.25f));
        DystopiaTerm.Rect(handle, 0f, y, size.X, y + 6f * s, theme.Fg.WithAlpha(0.55f));

        var font = theme.Font(false, 12);
        var label = theme.Corrupt(theme.Text("identify"));
        var w = DystopiaTerm.Measure(font, label, s, 2f);
        DystopiaTerm.Glowing(handle, theme, font, new Vector2((size.X - w) / 2f, size.Y - 22f * s), label, s,
            theme.Fg.WithAlpha(theme.Pulse), 2f);
    }
}

/// <summary>Сведения о смене: сводка сервера (номер смены, карта, режим, на связи) — моноширинным, «КЛЮЧ: значение».</summary>
public sealed partial class DystopiaLobbyInfo : Control
{
    private const int TextSize = 15;
    private const float Row = 22f;

    [Dependency] private IEntityManager _entities = default!;

    private DystopiaLobbyThemeController? _theme;
    private ClientGameTicker? _ticker;
    private string? _blob;
    private string[] _lines = Array.Empty<string>();

    private DystopiaLobbyThemeController Term => _theme ??= UserInterfaceManager.GetUIController<DystopiaLobbyThemeController>();

    public DystopiaLobbyInfo()
    {
        IoCManager.InjectDependencies(this);
        MinHeight = Row * 4f;
        HorizontalExpand = true;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        _ticker ??= _entities.SystemOrNull<ClientGameTicker>();
        var blob = _ticker?.ServerInfoBlob;
        if (blob != _blob)
        {
            _blob = blob;
            _lines = blob == null
                ? Array.Empty<string>()
                : Robust.Shared.Utility.FormattedMessage.RemoveMarkupPermissive(blob).Split('\n');
        }

        var theme = Term;
        var s = UIScale;
        var font = theme.Font(false, TextSize);
        for (var i = 0; i < _lines.Length; i++)
        {
            var line = _lines[i].Trim();
            if (line.Length == 0)
                continue;

            var y = i * Row * s;
            var split = line.IndexOf(": ", StringComparison.Ordinal);
            if (split < 0)
            {
                DystopiaTerm.Glowing(handle, theme, font, new Vector2(0f, y), DystopiaTerm.Fit(font, line, s, PixelSize.X), s, theme.Fg);
                continue;
            }

            var key = theme.Corrupt(line[..(split + 1)]);
            var x = DystopiaTerm.Glowing(handle, theme, font, new Vector2(0f, y), key, s, theme.Dim) + 8f * s;
            var room = PixelSize.X - x;
            if (room > 0f)
                DystopiaTerm.Glowing(handle, theme, font, new Vector2(x, y), DystopiaTerm.Fit(font, line[(split + 2)..], s, room), s, theme.Fg);
        }
    }
}
