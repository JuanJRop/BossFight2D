using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Scripts.Progression;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class MinionHordeAttack : IMoleBossAttack
    {
        private static readonly Color WarningColor = new(0.95f, 0.34f, 0.08f, 0.9f);
        private static readonly Color PhaseOneTint = new(0.88f, 0.62f, 0.34f, 1f);
        private static readonly Color PhaseTwoTint = new(1f, 0.2f, 0.12f, 1f);

        public MoleBossAttack Id => MoleBossAttack.MinionHorde;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            minimum += Vector2.one * 0.85f;
            maximum -= Vector2.one * 0.85f;
            int count = context.Config.HordeCount(phase);
            List<Vector2> spawnPositions = BuildSpawnPositions(count, minimum, maximum);
            List<GameObject> warnings = new(count);
            List<GameObject> minions = new(count);

            context.SetState(MoleBossState.Burrowing);
            context.SetBossHidden(true);
            context.PlaySound(context.Config.MinionSpawnSfx, 0.65f, phase == 2 ? 0.82f : 0.96f);

            foreach (Vector2 position in spawnPositions)
                warnings.Add(context.Telegraphs.CreateCircle("Horde spawn warning", position, 0.72f, WarningColor));
            yield return context.Wait(0.55f);
            foreach (GameObject warning in warnings) context.Telegraphs.Release(warning);

            context.SetState(MoleBossState.Attacking);
            for (int i = 0; i < spawnPositions.Count; i++)
            {
                GameObject minion = CreateMinion(context, phase, i, spawnPositions[i], minimum, maximum);
                if (minion != null) minions.Add(minion);
                yield return context.Wait(phase == 2 ? 0.055f : 0.09f);
            }

            float elapsed = 0f;
            while (elapsed < context.Config.HordeMaxDuration)
            {
                if (!context.IsPaused) elapsed += Time.deltaTime;
                minions.RemoveAll(minion => minion == null);
                if (minions.Count == 0) break;
                yield return null;
            }

            foreach (GameObject minion in minions)
            {
                if (minion != null) context.Telegraphs.Release(minion);
            }
            context.SetBossHidden(false);
            context.SetState(MoleBossState.Emerging);
            yield return context.Wait(0.32f);
        }

        private static GameObject CreateMinion(MoleBossCombatContext context, int phase, int index,
            Vector2 position, Vector2 minimum, Vector2 maximum)
        {
            Sprite sprite = context.BossSprite ?? context.Projectiles.ProjectileSprite;
            if (sprite == null) return null;

            Color tint = Color.Lerp(context.BossColor, phase == 2 ? PhaseTwoTint : PhaseOneTint, 0.38f);
            GameObject minion = context.Telegraphs.CreateSprite("Burrowling " + (index + 1), position,
                sprite, tint, 12);
            minion.tag = "Enemy";
            minion.transform.localScale = Vector3.one * (phase == 2 ? 0.42f : 0.34f);

            Rigidbody2D body = minion.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = minion.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = phase == 2 ? 0.78f : 0.68f;

            Health health = minion.AddComponent<Health>();
            health.ConfigureRuntime(context.Config.MinionHealth(phase));
            MoleMinionEnemy behaviour = minion.AddComponent<MoleMinionEnemy>();
            behaviour.Configure(context.Player, health, minimum, maximum,
                context.Config.MinionSpeed(phase), context.Config.HordeContactDamage,
                context.Config.HordeContactCooldown, defeatedMinion =>
                {
                    PlayerEconomy.AddGold(context.Config.MinionGoldReward);
                    RunSession.AwardExperience(context.Config.MinionExperienceReward);
                    context.Telegraphs.Release(defeatedMinion);
                });
            return minion;
        }

        private static List<Vector2> BuildSpawnPositions(int count, Vector2 minimum, Vector2 maximum)
        {
            List<Vector2> positions = new(count);
            int perSide = Mathf.CeilToInt(count / 4f);
            for (int i = 0; i < count; i++)
            {
                int side = i % 4;
                float progress = ((i / 4) + 1f) / (perSide + 1f);
                Vector2 position = side switch
                {
                    0 => new Vector2(minimum.x, Mathf.Lerp(minimum.y, maximum.y, progress)),
                    1 => new Vector2(maximum.x, Mathf.Lerp(maximum.y, minimum.y, progress)),
                    2 => new Vector2(Mathf.Lerp(minimum.x, maximum.x, progress), minimum.y),
                    _ => new Vector2(Mathf.Lerp(maximum.x, minimum.x, progress), maximum.y)
                };
                positions.Add(position);
            }
            return positions;
        }
    }
}
