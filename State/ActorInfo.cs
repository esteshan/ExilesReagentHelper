using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;

namespace ExilesReagentHelper.State;

/// <summary>
/// Animation / action information for an entity, read from its Actor component.
/// Useful for conditions like "don't recast while the cast animation is still playing".
/// </summary>
public sealed class ActorInfo
{
    private readonly Entity _entity;

    public ActorInfo(Entity entity) => _entity = entity;

    /// <summary>The current action flag (e.g. UsingAbility), or null if there's no Actor component.</summary>
    public string Action =>
        _entity.TryGetComponent<Actor>(out var actor) ? actor.Action.ToString() : null;

    /// <summary>The current animation name, or null if there's no Actor component.</summary>
    public string Animation =>
        _entity.TryGetComponent<Actor>(out var actor) ? actor.Animation.ToString() : null;

    public int CurrentAnimationId => TryGetAnimationController(out var ac) ? ac.CurrentAnimationId : -1;

    public int CurrentAnimationStage => TryGetAnimationController(out var ac) ? ac.CurrentAnimationStage : -1;

    public float AnimationProgress => TryGetAnimationController(out var ac) ? ac.AnimationProgress : 0f;

    private bool TryGetAnimationController(out AnimationController animationController)
    {
        if (_entity.TryGetComponent<Actor>(out var actor) && actor.AnimationController is { } ac ||
            _entity.TryGetComponent<Animated>(out var animated) &&
            animated.BaseAnimatedObjectEntity is { } baseEntity &&
            baseEntity.TryGetComponent(out ac))
        {
            animationController = ac;
            return true;
        }

        animationController = null;
        return false;
    }
}
