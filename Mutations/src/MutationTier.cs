namespace Mutations;

public sealed class MutationTier
{
    public string Code { get; }
    public float Scale { get; }
    public float HealthMult { get; }
    public float DamageMult { get; }
    public float WalkSpeedMult { get; }
    public bool GrantsGlow { get; }

    public MutationTier(string code, float scale, float healthMult, float damageMult, float walkSpeedMult, bool grantsGlow)
    {
        Code = code;
        Scale = scale;
        HealthMult = healthMult;
        DamageMult = damageMult;
        WalkSpeedMult = walkSpeedMult;
        GrantsGlow = grantsGlow;
    }
}
