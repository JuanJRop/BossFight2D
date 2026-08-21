namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public enum MoleBossState
    {
        Dormant,
        Burrowing,
        Emerging,
        Telegraphing,
        Attacking,
        Recovering,
        PhaseTransition,
        Defeated
    }

    public enum MoleBossAttack
    {
        AimedFan,
        RadialBurst,
        Spiral,
        Corridor,
        Crossfire,
        RockRain,
        ChargeDash
    }
}
