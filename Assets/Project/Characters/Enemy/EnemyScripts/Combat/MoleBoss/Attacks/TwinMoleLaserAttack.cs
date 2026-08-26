using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class TwinMoleLaserAttack : IMoleBossAttack
    {
        private const int Left = 0;
        private const int Right = 1;
        private const int Bottom = 2;
        private const int Top = 3;
        private const int Horizontal = 0;
        private const int Vertical = 1;

        private static readonly Color WarningColor = new(1f, 0.62f, 0.08f, 0.95f);
        private static readonly Color GlowColor = new(0.95f, 0.04f, 0.62f, 0.58f);
        private static readonly Color CoreColor = new(0.75f, 0.95f, 1f, 1f);
        private static readonly Color CyanAccent = new(0.25f, 0.9f, 1f, 1f);
        private static readonly Color MagentaAccent = new(1f, 0.3f, 0.72f, 1f);

        public MoleBossAttack Id => MoleBossAttack.TwinMoleLaser;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            const float sideInset = 1.35f;
            minimum += Vector2.one * sideInset;
            maximum -= Vector2.one * sideInset;
            if (minimum.x >= maximum.x || minimum.y >= maximum.y) yield break;

            float startNormalizedY = Random.Range(0.22f, 0.78f);
            float startNormalizedX = Random.Range(0.22f, 0.78f);
            Vector2[] positions =
            {
                new(minimum.x, Mathf.Lerp(minimum.y, maximum.y, startNormalizedY)),
                new(maximum.x, Mathf.Lerp(minimum.y, maximum.y, startNormalizedY)),
                new(Mathf.Lerp(minimum.x, maximum.x, startNormalizedX), minimum.y),
                new(Mathf.Lerp(minimum.x, maximum.x, startNormalizedX), maximum.y)
            };

            Sprite miniSprite = context.BossSprite != null
                ? context.BossSprite
                : context.Projectiles.ProjectileSprite;
            GameObject[] moles =
            {
                CreateMiniMole(context, "Left laser mole", positions[Left], miniSprite, false, CyanAccent),
                CreateMiniMole(context, "Right laser mole", positions[Right], miniSprite, true, MagentaAccent),
                CreateMiniMole(context, "Bottom laser mole", positions[Bottom], miniSprite, false, MagentaAccent),
                CreateMiniMole(context, "Top laser mole", positions[Top], miniSprite, true, CyanAccent)
            };
            GameObject[] auras =
            {
                context.Telegraphs.CreateCircle("Left mole energy ring", positions[Left], 0.8f, CyanAccent),
                context.Telegraphs.CreateCircle("Right mole energy ring", positions[Right], 0.8f, MagentaAccent),
                context.Telegraphs.CreateCircle("Bottom mole energy ring", positions[Bottom], 0.8f, MagentaAccent),
                context.Telegraphs.CreateCircle("Top mole energy ring", positions[Top], 0.8f, CyanAccent)
            };
            GameObject[] warnings =
            {
                context.Telegraphs.CreateLine("Horizontal mole laser warning", WarningColor, 0.08f,
                    positions[Left], positions[Right]),
                context.Telegraphs.CreateLine("Vertical mole laser warning", WarningColor, 0.08f,
                    positions[Bottom], positions[Top])
            };

            context.SetState(MoleBossState.Telegraphing);
            context.PlaySound(context.Config.MinionSpawnSfx, 0.48f, 1.08f);
            context.PlaySound(context.Config.LaserChargeSfx, 0.32f, 0.72f);
            yield return AnimateEntrance(context, moles, auras, warnings, positions);

            context.SetState(MoleBossState.Attacking);
            context.TriggerAttackAnimation();
            ReleaseAll(context, warnings);

            GameObject[] beamGlows =
            {
                context.Telegraphs.CreateLine("Horizontal mole laser glow", GlowColor, 0.9f,
                    positions[Left], positions[Right]),
                context.Telegraphs.CreateLine("Vertical mole laser glow", GlowColor, 0.9f,
                    positions[Bottom], positions[Top])
            };
            GameObject[] beamCores =
            {
                context.Telegraphs.CreateLine("Horizontal mole laser core", CoreColor, 0.22f,
                    positions[Left], positions[Right]),
                context.Telegraphs.CreateLine("Vertical mole laser core", CoreColor, 0.22f,
                    positions[Bottom], positions[Top])
            };
            context.PlaySound(context.Config.LaserFireSfx, 0.68f, phase == 2 ? 0.9f : 0.82f);

            float elapsed = 0f;
            float damageCooldown = 0f;
            float duration = context.Config.TwinLaserDuration(phase);
            float arenaHeight = Mathf.Max(0.1f, maximum.y - minimum.y);
            float arenaWidth = Mathf.Max(0.1f, maximum.x - minimum.x);
            float horizontalPhase = Random.Range(0f, 1f);
            float verticalPhase = Random.Range(0f, 1f);

            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    damageCooldown -= Time.deltaTime;

                    float horizontalSweep = Mathf.PingPong(horizontalPhase +
                        elapsed * context.Config.TwinLaserMoveSpeed / arenaHeight, 1f);
                    float verticalSweep = Mathf.PingPong(verticalPhase +
                        elapsed * context.Config.TwinLaserMoveSpeed / arenaWidth, 1f);
                    float horizontalCenter = Mathf.Lerp(minimum.y, maximum.y, horizontalSweep);
                    float verticalCenter = Mathf.Lerp(minimum.x, maximum.x, verticalSweep);
                    float waveSpeed = phase == 2 ? 2.15f : 1.65f;
                    float horizontalTilt = Mathf.Sin(elapsed * waveSpeed) * context.Config.TwinLaserTilt;
                    float verticalTilt = Mathf.Cos(elapsed * waveSpeed * 1.08f) * context.Config.TwinLaserTilt;

                    positions[Left].y = Mathf.Clamp(horizontalCenter + horizontalTilt, minimum.y, maximum.y);
                    positions[Right].y = Mathf.Clamp(horizontalCenter - horizontalTilt, minimum.y, maximum.y);
                    positions[Bottom].x = Mathf.Clamp(verticalCenter - verticalTilt, minimum.x, maximum.x);
                    positions[Top].x = Mathf.Clamp(verticalCenter + verticalTilt, minimum.x, maximum.x);

                    UpdatePositions(moles, auras, beamGlows, beamCores, positions, elapsed);

                    float horizontalDistance = DistanceToSegment(context.Player.Position,
                        positions[Left], positions[Right]);
                    float verticalDistance = DistanceToSegment(context.Player.Position,
                        positions[Bottom], positions[Top]);
                    if (damageCooldown <= 0f &&
                        Mathf.Min(horizontalDistance, verticalDistance) <= context.Config.TwinLaserRadius)
                    {
                        if (context.Player.TryDamage(context.Config.TwinLaserDamage))
                        {
                            damageCooldown = context.Config.TwinLaserDamageCooldown;
                            Vector2 push;
                            if (horizontalDistance <= verticalDistance)
                            {
                                float centerY = (positions[Left].y + positions[Right].y) * 0.5f;
                                push = context.Player.Position.y >= centerY ? Vector2.up : Vector2.down;
                            }
                            else
                            {
                                float centerX = (positions[Bottom].x + positions[Top].x) * 0.5f;
                                push = context.Player.Position.x >= centerX ? Vector2.right : Vector2.left;
                            }
                            context.Player.ApplyKnockback(push * 7.5f, 0.18f);
                        }
                    }
                }
                yield return null;
            }

            ReleaseAll(context, beamGlows);
            ReleaseAll(context, beamCores);
            yield return AnimateExit(context, moles, auras);
            ReleaseAll(context, moles);
            ReleaseAll(context, auras);
        }

        private static GameObject CreateMiniMole(MoleBossCombatContext context, string name, Vector2 position,
            Sprite sprite, bool flipX, Color accent)
        {
            Color tint = Color.Lerp(context.BossColor, accent, 0.24f);
            GameObject mini = context.Telegraphs.CreateSprite(name, position, sprite, tint, 28);
            mini.transform.localScale = Vector3.zero;
            SpriteRenderer renderer = mini.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.flipX = flipX;
            return mini;
        }

        private static IEnumerator AnimateEntrance(MoleBossCombatContext context, GameObject[] moles,
            GameObject[] auras, GameObject[] warnings, Vector2[] positions)
        {
            float elapsed = 0f;
            float duration = context.Config.TwinLaserWarningTime;
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float overshoot = Mathf.Sin(progress * Mathf.PI) * 0.14f;
                    float scale = Mathf.SmoothStep(0f, context.Config.MiniMoleScale, progress) + overshoot;
                    for (int i = 0; i < moles.Length; i++)
                    {
                        if (moles[i] != null) moles[i].transform.localScale = Vector3.one * scale;
                        UpdateCircle(auras[i], positions[i], Mathf.Lerp(0.25f, 0.92f, progress));
                    }

                    float width = Mathf.Lerp(0.05f, 0.16f, progress) +
                                  Mathf.PingPong(elapsed * 0.12f, 0.06f);
                    UpdateLine(warnings[Horizontal], positions[Left], positions[Right], width);
                    UpdateLine(warnings[Vertical], positions[Bottom], positions[Top], width);
                }
                yield return null;
            }
        }

        private static IEnumerator AnimateExit(MoleBossCombatContext context, GameObject[] moles,
            GameObject[] auras)
        {
            float elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float scale = Mathf.Lerp(context.Config.MiniMoleScale, 0f, elapsed / duration);
                    for (int i = 0; i < moles.Length; i++)
                    {
                        if (moles[i] != null) moles[i].transform.localScale = Vector3.one * scale;
                        if (auras[i] != null)
                            auras[i].transform.localScale = Vector3.one * Mathf.Max(0f, scale * 2f);
                    }
                }
                yield return null;
            }
        }

        private static void UpdatePositions(GameObject[] moles, GameObject[] auras, GameObject[] beamGlows,
            GameObject[] beamCores, Vector2[] positions, float elapsed)
        {
            for (int i = 0; i < moles.Length; i++)
            {
                if (moles[i] != null)
                {
                    moles[i].transform.position = positions[i];
                    float direction = i % 2 == 0 ? 1f : -1f;
                    moles[i].transform.rotation = Quaternion.Euler(0f, 0f,
                        direction * Mathf.Sin(elapsed * 3f + i * 0.7f) * 4f);
                }
                UpdateCircle(auras[i], positions[i],
                    0.82f + Mathf.Sin(elapsed * 5f + i * Mathf.PI * 0.5f) * 0.12f);
            }

            UpdateLine(beamGlows[Horizontal], positions[Left], positions[Right],
                0.82f + Mathf.Sin(elapsed * 18f) * 0.12f);
            UpdateLine(beamCores[Horizontal], positions[Left], positions[Right],
                0.2f + Mathf.Sin(elapsed * 24f) * 0.045f);
            UpdateLine(beamGlows[Vertical], positions[Bottom], positions[Top],
                0.82f + Mathf.Sin(elapsed * 18f + Mathf.PI) * 0.12f);
            UpdateLine(beamCores[Vertical], positions[Bottom], positions[Top],
                0.2f + Mathf.Sin(elapsed * 24f + Mathf.PI) * 0.045f);
        }

        private static void ReleaseAll(MoleBossCombatContext context, GameObject[] visuals)
        {
            if (visuals == null) return;
            foreach (GameObject visual in visuals)
            {
                context.Telegraphs.Release(visual);
            }
        }

        private static void UpdateCircle(GameObject visual, Vector2 center, float radius)
        {
            MoleBossTelegraphService.UpdateCircle(GetLine(visual), center, Mathf.Max(0.05f, radius));
        }

        private static void UpdateLine(GameObject visual, Vector2 start, Vector2 end, float width)
        {
            LineRenderer line = GetLine(visual);
            if (line == null) return;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = Mathf.Max(0.02f, width);
            line.endWidth = line.startWidth;
        }

        private static LineRenderer GetLine(GameObject visual)
        {
            return visual != null ? visual.GetComponent<LineRenderer>() : null;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }
    }
}
