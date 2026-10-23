using Content.Server.Parallax;
using Content.Server.Station.Components;
using Content.Server.Station.Events;

namespace Content.Server.Station.Systems;

public sealed partial class StationBiomeSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationBiomeComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<StationBiomeComponent> map, ref StationPostInitEvent args)
    {
        var station = _station.GetLargestGrid(map.Owner);
        if (station == null)
            return;

        var mapId = Transform(station.Value).MapID;
        var mapUid = _map.GetMapOrInvalid(mapId);

        _biome.EnsurePlanet(mapUid, ProtoMan.Index(map.Comp.Biome), map.Comp.Seed, mapLight: map.Comp.MapLightColor);

        // Dystopia-Ores: жилы руды в породе вокруг станции.
        if (map.Comp.MarkerLayers.Count > 0 && TryComp<Content.Shared.Parallax.Biomes.BiomeComponent>(mapUid, out var biome))
        {
            foreach (var layer in map.Comp.MarkerLayers)
            {
                _biome.AddMarkerLayer(mapUid, biome, layer);
            }
        }
    }
}
