using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class RockRainAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.RockRain;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            int spawned = 0;
            int total = context.Config.RockCount(phase);
            int rocksPerWave = phase == 2 ? 6 : 4;
            while (spawned < total)
            {
                int waveCount = Mathf.Min(rocksPerWave, total - spawned);
                List<RockMarker> markers = CreateMarkers(context, phase, waveCount);
                spawned += waveCount;
                context.SetState(MoleBossState.Telegraphing);

                float elapsed = 0f;
                while (elapsed < context.Config.RockWarningTime)
                {
                    if (!context.IsPaused)
                    {
                        elapsed += Time.deltaTime;
                        UpdateMarkers(markers, Mathf.Clamp01(elapsed / context.Config.RockWarningTime));
                    }
                    yield return null;
                }

                context.SetState(MoleBossState.Attacking);
                context.TriggerAttackAnimation();
                foreach (RockMarker marker in markers)
                {
                    ResolveImpact(context, marker.Target);
                    context.Telegraphs.Release(marker.Warning);
                    context.Telegraphs.Release(marker.Rock);
                }
                yield return context.Wait(phase == 2 ? 0.28f : 0.45f);
            }
        }

        private static List<RockMarker> CreateMarkers(MoleBossCombatContext context, int phase, int count)
        {
            List<RockMarker> markers = new(count);
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            float radius = context.Config.RockRadius;
            for (int i = 0; i < count; i++)
            {
                Vector2 target = i == 0
                    ? ClampToArena(context.Player.Position, minimum, maximum, radius)
                    : FindArenaTarget(markers, minimum, maximum, radius);
                GameObject warning = context.Telegraphs.CreateCircle("Rock impact warning", target, radius,
                    new Color(1f, 0.72f, 0.05f, 0.95f));
                GameObject rock = context.Telegraphs.CreatePrefab("Falling pixel rock",
                    context.Config.RockVisualPrefab, target + Vector2.up * 6f);
                if (rock == null)
                {
                    rock = context.Telegraphs.CreateSprite("Falling rock", target + Vector2.up * 6f,
                        context.Projectiles.ProjectileSprite, new Color(0.42f, 0.34f, 0.28f, 1f), 30);
                }
                rock.transform.localScale = Vector3.one * 0.85f;
                markers.Add(new RockMarker(target, warning, rock));
            }
            return markers;
        }

        private static void UpdateMarkers(IEnumerable<RockMarker> markers, float progress)
        {
            foreach (RockMarker marker in markers)
            {
                float eased = progress * progress;
                if (marker.Rock != null)
                {
                    marker.Rock.transform.position = Vector3.Lerp(marker.Target + Vector2.up * 6f, marker.Target, eased);
                    marker.Rock.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 2.8f, eased);
                }
                if (marker.Warning == null) continue;
                LineRenderer line = marker.Warning.GetComponent<LineRenderer>();
                if (line == null) continue;
                Color color = Color.Lerp(new Color(1f, 0.75f, 0.05f, 0.8f), new Color(1f, 0.05f, 0.02f, 1f), progress);
                line.startColor = color;
                line.endColor = color;
                line.startWidth = Mathf.Lerp(0.06f, 0.16f, progress);
                line.endWidth = line.startWidth;
            }
        }

        private static void ResolveImpact(MoleBossCombatContext context, Vector2 target)
        {
            context.Telegraphs.CreatePrefab("Rock impact FX", context.Config.RockImpactPrefab, target, 1.9f);
            if (Vector2.Distance(context.Player.Position, target) <= context.Config.RockRadius)
                context.Player.TryDamage(context.Config.RockDamage);
        }

        private static Vector2 FindArenaTarget(IReadOnlyList<RockMarker> markers, Vector2 minimum,
            Vector2 maximum, float radius)
        {
            Vector2 candidate = Vector2.zero;
            float minimumSpacing = radius * 1.7f;
            for (int attempt = 0; attempt < 18; attempt++)
            {
                candidate = new Vector2(Random.Range(minimum.x + radius, maximum.x - radius),
                    Random.Range(minimum.y + radius, maximum.y - radius));
                bool separated = true;
                for (int i = 0; i < markers.Count; i++)
                {
                    if (Vector2.Distance(candidate, markers[i].Target) >= minimumSpacing) continue;
                    separated = false;
                    break;
                }
                if (separated) break;
            }
            return candidate;
        }

        private static Vector2 ClampToArena(Vector2 target, Vector2 minimum, Vector2 maximum, float radius)
        {
            target.x = Mathf.Clamp(target.x, minimum.x + radius, maximum.x - radius);
            target.y = Mathf.Clamp(target.y, minimum.y + radius, maximum.y - radius);
            return target;
        }

        private readonly struct RockMarker
        {
            public RockMarker(Vector2 target, GameObject warning, GameObject rock)
            {
                Target = target;
                Warning = warning;
                Rock = rock;
            }
            public Vector2 Target { get; }
            public GameObject Warning { get; }
            public GameObject Rock { get; }
        }
    }
}
