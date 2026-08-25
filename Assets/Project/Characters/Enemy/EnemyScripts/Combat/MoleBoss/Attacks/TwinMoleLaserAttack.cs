using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class TwinMoleLaserAttack : IMoleBossAttack
    {
        private static readonly Color WarningColor = new(1f, 0.62f, 0.08f, 0.95f);
        private static readonly Color GlowColor = new(0.95f, 0.04f, 0.62f, 0.58f);
        private static readonly Color CoreColor = new(0.75f, 0.95f, 1f, 1f);

        public MoleBossAttack Id => MoleBossAttack.TwinMoleLaser;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            const float sideInset = 1.35f;
            minimum += Vector2.one * sideInset;
            maximum -= Vector2.one * sideInset;
            if (minimum.x >= maximum.x || minimum.y >= maximum.y) yield break;

            float startNormalizedY = Random.Range(0.2f, 0.8f);
            Vector2 leftPosition = new(minimum.x, Mathf.Lerp(minimum.y, maximum.y, startNormalizedY));
            Vector2 rightPosition = new(maximum.x, leftPosition.y);
            Sprite miniSprite = context.BossSprite != null ? context.BossSprite : context.Projectiles.ProjectileSprite;

            GameObject leftMole = CreateMiniMole(context, "Left laser mole", leftPosition, miniSprite, false);
            GameObject rightMole = CreateMiniMole(context, "Right laser mole", rightPosition, miniSprite, true);
            GameObject leftAura = context.Telegraphs.CreateCircle("Left mole energy ring", leftPosition, 0.8f,
                new Color(0.1f, 0.9f, 1f, 0.85f));
            GameObject rightAura = context.Telegraphs.CreateCircle("Right mole energy ring", rightPosition, 0.8f,
                new Color(1f, 0.12f, 0.72f, 0.85f));
            GameObject warningBeam = context.Telegraphs.CreateLine("Twin mole laser warning", WarningColor, 0.08f,
                leftPosition, rightPosition);

            context.SetState(MoleBossState.Telegraphing);
            context.PlaySound(context.Config.MinionSpawnSfx, 0.45f, 1.15f);
            context.PlaySound(context.Config.LaserChargeSfx, 0.28f, 0.72f);
            yield return AnimateEntrance(context, leftMole, rightMole, leftAura, rightAura, warningBeam,
                leftPosition, rightPosition);

            context.SetState(MoleBossState.Attacking);
            context.TriggerAttackAnimation();
            context.Telegraphs.Release(warningBeam);
            GameObject beamGlow = context.Telegraphs.CreateLine("Twin mole laser glow", GlowColor, 0.9f,
                leftPosition, rightPosition);
            GameObject beamCore = context.Telegraphs.CreateLine("Twin mole laser core", CoreColor, 0.22f,
                leftPosition, rightPosition);
            context.PlaySound(context.Config.LaserFireSfx, 0.62f, phase == 2 ? 0.9f : 0.82f);

            float elapsed = 0f;
            float damageCooldown = 0f;
            float duration = context.Config.TwinLaserDuration(phase);
            float arenaHeight = Mathf.Max(0.1f, maximum.y - minimum.y);
            float direction = Random.value < 0.5f ? -1f : 1f;
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    damageCooldown -= Time.deltaTime;
                    float sweep = Mathf.PingPong(startNormalizedY + direction *
                        elapsed * context.Config.TwinLaserMoveSpeed / arenaHeight, 1f);
                    float centerY = Mathf.Lerp(minimum.y, maximum.y, sweep);
                    float tilt = Mathf.Sin(elapsed * (phase == 2 ? 2.15f : 1.65f)) *
                                 context.Config.TwinLaserTilt;
                    leftPosition.y = Mathf.Clamp(centerY + tilt, minimum.y, maximum.y);
                    rightPosition.y = Mathf.Clamp(centerY - tilt, minimum.y, maximum.y);

                    UpdatePositions(leftMole, rightMole, leftAura, rightAura, beamGlow, beamCore,
                        leftPosition, rightPosition, elapsed);

                    if (damageCooldown <= 0f &&
                        DistanceToSegment(context.Player.Position, leftPosition, rightPosition) <=
                        context.Config.TwinLaserRadius)
                    {
                        if (context.Player.TryDamage(context.Config.TwinLaserDamage))
                        {
                            damageCooldown = context.Config.TwinLaserDamageCooldown;
                            Vector2 push = context.Player.Position.y >= (leftPosition.y + rightPosition.y) * 0.5f
                                ? Vector2.up : Vector2.down;
                            context.Player.ApplyKnockback(push * 7.5f, 0.18f);
                        }
                    }
                }
                yield return null;
            }

            context.Telegraphs.Release(beamGlow);
            context.Telegraphs.Release(beamCore);
            yield return AnimateExit(context, leftMole, rightMole, leftAura, rightAura);
            context.Telegraphs.Release(leftMole);
            context.Telegraphs.Release(rightMole);
            context.Telegraphs.Release(leftAura);
            context.Telegraphs.Release(rightAura);
        }

        private static GameObject CreateMiniMole(MoleBossCombatContext context, string name, Vector2 position,
            Sprite sprite, bool flipX)
        {
            Color tint = Color.Lerp(context.BossColor, flipX
                ? new Color(1f, 0.3f, 0.72f, 1f)
                : new Color(0.25f, 0.9f, 1f, 1f), 0.24f);
            GameObject mini = context.Telegraphs.CreateSprite(name, position, sprite, tint, 28);
            mini.transform.localScale = Vector3.zero;
            SpriteRenderer renderer = mini.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.flipX = flipX;
            return mini;
        }

        private static IEnumerator AnimateEntrance(MoleBossCombatContext context, GameObject leftMole,
            GameObject rightMole, GameObject leftAura, GameObject rightAura, GameObject warningBeam,
            Vector2 leftPosition, Vector2 rightPosition)
        {
            float elapsed = 0f;
            float duration = context.Config.TwinLaserWarningTime;
            LineRenderer warningLine = GetLine(warningBeam);
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float overshoot = Mathf.Sin(progress * Mathf.PI) * 0.14f;
                    float scale = (Mathf.SmoothStep(0f, context.Config.MiniMoleScale, progress) + overshoot);
                    if (leftMole != null) leftMole.transform.localScale = Vector3.one * scale;
                    if (rightMole != null) rightMole.transform.localScale = Vector3.one * scale;
                    UpdateCircle(leftAura, leftPosition, Mathf.Lerp(0.25f, 0.92f, progress));
                    UpdateCircle(rightAura, rightPosition, Mathf.Lerp(0.25f, 0.92f, progress));
                    if (warningLine != null)
                    {
                        warningLine.SetPosition(0, leftPosition);
                        warningLine.SetPosition(1, rightPosition);
                        float width = Mathf.Lerp(0.05f, 0.16f, progress) +
                                      Mathf.PingPong(elapsed * 0.12f, 0.06f);
                        warningLine.startWidth = width;
                        warningLine.endWidth = width;
                    }
                }
                yield return null;
            }
        }

        private static IEnumerator AnimateExit(MoleBossCombatContext context, GameObject leftMole,
            GameObject rightMole, GameObject leftAura, GameObject rightAura)
        {
            float elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float scale = Mathf.Lerp(context.Config.MiniMoleScale, 0f, elapsed / duration);
                    if (leftMole != null) leftMole.transform.localScale = Vector3.one * scale;
                    if (rightMole != null) rightMole.transform.localScale = Vector3.one * scale;
                    if (leftAura != null) leftAura.transform.localScale = Vector3.one * Mathf.Max(0f, scale * 2f);
                    if (rightAura != null) rightAura.transform.localScale = Vector3.one * Mathf.Max(0f, scale * 2f);
                }
                yield return null;
            }
        }

        private static void UpdatePositions(GameObject leftMole, GameObject rightMole, GameObject leftAura,
            GameObject rightAura, GameObject beamGlow, GameObject beamCore, Vector2 leftPosition,
            Vector2 rightPosition, float elapsed)
        {
            if (leftMole != null)
            {
                leftMole.transform.position = leftPosition;
                leftMole.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 3f) * 4f);
            }
            if (rightMole != null)
            {
                rightMole.transform.position = rightPosition;
                rightMole.transform.rotation = Quaternion.Euler(0f, 0f, -Mathf.Sin(elapsed * 3f) * 4f);
            }

            UpdateCircle(leftAura, leftPosition, 0.82f + Mathf.Sin(elapsed * 5f) * 0.12f);
            UpdateCircle(rightAura, rightPosition, 0.82f + Mathf.Sin(elapsed * 5f + Mathf.PI) * 0.12f);
            UpdateLine(beamGlow, leftPosition, rightPosition,
                0.82f + Mathf.Sin(elapsed * 18f) * 0.12f);
            UpdateLine(beamCore, leftPosition, rightPosition,
                0.2f + Mathf.Sin(elapsed * 24f) * 0.045f);
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
