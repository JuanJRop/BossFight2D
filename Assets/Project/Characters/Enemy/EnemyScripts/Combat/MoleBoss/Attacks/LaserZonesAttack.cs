using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class LaserZonesAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.LaserZones;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            int waves = context.Config.LaserWaves(phase);
            int beamsPerWave = context.Config.LasersPerWave(phase);

            for (int wave = 0; wave < waves; wave++)
            {
                List<Beam> beams = CreateBeams(context, phase, beamsPerWave, minimum, maximum);
                context.SetState(MoleBossState.Telegraphing);
                yield return RunWarning(context, beams, phase);

                context.SetState(MoleBossState.Attacking);
                context.TriggerAttackAnimation();
                Activate(beams, context, phase);
                yield return RunActiveBeams(context, beams, phase);
                Release(context, beams);
                yield return context.Wait(phase == 2 ? 0.2f : 0.32f);
            }
        }

        private static List<Beam> CreateBeams(MoleBossCombatContext context, int phase, int count,
            Vector2 minimum, Vector2 maximum)
        {
            List<Beam> beams = new(count);
            for (int i = 0; i < count; i++)
            {
                GetRandomPath(minimum, maximum, out Vector2 start, out Vector2 end);
                Color warningColor = phase == 2
                    ? new Color(1f, 0.12f, 0.75f, 0.82f)
                    : new Color(0.05f, 0.9f, 1f, 0.82f);
                GameObject warning = context.Telegraphs.CreateLine("Laser target zone", warningColor, 0.12f, start, end);
                beams.Add(new Beam(start, end, warning));
            }
            return beams;
        }

        private static IEnumerator RunWarning(MoleBossCombatContext context, IEnumerable<Beam> beams, int phase)
        {
            float duration = context.Config.LaserWarning(phase);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float pulse = 0.1f + Mathf.PingPong(elapsed * 0.45f, 0.16f);
                    foreach (Beam beam in beams)
                    {
                        SetWidth(beam.Warning, pulse);
                        SetAlpha(beam.Warning, Mathf.Lerp(0.55f, 1f, progress));
                    }
                }
                yield return null;
            }
        }

        private static void Activate(IEnumerable<Beam> beams, MoleBossCombatContext context, int phase)
        {
            Color glowColor = phase == 2
                ? new Color(1f, 0.02f, 0.58f, 0.48f)
                : new Color(0.02f, 0.72f, 1f, 0.48f);
            foreach (Beam beam in beams)
            {
                context.Telegraphs.Release(beam.Warning);
                beam.Glow = context.Telegraphs.CreateLine("Laser glow", glowColor,
                    context.Config.LaserWidth(phase) * 1.7f, beam.Start, beam.End);
                beam.Core = context.Telegraphs.CreateLine("Laser core", Color.white,
                    context.Config.LaserWidth(phase) * 0.28f, beam.Start, beam.End);
            }
        }

        private static IEnumerator RunActiveBeams(MoleBossCombatContext context, IEnumerable<Beam> beams, int phase)
        {
            float duration = context.Config.LaserActiveTime(phase);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float pulse = 0.88f + Mathf.Sin(elapsed * 28f) * 0.12f;
                    foreach (Beam beam in beams)
                    {
                        SetWidth(beam.Glow, context.Config.LaserWidth(phase) * 1.7f * pulse);
                        if (beam.HitPlayer) continue;
                        if (DistanceToSegment(context.Player.Position, beam.Start, beam.End) >
                            context.Config.LaserWidth(phase) * 0.5f) continue;
                        beam.HitPlayer = context.Player.TryDamage(context.Config.LaserDamage(phase));
                    }
                }
                yield return null;
            }
        }

        private static void Release(MoleBossCombatContext context, IEnumerable<Beam> beams)
        {
            foreach (Beam beam in beams)
            {
                context.Telegraphs.Release(beam.Warning);
                context.Telegraphs.Release(beam.Glow);
                context.Telegraphs.Release(beam.Core);
            }
        }

        private static void GetRandomPath(Vector2 minimum, Vector2 maximum, out Vector2 start, out Vector2 end)
        {
            switch (Random.Range(0, 4))
            {
                case 0:
                    float y = Random.Range(minimum.y, maximum.y);
                    start = new Vector2(minimum.x, y);
                    end = new Vector2(maximum.x, y);
                    break;
                case 1:
                    float x = Random.Range(minimum.x, maximum.x);
                    start = new Vector2(x, minimum.y);
                    end = new Vector2(x, maximum.y);
                    break;
                case 2:
                    start = new Vector2(minimum.x, Random.Range(minimum.y, maximum.y * 0.65f));
                    end = new Vector2(maximum.x, Random.Range(minimum.y * 0.65f, maximum.y));
                    break;
                default:
                    start = new Vector2(minimum.x, Random.Range(minimum.y * 0.65f, maximum.y));
                    end = new Vector2(maximum.x, Random.Range(minimum.y, maximum.y * 0.65f));
                    break;
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude < 0.001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void SetWidth(GameObject visual, float width)
        {
            if (visual == null) return;
            LineRenderer line = visual.GetComponent<LineRenderer>();
            if (line == null) return;
            line.startWidth = width;
            line.endWidth = width;
        }

        private static void SetAlpha(GameObject visual, float alpha)
        {
            if (visual == null) return;
            LineRenderer line = visual.GetComponent<LineRenderer>();
            if (line == null) return;
            Color start = line.startColor;
            Color end = line.endColor;
            start.a = alpha;
            end.a = alpha;
            line.startColor = start;
            line.endColor = end;
        }

        private sealed class Beam
        {
            public Beam(Vector2 start, Vector2 end, GameObject warning)
            {
                Start = start;
                End = end;
                Warning = warning;
            }

            public Vector2 Start { get; }
            public Vector2 End { get; }
            public GameObject Warning { get; }
            public GameObject Glow { get; set; }
            public GameObject Core { get; set; }
            public bool HitPlayer { get; set; }
        }
    }
}
