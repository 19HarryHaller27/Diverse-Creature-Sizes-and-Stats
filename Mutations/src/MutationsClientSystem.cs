using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Mutations;

/// <summary>
/// Applies mutation visual state on the client (mesh size and glow level on entity client properties).
/// </summary>
public class MutationsClientSystem : ModSystem
{
    private ICoreClientAPI? capi;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Event.OnEntitySpawn += OnEntitySpawn;
        api.Event.OnEntityLoaded += OnEntityLoaded;
    }

    public override void Dispose()
    {
        if (capi is null)
        {
            return;
        }

        capi.Event.OnEntitySpawn -= OnEntitySpawn;
        capi.Event.OnEntityLoaded -= OnEntityLoaded;
        capi = null;
    }

    private void OnEntitySpawn(Entity entity)
    {
        ScheduleApply(entity);
    }

    private void OnEntityLoaded(Entity entity)
    {
        ScheduleApply(entity);
    }

    private void ScheduleApply(Entity entity)
    {
        if (capi is null)
        {
            return;
        }

        Entity captured = entity;
        capi.Event.RegisterCallback(_ =>
        {
            if (captured.World is null || !captured.Alive)
            {
                return;
            }

            ApplyMutationVisuals(captured);
        }, 1);
    }

    private static void ApplyMutationVisuals(Entity entity)
    {
        if (entity?.WatchedAttributes is null || !entity.WatchedAttributes.GetBool(MutationConstants.AttrMutApplied))
        {
            return;
        }

        if (entity.Properties?.Client is null)
        {
            return;
        }

        float scale = entity.WatchedAttributes.GetFloat(MutationConstants.AttrMutScale, 1f);
        if (scale <= 0f)
        {
            scale = 1f;
        }

        float baseSize = entity.WatchedAttributes.GetFloat(MutationConstants.AttrMutBaseClientSize, 0f);
        if (baseSize <= 0f)
        {
            baseSize = entity.Properties.Client.Size / scale;
            if (baseSize <= 0f)
            {
                baseSize = 1f;
            }
        }

        entity.Properties.Client.Size = Math.Max(0.01f, baseSize * scale);

        int glow = entity.WatchedAttributes.GetInt(MutationConstants.AttrMutGlow, 0);
        if (glow > 0)
        {
            entity.Properties.Client.GlowLevel = Math.Max(entity.Properties.Client.GlowLevel, glow);
        }
    }
}
