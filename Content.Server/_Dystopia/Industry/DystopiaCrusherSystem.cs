// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: дробилки руды. Ручная — цикл идёт, пока игрок крутит ворот (повторяется, пока не отойдёт);
// электрические — сами, пока есть руда и ток. За цикл дробится до OresPerCycle кусков: 7 кусков ручная и щековая
// дробят за 7 циклов, конусная (по 3 за цикл) — за 3.

using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Dystopia.Industry;
using Content.Shared.Audio;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Dystopia.Industry;

public sealed partial class DystopiaCrusherSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaCrusherComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DystopiaCrusherComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DystopiaCrusherComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaCrusherComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DystopiaCrusherComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DystopiaCrusherComponent, DystopiaCrusherCrankDoAfterEvent>(OnCrank);
        SubscribeLocalEvent<DystopiaCrusherComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(Entity<DystopiaCrusherComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    private void OnMapInit(Entity<DystopiaCrusherComponent> ent, ref MapInitEvent args)
    {
        SetRunning(ent, false);
    }

    // ------------------------------------------------------------------ рецепты

    /// <summary>Рецепт для предмета (по его стеку). Null — дробилка это не берёт.</summary>
    public DystopiaCrushRecipePrototype? GetRecipe(EntityUid item)
    {
        if (!TryComp<StackComponent>(item, out var stack))
            return null;

        foreach (var recipe in _proto.EnumeratePrototypes<DystopiaCrushRecipePrototype>())
        {
            if (recipe.Input == stack.StackTypeId)
                return recipe;
        }

        return null;
    }

    /// <summary>Сколько кусков руды лежит в загрузке.</summary>
    public int LoadedOre(Entity<DystopiaCrusherComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return 0;

        var total = 0;
        foreach (var item in container.ContainedEntities)
        {
            total += _stack.GetCount(item);
        }

        return total;
    }

    // ------------------------------------------------------------------ загрузка

    private void OnInteractUsing(Entity<DystopiaCrusherComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (GetRecipe(args.Used) == null)
        {
            // Руда, которую не дробят (боксит, химические руды) — сказать; остальное (ключ и т.п.) — не наше.
            if (_tag.HasTag(args.Used, OreTag))
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("dystopia-crusher-not-crushable", ("item", args.Used)), ent, args.User);
            }

            return;
        }

        args.Handled = true;
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        var free = ent.Comp.Capacity - LoadedOre(ent);
        if (free <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dystopia-crusher-full"), ent, args.User);
            return;
        }

        var item = args.Used;
        if (TryComp<StackComponent>(item, out var stack) && stack.Count > free)
        {
            if (_stack.Split((item, stack), free, Transform(ent).Coordinates, args.User) is not { } split)
                return;

            item = split;
        }

        if (!_container.Insert(item, container))
            return;

        _audio.PlayPvs(ent.Comp.InsertSound, ent);
        _popup.PopupEntity(Loc.GetString("dystopia-crusher-loaded",
            ("item", item), ("loaded", LoadedOre(ent)), ("capacity", ent.Comp.Capacity)), ent, args.User);
    }

    // ------------------------------------------------------------------ ручная: крутить ворот

    private void OnInteractHand(Entity<DystopiaCrusherComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.Manual)
            return;

        args.Handled = true;
        TryStartCrank(ent, args.User);
    }

    private void OnGetVerbs(Entity<DystopiaCrusherComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (ent.Comp.Manual)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-crusher-crank-verb"),
                Act = () => TryStartCrank(ent, user),
                Priority = 1,
            });
        }

        if (LoadedOre(ent) > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dystopia-crusher-eject-verb"),
                Act = () => Eject(ent, user),
            });
        }
    }

    public void TryStartCrank(Entity<DystopiaCrusherComponent> ent, EntityUid user)
    {
        if (!CanWork(ent, user))
            return;

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.CycleTime,
            new DystopiaCrusherCrankDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            SetRunning(ent, true);
    }

    private void OnCrank(Entity<DystopiaCrusherComponent> ent, ref DystopiaCrusherCrankDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            SetRunning(ent, false);
            return;
        }

        Crush(ent);

        // Крутят, пока есть руда и пока не отойдут.
        args.Repeat = LoadedOre(ent) > 0;
        SetRunning(ent, args.Repeat);
    }

    private bool CanWork(Entity<DystopiaCrusherComponent> ent, EntityUid? user)
    {
        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-crusher-not-anchored"), ent, user.Value);
            return false;
        }

        if (LoadedOre(ent) <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dystopia-crusher-empty"), ent, user.Value);
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ электрические: сами

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DystopiaCrusherComponent>();
        while (query.MoveNext(out var uid, out var crusher))
        {
            if (crusher.Manual)
                continue;

            Entity<DystopiaCrusherComponent> ent = (uid, crusher);
            var canRun = _power.IsPowered(uid) && (!crusher.RequireAnchored || Transform(uid).Anchored);

            if (crusher.Running)
            {
                if (!canRun)
                {
                    SetRunning(ent, false); // пропал ток — цикл сорван, руда цела
                    continue;
                }

                if (now < crusher.CycleEnd)
                    continue;

                Crush(ent);
                SetRunning(ent, false);
            }

            if (canRun && LoadedOre(ent) > 0)
            {
                crusher.CycleEnd = now + TimeSpan.FromSeconds(crusher.CycleTime);
                SetRunning(ent, true);
            }
        }
    }

    // ------------------------------------------------------------------ цикл

    /// <summary>Раздробить до OresPerCycle кусков из загрузки. Выход — на месте дробилки.</summary>
    public void Crush(Entity<DystopiaCrusherComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        var left = ent.Comp.OresPerCycle;
        var outputs = new Dictionary<EntProtoId, int>();
        foreach (var item in new List<EntityUid>(container.ContainedEntities))
        {
            if (left <= 0)
                break;

            if (GetRecipe(item) is not { } recipe)
            {
                // Чужое в загрузке (не должно быть) — выкинуть, чтобы не застревало.
                _container.Remove(item, container);
                continue;
            }

            var take = Math.Min(left, _stack.GetCount(item));
            if (take <= 0)
                continue;

            outputs[recipe.Output] = outputs.GetValueOrDefault(recipe.Output) + take * recipe.Amount;
            left -= take;
            _stack.ReduceCount(item, take); // дошло до нуля — стопка удаляется сама и уже не считается
        }

        if (outputs.Count == 0)
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var (output, amount) in outputs)
        {
            foreach (var spawned in _stack.SpawnMultipleAtPosition(output, amount, coords))
            {
                _stack.TryMergeToContacts(spawned);
            }
        }

        _audio.PlayPvs(ent.Comp.CycleSound, ent);
    }

    private void Eject(Entity<DystopiaCrusherComponent> ent, EntityUid user)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        _container.EmptyContainer(container, destination: Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("dystopia-crusher-ejected"), ent, user);
        if (!ent.Comp.Manual)
            SetRunning(ent, false);
    }

    private void SetRunning(Entity<DystopiaCrusherComponent> ent, bool running)
    {
        ent.Comp.Running = running;
        _appearance.SetData(ent, DystopiaCrusherVisuals.Running, running);
        _ambient.SetAmbience(ent, running);
    }

    // ------------------------------------------------------------------ осмотр

    private void OnExamined(Entity<DystopiaCrusherComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dystopia-crusher-examine-load",
            ("loaded", LoadedOre(ent)), ("capacity", ent.Comp.Capacity)));
        args.PushMarkup(Loc.GetString("dystopia-crusher-examine-cycle",
            ("ores", ent.Comp.OresPerCycle), ("seconds", ent.Comp.CycleTime)));

        if (ent.Comp.RequireAnchored && !Transform(ent).Anchored)
            args.PushMarkup(Loc.GetString("dystopia-crusher-examine-loose"));
        else if (!ent.Comp.Manual && !_power.IsPowered(ent.Owner))
            args.PushMarkup(Loc.GetString("dystopia-crusher-examine-unpowered"));
    }
}
