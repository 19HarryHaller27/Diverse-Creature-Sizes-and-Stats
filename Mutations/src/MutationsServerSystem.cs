using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Mutations;

public class MutationsServerSystem : ModSystem
{
    private static readonly MutationTier[] Tiers =
    {
        new("S50", 0.50f, 0.62f, 0.70f, 0.88f, false),
        new("S25", 0.75f, 0.78f, 0.82f, 0.94f, false),
        new("G25", 1.25f, 1.18f, 1.10f, 1.06f, false),
        new("G50", 1.50f, 1.42f, 1.22f, 1.10f, false),
        new("G75", 1.75f, 1.75f, 1.75f, 1.75f, true)
    };

    private ICoreServerAPI? sapi;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        // Natural roll on spawn and/or load, but only once per entity (AttrMutNaturalAttempted).
        api.Event.OnEntitySpawn += OnEntitySpawn;
        api.Event.OnEntityLoaded += OnEntityLoaded;
        api.Event.OnEntityDeath += OnEntityDeath;

        foreach (KeyValuePair<string, string> alias in MutationConstants.SpawnAliases)
        {
            RegisterSpawnAlias(api, alias.Key, alias.Value);
        }
    }

    public override void Dispose()
    {
        if (sapi is null)
        {
            return;
        }

        sapi.Event.OnEntitySpawn -= OnEntitySpawn;
        sapi.Event.OnEntityLoaded -= OnEntityLoaded;
        sapi.Event.OnEntityDeath -= OnEntityDeath;
        sapi = null;
    }

    private static void RegisterSpawnAlias(ICoreServerAPI api, string alias, string entityCode)
    {
        api.ChatCommands
            .GetOrCreate(alias)
            .WithDescription($"Spawn forced-mutated {entityCode}. Usage: /{alias} [count]")
            .RequiresPlayer()
            .RequiresPrivilege("gamemode")
            .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
            .HandleWith(args => OnSpawnAlias(args, entityCode));
    }

    private static TextCommandResult OnSpawnAlias(TextCommandCallingArgs args, string entityCode)
    {
        if (args.Caller.Player is not IServerPlayer sp || sp.Entity is null)
        {
            return TextCommandResult.Error("Server player entity unavailable.", "noplayer");
        }

        if (sp.Entity.Api is not ICoreServerAPI sapi)
        {
            return TextCommandResult.Error("Server API unavailable.", "nosapi");
        }

        int count = 1;
        if (args.Parsers is not null && args.Parsers.Count > 0)
        {
            object? parsed = args[0];
            if (parsed is int parsedCount)
            {
                count = GameMath.Clamp(parsedCount, 1, 25);
            }
            else if (parsed is long parsedLong)
            {
                count = GameMath.Clamp((int)parsedLong, 1, 25);
            }
            else if (parsed is IConvertible conv)
            {
                try
                {
                    count = GameMath.Clamp(conv.ToInt32(CultureInfo.InvariantCulture), 1, 25);
                }
                catch
                {
                    // keep default count
                }
            }
        }

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TrySpawnForcedMutation(sapi, sp, entityCode, out string? err))
            {
                return TextCommandResult.Error(err ?? "Spawn failed.", "spawnfail");
            }
            spawned++;
        }

        return TextCommandResult.Success($"{MutationConstants.DebugPrefix} Spawned {spawned} mutated {entityCode}.");
    }

    private static bool TrySpawnForcedMutation(ICoreServerAPI sapi, IServerPlayer sp, string entityCode, out string? error)
    {
        error = null;
        EntityProperties? props = sapi.World.GetEntityType(new AssetLocation(entityCode));
        if (props is null)
        {
            error = $"Unknown entity code: {entityCode}";
            return false;
        }

        Entity? entity = sapi.World.ClassRegistry.CreateEntity(props);
        if (entity is null)
        {
            error = $"Failed to instantiate entity: {entityCode}";
            return false;
        }

        Vec3d origin = sp.Entity!.Pos.XYZ;
        Vec3f facing3 = sp.Entity.Pos.GetViewVector();
        Vec3d facing = new(facing3.X, facing3.Y, facing3.Z);
        Vec3d pos = new(
            origin.X + facing.X * 3 + (sapi.World.Rand.NextDouble() * 2 - 1),
            origin.Y + 0.3,
            origin.Z + facing.Z * 3 + (sapi.World.Rand.NextDouble() * 2 - 1));

        entity.Pos.SetPos(pos);
        entity.Pos.Yaw = (float)(sapi.World.Rand.NextDouble() * GameMath.TWOPI);

        sapi.World.SpawnEntity(entity);
        ForceMutateEntity(entity);
        return true;
    }

    private void OnEntitySpawn(Entity entity)
    {
        if (entity.WatchedAttributes.GetBool(MutationConstants.AttrMutApplied))
        {
            return;
        }

        TryAttemptNaturalMutation(entity);
    }

    private void OnEntityLoaded(Entity entity)
    {
        if (entity.WatchedAttributes.GetBool(MutationConstants.AttrMutApplied))
        {
            ReapplyMutationPhysics(entity);
            return;
        }

        TryAttemptNaturalMutation(entity);
    }

    private void TryAttemptNaturalMutation(Entity entity)
    {
        if (sapi is null || entity is EntityPlayer)
        {
            return;
        }

        if (entity.WatchedAttributes.GetBool(MutationConstants.AttrMutApplied)
            || entity.WatchedAttributes.GetBool(MutationConstants.AttrMutNaturalAttempted))
        {
            return;
        }

        entity.WatchedAttributes.SetBool(MutationConstants.AttrMutNaturalAttempted, true);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutNaturalAttempted);

        if (!CanMutate(entity, applyCodeExclusions: true))
        {
            return;
        }

        if (sapi.World.Rand.NextDouble() > MutationConstants.MutationChance)
        {
            return;
        }

        ApplyTier(entity, PickRandomTier(sapi.World.Rand), announce: false);
    }

    private static void ForceMutateEntity(Entity entity)
    {
        if (entity is null || entity is EntityPlayer || entity.World is null)
        {
            return;
        }

        MutationTier tier = PickRandomTier(entity.World.Rand);
        ApplyTier(entity, tier, announce: true);
    }

    private static MutationTier PickRandomTier(Random rand)
    {
        int idx = rand.Next(0, Tiers.Length);
        return Tiers[idx];
    }

    /// <param name="applyCodeExclusions">When true, fish/bird/poultry codes are skipped (natural spawns). Debug <c>/chicken</c> etc. use forced path instead.</param>
    private static bool CanMutate(Entity entity, bool applyCodeExclusions)
    {
        if (entity is null || entity.WatchedAttributes.GetBool(MutationConstants.AttrMutApplied))
        {
            return false;
        }

        if (entity is EntityPlayer)
        {
            return false;
        }

        string code = entity.Code?.Path ?? string.Empty;
        if (code.Length == 0)
        {
            return false;
        }

        if (!applyCodeExclusions)
        {
            return true;
        }

        foreach (string token in MutationConstants.ExcludedCodeTokens)
        {
            if (code.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyTier(Entity entity, MutationTier tier, bool announce)
    {
        if (entity.Properties?.Client is not null)
        {
            float existingBase = entity.WatchedAttributes.GetFloat(MutationConstants.AttrMutBaseClientSize, 0f);
            if (existingBase <= 0f)
            {
                float baseSize = entity.Properties.Client.Size;
                entity.WatchedAttributes.SetFloat(MutationConstants.AttrMutBaseClientSize, baseSize);
                entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutBaseClientSize);
            }
        }

        entity.WatchedAttributes.SetBool(MutationConstants.AttrMutApplied, true);
        entity.WatchedAttributes.SetString(MutationConstants.AttrMutTier, tier.Code);
        entity.WatchedAttributes.SetFloat(MutationConstants.AttrMutScale, tier.Scale);
        entity.WatchedAttributes.SetFloat(MutationConstants.AttrMutDamage, tier.DamageMult);
        float lootMult = tier.GrantsGlow
            ? MutationConstants.LootMultiplierTopTier
            : MutationConstants.LootMultiplierDefault;
        entity.WatchedAttributes.SetFloat(MutationConstants.AttrMutLoot, lootMult);
        entity.WatchedAttributes.SetInt(MutationConstants.AttrMutGlow, tier.GrantsGlow ? MutationConstants.GlowForG75 : 0);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutApplied);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutTier);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutScale);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutDamage);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutLoot);
        entity.WatchedAttributes.MarkPathDirty(MutationConstants.AttrMutGlow);

        ScaleEntityFrame(entity, tier.Scale);
        ApplyWalkSpeed(entity, tier.WalkSpeedMult);
        ApplyHealth(entity, tier.HealthMult);
        ApplyServerVisualSize(entity, tier.Scale);

        if (entity.Api is ICoreServerAPI sapiLog)
        {
            string msg =
                $"{MutationConstants.DebugPrefix} {(announce ? "Forced" : "Natural")} mutation {entity.Code} -> {tier.Code} " +
                $"(scale={tier.Scale.ToString(CultureInfo.InvariantCulture)})";
            if (announce)
            {
                sapiLog.Logger.Notification(msg);
            }
            else
            {
                sapiLog.Logger.Debug(msg);
            }
        }
    }

    /// <summary>
    /// Scale hitboxes from the entity type template SpawnCollisionBox, not from the live boxes,
    /// so we never compound scale and we match post-init collision rebuilds.
    /// </summary>
    private static void ScaleEntityFrame(Entity entity, float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        float s = Math.Max(0.1f, scale);
        if (!TryGetTemplateCollisionCuboid(entity, out Cuboidf baseColl))
        {
            return;
        }

        Cuboidf scaled = ScaleCuboid(baseColl, s);
        entity.CollisionBox = scaled.Clone();
        entity.OriginCollisionBox = scaled.Clone();
        entity.SelectionBox = scaled.Clone();
        entity.OriginSelectionBox = scaled.Clone();
    }

    private static bool TryGetTemplateCollisionCuboid(Entity entity, out Cuboidf box)
    {
        if (entity.Properties is null)
        {
            box = new Cuboidf(0f, 0f, 0f, 0.01f, 0.01f, 0.01f);
            return false;
        }

        box = entity.Properties.SpawnCollisionBox;
        float w = box.X2 - box.X1;
        float h = box.Y2 - box.Y1;
        float d = box.Z2 - box.Z1;
        return w > 1e-4f && h > 1e-4f && d > 1e-4f;
    }

    private static void ReapplyMutationPhysics(Entity entity)
    {
        float scale = entity.WatchedAttributes.GetFloat(MutationConstants.AttrMutScale, 1f);
        if (scale <= 0f)
        {
            scale = 1f;
        }

        ScaleEntityFrame(entity, scale);
        MutationTier? tier = FindTierByCode(entity.WatchedAttributes.GetString(MutationConstants.AttrMutTier, string.Empty));
        ApplyWalkSpeed(entity, tier?.WalkSpeedMult ?? scale);
        ApplyServerVisualSize(entity, scale);
    }

    private static MutationTier? FindTierByCode(string code)
    {
        for (int i = 0; i < Tiers.Length; i++)
        {
            if (string.Equals(Tiers[i].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return Tiers[i];
            }
        }

        return null;
    }

    private static Cuboidf ScaleCuboid(Cuboidf box, float scale)
    {
        float s = Math.Max(0.1f, scale);
        return new Cuboidf(box.X1 * s, box.Y1 * s, box.Z1 * s, box.X2 * s, box.Y2 * s, box.Z2 * s);
    }

    private static void ApplyWalkSpeed(Entity entity, float mult)
    {
        float clamped = Math.Max(0.25f, mult);
        entity.Stats.Set("walkspeed", MutationConstants.WalkSpeedLayer, clamped, persistent: false);
    }

    private static void ApplyHealth(Entity entity, float mult)
    {
        ITreeAttribute? health = entity.WatchedAttributes.GetTreeAttribute("health");
        if (health is null)
        {
            return;
        }

        float max = health.GetFloat("maxhealth", 0f);
        float cur = health.GetFloat("currenthealth", 0f);
        if (max <= 0f)
        {
            return;
        }

        float newMax = Math.Max(1f, max * mult);
        float ratio = cur <= 0f ? 1f : GameMath.Clamp(cur / max, 0f, 1f);
        float newCur = Math.Max(1f, newMax * ratio);
        health.SetFloat("maxhealth", newMax);
        health.SetFloat("currenthealth", newCur);
        entity.WatchedAttributes.MarkPathDirty("health");
    }

    /// <summary>Keeps integrated server aligned; clients apply visible scale in MutationsClientSystem.</summary>
    private static void ApplyServerVisualSize(Entity entity, float scale)
    {
        if (entity.Properties?.Client is null || scale <= 0f)
        {
            return;
        }

        float baseSize = entity.WatchedAttributes.GetFloat(MutationConstants.AttrMutBaseClientSize, 0f);
        if (baseSize <= 0f)
        {
            baseSize = entity.Properties.Client.Size;
        }

        entity.Properties.Client.Size = Math.Max(0.01f, baseSize * scale);
    }

    private static void OnEntityDeath(Entity entity, DamageSource? damageSource)
    {
        if (!entity.WatchedAttributes.GetBool(MutationConstants.AttrMutApplied))
        {
            return;
        }

        if (entity.World is null)
        {
            return;
        }

        IPlayer? byPlayer = damageSource?.GetCauseEntity() is EntityPlayer playerEntity ? playerEntity.Player : null;
        ItemStack[]? drops = entity.GetDrops(entity.World, entity.Pos.AsBlockPos, byPlayer);
        if (drops is null || drops.Length == 0)
        {
            return;
        }

        float lootMult = entity.WatchedAttributes.GetFloat(
            MutationConstants.AttrMutLoot,
            MutationConstants.LootMultiplierDefault);
        if (!float.IsFinite(lootMult) || lootMult < 0f)
        {
            lootMult = MutationConstants.LootMultiplierDefault;
        }

        int extraCopies = Math.Max(0, (int)Math.Round(lootMult, MidpointRounding.AwayFromZero) - 1);

        Vec3d pos = entity.Pos.XYZ.AddCopy(0, 0.2, 0);
        for (int i = 0; i < drops.Length; i++)
        {
            ItemStack stack = drops[i];
            if (stack is null || stack.StackSize <= 0)
            {
                continue;
            }

            for (int c = 0; c < extraCopies; c++)
            {
                ItemStack bonus = stack.Clone();
                entity.World.SpawnItemEntity(bonus, pos, new Vec3d(
                    (entity.World.Rand.NextDouble() * 2 - 1) * 0.03,
                    0.07 + entity.World.Rand.NextDouble() * 0.03,
                    (entity.World.Rand.NextDouble() * 2 - 1) * 0.03));
            }
        }
    }
}
