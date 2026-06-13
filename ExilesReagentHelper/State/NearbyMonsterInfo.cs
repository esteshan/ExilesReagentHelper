using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace ExilesAutoCore.State;

/// <summary>
/// The hostile monsters within range of the player, bucketed by integer distance so that
/// "how many monsters within N units" queries are fast. Friendly monsters are kept separately.
/// Built once per frame and queried by the rule conditions.
/// </summary>
public sealed class NearbyMonsterInfo
{
    private readonly SortedDictionary<int, List<MonsterInfo>> _hostilesByDistance = new();

    public IReadOnlyCollection<MonsterInfo> FriendlyMonsters { get; }

    public NearbyMonsterInfo(GameController controller, int maxMonsterRange)
    {
        var friendly = new List<MonsterInfo>();
        FriendlyMonsters = friendly;

        if (controller?.Player == null || !controller.Player.HasComponent<Render>())
        {
            return;
        }

        foreach (var entity in controller.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
        {
            if (!IsValidMonster(entity, maxMonsterRange, checkIsAlive: true, desiredIsHiddenValue: false))
            {
                continue;
            }

            var monster = new MonsterInfo(controller, entity);
            if (!entity.IsHostile)
            {
                friendly.Add(monster);
                continue;
            }

            var distance = (int)Math.Ceiling(entity.DistancePlayer);
            if (_hostilesByDistance.TryGetValue(distance, out var list))
            {
                list.Add(monster);
            }
            else
            {
                _hostilesByDistance[distance] = [monster];
            }
        }
    }

    /// <summary>
    /// Whether an entity counts as a real, targetable monster within range. Mirrors the set of
    /// component checks ReAgent uses; <paramref name="desiredIsHiddenValue"/> selects normal vs
    /// "hidden" monsters (the latter are spawned-but-not-yet-active entities).
    /// </summary>
    public static bool IsValidMonster(Entity entity, int maxMonsterRange, bool checkIsAlive, bool desiredIsHiddenValue) =>
        entity.DistancePlayer <= maxMonsterRange &&
        entity.HasComponent<Monster>() &&
        entity.HasComponent<Positioned>() &&
        entity.HasComponent<Render>() &&
        entity.HasComponent<Life>() &&
        (!checkIsAlive || entity.IsAlive) &&
        entity.HasComponent<ObjectMagicProperties>() &&
        entity.TryGetComponent<Buffs>(out var buffs) &&
        desiredIsHiddenValue == buffs.HasBuff("hidden_monster");

    public int GetMonsterCount(int range, MonsterRarity rarity) => GetMonsters(range, rarity).Count();

    public IEnumerable<MonsterInfo> GetMonsters(int range, MonsterRarity rarity) =>
        _hostilesByDistance
            .TakeWhile(bucket => bucket.Key <= range)
            .SelectMany(bucket => bucket.Value)
            .Where(monster => (monster.Rarity & rarity) != 0);
}
