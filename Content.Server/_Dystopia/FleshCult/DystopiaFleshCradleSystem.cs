using Content.Server.Popups;
using Content.Shared._Dystopia.FleshCult;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server._Dystopia.FleshCult;

/// <summary>
/// Колыбель Плоти: поглощает трупы (перетаскивание — в общей системе, здесь — само поглощение) и куски биомассы.
/// Вещи с трупа падают рядом. Накопив порог биомассы, Колыбель становится готова родить Посланника.
/// </summary>
public sealed partial class DystopiaFleshCradleSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private DystopiaFleshCradleSharedSystem _shared = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private AudioSystem _audio = default!;

    private static readonly SoundSpecifier FeedSound = new SoundPathSpecifier("/Audio/Effects/gib1.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DystopiaFleshCradleComponent, DystopiaCradleFeedDoAfterEvent>(OnFeedDoAfter);
        SubscribeLocalEvent<DystopiaFleshCradleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DystopiaFleshCradleComponent, ExaminedEvent>(OnExamined);
    }

    private void OnFeedDoAfter(Entity<DystopiaFleshCradleComponent> ent, ref DystopiaCradleFeedDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } body || !_shared.IsFeedableBody(body))
            return;

        args.Handled = true;

        DropEverything(body);

        var amount = HasComp<HumanoidProfileComponent>(body) ? ent.Comp.HumanoidBiomass : ent.Comp.CreatureBiomass;
        _popup.PopupEntity(Loc.GetString("dystopia-cradle-consumes", ("body", Name(body))), ent.Owner);
        _audio.PlayPvs(FeedSound, ent.Owner);
        QueueDel(body);
        AddBiomass(ent, amount);
    }

    private void OnInteractUsing(Entity<DystopiaFleshCradleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DystopiaBiomassComponent>(args.Used, out var biomass))
            return;

        args.Handled = true;
        _audio.PlayPvs(FeedSound, ent.Owner);
        QueueDel(args.Used);
        AddBiomass(ent, biomass.Amount);
    }

    /// <summary>Добавить биомассу в Колыбель (в том числе издалека — с наростов, этап 2).</summary>
    public void AddBiomass(Entity<DystopiaFleshCradleComponent> ent, float amount)
    {
        if (amount <= 0)
            return;

        ent.Comp.Biomass += amount;
        while (ent.Comp.Biomass >= ent.Comp.EnvoyThreshold)
        {
            ent.Comp.Biomass -= ent.Comp.EnvoyThreshold;
            ent.Comp.EnvoysReady++;
            _popup.PopupEntity(Loc.GetString("dystopia-cradle-envoy-ready"), ent.Owner, Content.Shared.Popups.PopupType.LargeCaution);
        }
    }

    /// <summary>Первая Колыбель на сервере (у Культа она одна).</summary>
    public bool TryGetCradle(out Entity<DystopiaFleshCradleComponent> cradle)
    {
        var query = EntityQueryEnumerator<DystopiaFleshCradleComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            cradle = (uid, comp);
            return true;
        }

        cradle = default;
        return false;
    }

    private void DropEverything(EntityUid body)
    {
        // Одежда и снаряжение падают на пол
        if (_inventory.TryGetContainerSlotEnumerator(body, out var enumerator))
        {
            var slots = new List<string>();
            while (enumerator.MoveNext(out var container, out var slot))
            {
                if (container.ContainedEntity != null)
                    slots.Add(slot.Name);
            }

            foreach (var slot in slots)
            {
                _inventory.TryUnequip(body, slot, silent: true, force: true);
            }
        }

        // И то, что было в руках
        foreach (var held in new List<EntityUid>(_hands.EnumerateHeld(body)))
        {
            _hands.TryDrop(body, held, checkActionBlocker: false);
        }
    }

    private void OnExamined(Entity<DystopiaFleshCradleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Подробности видят только культисты — остальным она просто мерзкая
        if (!HasComp<DystopiaFleshCultistComponent>(args.Examiner))
        {
            args.PushMarkup(Loc.GetString("dystopia-cradle-examine-outsider"));
            return;
        }

        var percent = (int) (ent.Comp.Biomass / ent.Comp.EnvoyThreshold * 100f);
        args.PushMarkup(Loc.GetString("dystopia-cradle-examine-cultist",
            ("biomass", (int) ent.Comp.Biomass), ("threshold", (int) ent.Comp.EnvoyThreshold), ("percent", percent)));
        if (ent.Comp.EnvoysReady > 0)
            args.PushMarkup(Loc.GetString("dystopia-cradle-examine-envoys", ("count", ent.Comp.EnvoysReady)));
    }
}
