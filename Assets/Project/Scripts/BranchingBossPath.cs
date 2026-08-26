using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Combat;
using Project.Characters.Enemy.EnemyScripts.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Project.Scripts.Arena
{
    /// <summary>
    /// Builds a deterministic approach to Spike using tiles that are already painted in
    /// the fight scene. The source Tile Palette remains the visual authority: no external
    /// sprites or replacement materials are introduced here.
    /// </summary>
    public sealed class BranchingBossPath : MonoBehaviour
    {
        private const string RuntimeRootName = "Boss Approach Path";

        private Tilemap background;
        private Tilemap pathTilemap;
        private Tilemap decorationTilemap;
        private BoundsInt safeCells;
        private readonly List<Vector3Int> branchEnds = new();

        public IReadOnlyList<Vector3Int> BranchEnds => branchEnds;

        public static void BuildFrom(Tilemap arenaBackground)
        {
            if (arenaBackground == null || arenaBackground.transform.parent == null) return;
            Transform grid = arenaBackground.transform.parent;
            Transform existing = grid.Find(RuntimeRootName);
            if (existing != null) return;

            GameObject root = new(RuntimeRootName);
            root.layer = arenaBackground.gameObject.layer;
            root.transform.SetParent(grid, false);

            BranchingBossPath builder = root.AddComponent<BranchingBossPath>();
            builder.background = arenaBackground;
            builder.Build();
        }

        private void Build()
        {
            Tilemap mud = FindSiblingTilemap("Lodo");
            Tilemap mudDecorations = FindSiblingTilemap("LodoDecorations");
            Tilemap grassDecorations = FindSiblingTilemap("GrassDecorations");
            TileBase pathTile = MostUsedTile(mud) ?? MostUsedTile(background);
            TileBase alternatePathTile = SecondMostUsedTile(mud) ?? pathTile;
            List<TileBase> decorations = CollectDistinctTiles(mudDecorations, grassDecorations);
            if (pathTile == null) return;

            pathTilemap = CreateTilemap("Path Floor", mud, 2);
            decorationTilemap = CreateTilemap("Path Details", grassDecorations, 4);

            BoundsInt bounds = background.cellBounds;
            safeCells = new BoundsInt(
                bounds.xMin + 2,
                bounds.yMin + 2,
                0,
                Mathf.Max(1, bounds.size.x - 4),
                Mathf.Max(1, bounds.size.y - 4),
                1);

            Vector3Int start = ActorCell("Player", new Vector2(0f, -7f));
            Vector3Int boss = ActorCell("Enemy", new Vector2(0f, 8f));
            start = ClampCell(start);
            boss = ClampCell(boss);

            PaintRoute(start, boss, pathTile, alternatePathTile);
            PaintBranches(start, boss, pathTile, alternatePathTile, decorations);
            CreateBossGate(start, boss);

            pathTilemap.CompressBounds();
            decorationTilemap.CompressBounds();
        }

        private void PaintRoute(Vector3Int start, Vector3Int boss, TileBase primary, TileBase alternate)
        {
            int middleY = Mathf.RoundToInt(Mathf.Lerp(start.y, boss.y, 0.55f));
            PaintThickLine(start, new Vector3Int(start.x, middleY, 0), 3, primary, alternate);
            PaintThickLine(new Vector3Int(start.x, middleY, 0), new Vector3Int(boss.x, middleY, 0), 3,
                primary, alternate);
            PaintThickLine(new Vector3Int(boss.x, middleY, 0), boss, 3, primary, alternate);
            PaintRoom(start, 2, primary, alternate);
            PaintRoom(boss, 3, primary, alternate);
        }

        private void PaintBranches(
            Vector3Int start,
            Vector3Int boss,
            TileBase primary,
            TileBase alternate,
            IReadOnlyList<TileBase> decorations)
        {
            int direction = boss.y >= start.y ? 1 : -1;
            int distance = Mathf.Max(6, Mathf.Abs(boss.y - start.y));
            int leftEdge = safeCells.xMin + 3;
            int rightEdge = safeCells.xMax - 4;
            int centerX = Mathf.RoundToInt((start.x + boss.x) * 0.5f);

            Vector3Int[] forks =
            {
                new(centerX, start.y + direction * Mathf.RoundToInt(distance * 0.28f), 0),
                new(centerX, start.y + direction * Mathf.RoundToInt(distance * 0.52f), 0),
                new(centerX, start.y + direction * Mathf.RoundToInt(distance * 0.74f), 0)
            };
            int[] targets = { leftEdge, rightEdge, safeCells.xMin + Mathf.Max(5, safeCells.size.x / 4) };

            for (int index = 0; index < forks.Length; index++)
            {
                Vector3Int fork = ClampCell(forks[index]);
                Vector3Int end = ClampCell(new Vector3Int(targets[index], fork.y, 0));
                branchEnds.Add(end);
                PaintThickLine(fork, end, 3, primary, alternate);
                PaintRoom(end, index == 1 ? 3 : 2, primary, alternate);
                PaintBranchDetails(end, index, decorations);
            }
        }

        private void PaintThickLine(
            Vector3Int from,
            Vector3Int to,
            int width,
            TileBase primary,
            TileBase alternate)
        {
            int radius = Mathf.Max(0, width / 2);
            bool horizontal = Mathf.Abs(to.x - from.x) >= Mathf.Abs(to.y - from.y);
            int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            steps = Mathf.Max(1, steps);

            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                Vector3Int center = new(
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)),
                    0);

                for (int offset = -radius; offset <= radius; offset++)
                {
                    Vector3Int cell = horizontal
                        ? center + new Vector3Int(0, offset, 0)
                        : center + new Vector3Int(offset, 0, 0);
                    SetPathTile(cell, primary, alternate);
                }
            }
        }

        private void PaintRoom(Vector3Int center, int radius, TileBase primary, TileBase alternate)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y > radius * radius + 1) continue;
                    SetPathTile(center + new Vector3Int(x, y, 0), primary, alternate);
                }
            }
        }

        private void SetPathTile(Vector3Int cell, TileBase primary, TileBase alternate)
        {
            if (!safeCells.Contains(cell)) return;
            int hash = Mathf.Abs(cell.x * 73856093 ^ cell.y * 19349663);
            pathTilemap.SetTile(cell, hash % 7 == 0 ? alternate : primary);
        }

        private void PaintBranchDetails(Vector3Int center, int branchIndex, IReadOnlyList<TileBase> decorations)
        {
            if (decorations == null || decorations.Count == 0) return;
            Vector3Int[] offsets =
            {
                new(-2, 2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, -2, 0)
            };

            for (int index = 0; index < offsets.Length; index++)
            {
                Vector3Int cell = center + offsets[index];
                if (!safeCells.Contains(cell)) continue;
                int tileIndex = (branchIndex * 3 + index) % decorations.Count;
                decorationTilemap.SetTile(cell, decorations[tileIndex]);
            }
        }

        private void CreateBossGate(Vector3Int start, Vector3Int boss)
        {
            GameObject enemyObject = GameObject.FindGameObjectWithTag("Enemy");
            if (enemyObject == null) return;

            EnemyAttackController attackController = enemyObject.GetComponent<EnemyAttackController>();
            if (attackController == null) attackController = enemyObject.GetComponentInChildren<EnemyAttackController>();
            Health bossHealth = enemyObject.GetComponent<Health>();
            if (bossHealth == null) bossHealth = enemyObject.GetComponentInChildren<Health>();
            if (attackController == null && bossHealth == null) return;

            int direction = boss.y >= start.y ? 1 : -1;
            Vector3Int gateCell = ClampCell(new Vector3Int(boss.x, boss.y - direction * 4, 0));
            GameObject gateObject = new("Spike Arena Entrance");
            gateObject.transform.SetParent(transform, false);
            gateObject.transform.position = background.GetCellCenterWorld(gateCell);
            BoxCollider2D collider = gateObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(7f, 1.4f);

            BossApproachTrigger trigger = gateObject.AddComponent<BossApproachTrigger>();
            trigger.Configure(attackController, bossHealth);
        }

        private Vector3Int ActorCell(string tag, Vector2 fallbackWorldPosition)
        {
            GameObject actor = GameObject.FindGameObjectWithTag(tag);
            Vector3 world = actor != null ? actor.transform.position : fallbackWorldPosition;
            return background.WorldToCell(world);
        }

        private Vector3Int ClampCell(Vector3Int cell)
        {
            return new Vector3Int(
                Mathf.Clamp(cell.x, safeCells.xMin, safeCells.xMax - 1),
                Mathf.Clamp(cell.y, safeCells.yMin, safeCells.yMax - 1),
                0);
        }

        private Tilemap FindSiblingTilemap(string objectName)
        {
            Transform sibling = background.transform.parent.Find(objectName);
            return sibling != null ? sibling.GetComponent<Tilemap>() : null;
        }

        private Tilemap CreateTilemap(string objectName, Tilemap visualReference, int fallbackSortingOrder)
        {
            GameObject tilemapObject = new(objectName);
            tilemapObject.layer = background.gameObject.layer;
            tilemapObject.transform.SetParent(transform.parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();

            TilemapRenderer referenceRenderer = visualReference != null
                ? visualReference.GetComponent<TilemapRenderer>()
                : null;
            if (referenceRenderer != null)
            {
                renderer.sortingLayerID = referenceRenderer.sortingLayerID;
                renderer.sortingOrder = referenceRenderer.sortingOrder + 1;
                renderer.sharedMaterial = referenceRenderer.sharedMaterial;
            }
            else
            {
                renderer.sortingOrder = fallbackSortingOrder;
            }

            return tilemap;
        }

        private static TileBase MostUsedTile(Tilemap source)
        {
            List<KeyValuePair<TileBase, int>> usage = CountTileUsage(source);
            return usage.Count > 0 ? usage[0].Key : null;
        }

        private static TileBase SecondMostUsedTile(Tilemap source)
        {
            List<KeyValuePair<TileBase, int>> usage = CountTileUsage(source);
            return usage.Count > 1 ? usage[1].Key : usage.Count > 0 ? usage[0].Key : null;
        }

        private static List<KeyValuePair<TileBase, int>> CountTileUsage(Tilemap source)
        {
            Dictionary<TileBase, int> counts = new();
            if (source != null)
            {
                foreach (Vector3Int position in source.cellBounds.allPositionsWithin)
                {
                    TileBase tile = source.GetTile(position);
                    if (tile == null) continue;
                    counts.TryGetValue(tile, out int count);
                    counts[tile] = count + 1;
                }
            }

            List<KeyValuePair<TileBase, int>> ordered = new(counts);
            ordered.Sort((left, right) => right.Value.CompareTo(left.Value));
            return ordered;
        }

        private static List<TileBase> CollectDistinctTiles(params Tilemap[] sources)
        {
            List<TileBase> result = new();
            HashSet<TileBase> seen = new();
            foreach (Tilemap source in sources)
            {
                if (source == null) continue;
                foreach (Vector3Int position in source.cellBounds.allPositionsWithin)
                {
                    TileBase tile = source.GetTile(position);
                    if (tile != null && seen.Add(tile)) result.Add(tile);
                }
            }

            return result;
        }
    }

    public sealed class BossApproachTrigger : MonoBehaviour
    {
        private EnemyAttackController attackController;
        private Health bossHealth;
        private bool activated;

        public void Configure(EnemyAttackController controller, Health health)
        {
            attackController = controller;
            bossHealth = health;
            if (bossHealth != null) bossHealth.SetExternalInvulnerable(true);
            if (attackController != null) attackController.enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (activated || other == null) return;
            Transform root = other.transform.root;
            if (!other.CompareTag("Player") && (root == null || !root.CompareTag("Player"))) return;

            activated = true;
            if (bossHealth != null) bossHealth.SetExternalInvulnerable(false);
            if (attackController != null) attackController.enabled = true;
            Destroy(gameObject);
        }
    }
}
