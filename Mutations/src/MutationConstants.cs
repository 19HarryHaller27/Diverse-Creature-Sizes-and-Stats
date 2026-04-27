using System.Collections.Generic;

namespace Mutations;

public static class MutationConstants
{
    /// <summary>Per-entity probability for natural (random) mutation. At most one roll per entity lifetime, on first spawn or load (whichever runs first).
    /// Mutation runs when <c>NextDouble() &lt;= MutationChance</c>.</summary>
    public const double MutationChance = 0.50;
    /// <summary>Extra loot copies for most mutation tiers (total drops ≈ this factor of vanilla).</summary>
    public const float LootMultiplierDefault = 2f;
    /// <summary>G75 top tier: total drops ≈ 3× vanilla from bonus stacks.</summary>
    public const float LootMultiplierTopTier = 3f;
    public const int GlowForG75 = 160;

    /// <summary>Set after the natural mutation pass runs once (success or fail), so spawn+load does not double the chance.</summary>
    public const string AttrMutNaturalAttempted = "mutations.naturalAttempted";

    public const string AttrMutApplied = "mutations.applied";
    public const string AttrMutTier = "mutations.tier";
    public const string AttrMutScale = "mutations.scale";
    public const string AttrMutDamage = "mutations.damageMult";
    public const string AttrMutLoot = "mutations.lootMult";
    public const string AttrMutGlow = "mutations.glow";
    /// <summary>Default entity client mesh size before tier scale (synced for client-side rendering).</summary>
    public const string AttrMutBaseClientSize = "mutations.baseClientSize";

    /// <summary>Legacy attribute key (no longer written; kept so old saves do not confuse tooling).</summary>
    public const string AttrMutMeleeBaselineTree = "mutations.meleeBaselines";

    public const string WalkSpeedLayer = "mutations-tier";
    public const string DebugPrefix = "[Mutations]";

    public static readonly HashSet<string> ExcludedCodeTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "fish",
        "salmon",
        "trout",
        "eel",
        "shark",
        "reef",
        "chicken",
        "duck",
        "goose",
        "penguin",
        "bird",
        "owl",
        "crow",
        "seagull"
    };

    public static readonly Dictionary<string, string> SpawnAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chicken"] = "chicken-hen",
        ["hare"] = "hare-european-adult-male",
        ["fox"] = "fox-red-adult-female",
        ["raccoon"] = "raccoon-common-adult-male",
        ["sheep"] = "sheep-mouflon-adult-male",
        ["goat"] = "goat-mountain-adult-male",
        ["pig"] = "pig-warthog-adult-male",
        ["deer"] = "deer-whitetail-adult-male",
        ["wolf"] = "wolf-eurasian-adult-male",
        ["polarbear"] = "bear-polar-adult-male"
    };
}
