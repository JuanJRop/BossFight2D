using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Movement
{
    public sealed class PlayerElectricStunFeedback : MonoBehaviour
    {
        private const int BoltCount = 4;
        private readonly LineRenderer[] bolts = new LineRenderer[BoltCount];
        private Material material;
        private float visibleUntil;
        private float redrawTimer;

        public void Show(float duration)
        {
            EnsureBuilt();
            visibleUntil = Mathf.Max(visibleUntil, Time.time + Mathf.Max(0.05f, duration));
            redrawTimer = 0f;
            SetVisible(true);
        }

        private void Update()
        {
            if (Time.time >= visibleUntil)
            {
                SetVisible(false);
                return;
            }

            redrawTimer -= Time.deltaTime;
            if (redrawTimer > 0f) return;
            redrawTimer = 0.045f;
            RedrawBolts();
        }

        private void EnsureBuilt()
        {
            if (bolts[0] != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader) { name = "Player Electric Stun" };

            SpriteRenderer playerRenderer = GetComponentInChildren<SpriteRenderer>();
            for (int i = 0; i < BoltCount; i++)
            {
                GameObject boltObject = new("Electric Stun Bolt " + (i + 1));
                boltObject.transform.SetParent(transform, false);
                LineRenderer line = boltObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 5;
                line.startWidth = 0.075f;
                line.endWidth = 0.025f;
                line.startColor = i % 2 == 0 ? new Color(0.32f, 0.95f, 1f, 1f) : Color.white;
                line.endColor = new Color(0.25f, 0.58f, 1f, 0.08f);
                line.material = material;
                if (playerRenderer != null)
                {
                    line.sortingLayerID = playerRenderer.sortingLayerID;
                    line.sortingOrder = playerRenderer.sortingOrder + 4;
                }
                bolts[i] = line;
            }
        }

        private void RedrawBolts()
        {
            for (int i = 0; i < bolts.Length; i++)
            {
                LineRenderer line = bolts[i];
                if (line == null) continue;
                float startAngle = Random.Range(0f, Mathf.PI * 2f);
                float endAngle = startAngle + Random.Range(0.75f, 1.45f) * (Random.value < 0.5f ? -1f : 1f);
                Vector2 start = new(Mathf.Cos(startAngle) * 0.5f, Mathf.Sin(startAngle) * 0.68f);
                Vector2 end = new(Mathf.Cos(endAngle) * 0.58f, Mathf.Sin(endAngle) * 0.76f);
                for (int point = 0; point < line.positionCount; point++)
                {
                    float progress = point / (float)(line.positionCount - 1);
                    Vector2 position = Vector2.Lerp(start, end, progress);
                    if (point > 0 && point < line.positionCount - 1)
                        position += Random.insideUnitCircle * 0.16f;
                    line.SetPosition(point, position);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (LineRenderer line in bolts)
            {
                if (line != null) line.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
