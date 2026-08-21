using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleProjectileVisual : MonoBehaviour
    {
        private SpriteRenderer core;
        private SpriteRenderer glow;
        private TrailRenderer trail;
        private Light2D light2D;

        private void Awake()
        {
            trail = GetComponent<TrailRenderer>();
            light2D = GetComponentInChildren<Light2D>(true);
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "AnimatedFireball") core = renderer;
                else if (renderer.gameObject.name == "FireGlow") glow = renderer;
            }
        }

        public void Apply(MoleProjectilePalette palette)
        {
            ResolvePalette(palette, out Color coreColor, out Color glowColor, out Color trailEnd);
            if (core != null) core.color = coreColor;
            if (glow != null) glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.42f);
            if (trail != null)
            {
                trail.Clear();
                trail.startColor = coreColor;
                trail.endColor = new Color(trailEnd.r, trailEnd.g, trailEnd.b, 0f);
            }
            if (light2D != null)
            {
                light2D.color = glowColor;
                light2D.intensity = palette == MoleProjectilePalette.Acid ? 1.55f : 1.35f;
            }
        }

        private static void ResolvePalette(MoleProjectilePalette palette, out Color coreColor,
            out Color glowColor, out Color trailEnd)
        {
            switch (palette)
            {
                case MoleProjectilePalette.Cyan:
                    coreColor = new Color(0.55f, 1f, 1f, 1f);
                    glowColor = new Color(0.02f, 0.82f, 1f, 1f);
                    trailEnd = new Color(0.08f, 0.25f, 1f, 1f);
                    break;
                case MoleProjectilePalette.Violet:
                    coreColor = new Color(0.95f, 0.68f, 1f, 1f);
                    glowColor = new Color(0.58f, 0.08f, 1f, 1f);
                    trailEnd = new Color(0.18f, 0.02f, 0.65f, 1f);
                    break;
                case MoleProjectilePalette.Acid:
                    coreColor = new Color(0.9f, 1f, 0.35f, 1f);
                    glowColor = new Color(0.35f, 1f, 0.04f, 1f);
                    trailEnd = new Color(0.02f, 0.48f, 0.12f, 1f);
                    break;
                case MoleProjectilePalette.Rose:
                    coreColor = new Color(1f, 0.62f, 0.88f, 1f);
                    glowColor = new Color(1f, 0.04f, 0.58f, 1f);
                    trailEnd = new Color(0.55f, 0.01f, 0.42f, 1f);
                    break;
                default:
                    coreColor = new Color(1f, 0.82f, 0.42f, 1f);
                    glowColor = new Color(1f, 0.1f, 0.02f, 1f);
                    trailEnd = new Color(0.72f, 0.02f, 0.02f, 1f);
                    break;
            }
        }
    }
}
