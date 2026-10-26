// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — общее состояние для всех его элементов: тема (по должности на высоком приоритете у
// выбранного персонажа), профиль, время для анимаций, шрифты, надписи темы, порча текста у повреждённого терминала.
// Элементы сравнивают Version со своим, чтобы заметить смену персонажа или темы.

using System.Linq;
using System.Text;
using Content.Client.Lobby;
using Content.Client.Resources;
using Content.Shared._Dystopia.Lobby;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaLobbyThemeController : UIController
{
    [Dependency] private IClientPreferencesManager _prefs = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _res = default!;

    public const string MonoFontPath = "/Fonts/_Dystopia/PTMono/PTMono-Regular.ttf";
    public const string PixelFontPath = "/Fonts/_Dystopia/PressStart2P/PressStart2P-Regular.ttf";
    private const string Glyphs = "█▓▒?#@/0";

    private readonly Dictionary<(bool, int), Font> _fonts = new();
    private DystopiaLobbyThemePrototype? _theme;

    /// <summary>Растёт при каждой смене персонажа, его профиля или темы.</summary>
    public int Version { get; private set; }

    public HumanoidCharacterProfile? Profile { get; private set; }

    /// <summary>Секунды с запуска клиента — для анимаций.</summary>
    public float Time { get; private set; }

    /// <summary>Шаг порчи текста (раз в 0,65 с).</summary>
    public int Tick => (int) (Time / 0.65f);

    public DystopiaLobbyThemePrototype Theme => _theme ??= Resolve(Profile);

    public bool Corrupted => Theme.Corrupted;
    public Color Fg => Theme.Foreground;
    public Color Dim => Theme.Dim;
    public Color Glow => Theme.Glow;
    public Color Bg => Theme.Background;
    public Color Alert => Theme.Alert;

    /// <summary>Тёмный цвет текста на инвертированной (выбранной) строке.</summary>
    public static readonly Color Ink = Color.FromHex("#020402");

    /// <summary>Пульсация: 1 → 0,45 → 1 за 2 с.</summary>
    public float Pulse => 0.725f + 0.275f * MathF.Cos(Time * MathF.PI);

    /// <summary>Мигание курсора: раз в секунду.</summary>
    public bool Blink => (Time % 1f) < 0.5f;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Time += args.DeltaSeconds;

        HumanoidCharacterProfile? profile = null;
        if (_prefs.ServerDataLoaded && _prefs.Preferences is { } prefs &&
            prefs.Characters.ContainsKey(prefs.SelectedCharacterIndex))
        {
            profile = prefs.SelectedCharacter;
        }

        if (ReferenceEquals(profile, Profile) && _theme != null)
            return;

        Profile = profile;
        _theme = Resolve(profile);
        Version++;
    }

    private DystopiaLobbyThemePrototype Resolve(HumanoidCharacterProfile? profile)
    {
        ProtoId<JobPrototype>? high = null;
        if (profile != null)
        {
            foreach (var (job, priority) in profile.JobPriorities)
            {
                if (priority != JobPriority.High)
                    continue;

                high = job;
                break;
            }
        }

        DystopiaLobbyThemePrototype? fallback = null;
        foreach (var theme in _proto.EnumeratePrototypes<DystopiaLobbyThemePrototype>().OrderBy(t => t.ID))
        {
            if (high is { } job && theme.Jobs.Contains(job))
                return theme;

            if (theme.Default)
                fallback ??= theme;
        }

        return fallback ?? new DystopiaLobbyThemePrototype();
    }

    /// <summary>Надпись темы: dystopia-lobby-&lt;тема&gt;-&lt;часть&gt;, затем общая dystopia-lobby-&lt;часть&gt;.</summary>
    public string Text(string part)
    {
        if (Loc.TryGetString($"dystopia-lobby-{Theme.Text}-{part}", out var own))
            return own;

        return Loc.TryGetString($"dystopia-lobby-{part}", out var common) ? common : part;
    }

    /// <summary>У повреждённого терминала часть букв подменяется мусором (меняется каждые 0,65 с).</summary>
    public string Corrupt(string text, int rate = 19)
    {
        if (!Corrupted || text.Length == 0)
            return text;

        var tick = Tick;
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            sb.Append(c != ' ' && (i * 7 + tick * 13) % rate == 0 ? Glyphs[(i + tick) % Glyphs.Length] : c);
        }

        return sb.ToString();
    }

    public Font Font(bool pixel, int size)
    {
        if (_fonts.TryGetValue((pixel, size), out var font))
            return font;

        font = _res.GetFont(pixel ? PixelFontPath : MonoFontPath, size);
        _fonts[(pixel, size)] = font;
        return font;
    }

    public Texture Texture(string name)
    {
        return _res.GetTexture($"/Textures/_Dystopia/Interface/Lobby/{name}.png");
    }
}
