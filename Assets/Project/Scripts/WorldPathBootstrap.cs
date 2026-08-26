using System.Collections;
using System.Collections.Generic;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Project.Scripts.World
{
    [DefaultExecutionOrder(-600)]
    public sealed class WorldPathBootstrap : MonoBehaviour
    {
        private const int WorldXMin = -24;
        private const int WorldXMax = 24;
        private const int WorldYMin = -60;
        private const int WorldYMax = 60;

        [Header("Shared project assets")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private TileBase backgroundTile;
        [SerializeField] private TileBase pathTile;
        [SerializeField] private TileBase alternatePathTile;
        [SerializeField] private TileBase wallTile;
        [SerializeField] private TileBase[] decorationTiles;

        [Header("Destination")]
        [SerializeField] private string bossSceneName = "BossFight";

        private readonly HashSet<Vector3Int> walkable = new();
        private readonly List<Vector3Int> explorationRooms = new();
        private readonly List<Tile> runtimeTiles = new();
        private GameObject tutorialOverlay;
        private GameObject objectiveHud;
        private GameObject exitPrompt;
        private float tutorialUnlockTime;
        private bool tutorialOpen;

        private static readonly Vector3Int StartCell = new(0, -52, 0);
        private static readonly Vector3Int ExitCell = new(0, 54, 0);

        private void Awake()
        {
            Time.timeScale = 1f;
            BuildWorld();
            GameObject player = SpawnPlayer();
            BuildCamera(player != null ? player.transform : null);
            BuildInterface();
            BuildExitPortal();
            OpenTutorial();
        }

        private void Update()
        {
            if (!tutorialOpen || Time.unscaledTime < tutorialUnlockTime || !Input.anyKeyDown) return;
            tutorialOpen = false;
            Time.timeScale = 1f;
            if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
            if (objectiveHud != null) objectiveHud.SetActive(true);
        }

        private void OnDestroy()
        {
            if (tutorialOpen) Time.timeScale = 1f;
            foreach (Tile tile in runtimeTiles)
            {
                if (tile != null) Destroy(tile);
            }
        }

        private void BuildWorld()
        {
            if (backgroundTile == null || pathTile == null || wallTile == null)
            {
                Debug.LogError("WorldPath requires the shared cave background, path and wall tiles.", this);
                return;
            }

            GameObject gridObject = new("World Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Tilemap floor = CreateTilemap(gridObject.transform, "Cave Background", 0);
            Tilemap path = CreateTilemap(gridObject.transform, "Exploration Path", 2);
            Tilemap walls = CreateTilemap(gridObject.transform, "Cave Walls", 5);
            Tilemap details = CreateTilemap(gridObject.transform, "Path Decorations", 4);

            CarveMainRoute();
            CarveExplorationBranches();

            for (int x = WorldXMin; x <= WorldXMax; x++)
            {
                for (int y = WorldYMin; y <= WorldYMax; y++)
                {
                    Vector3Int cell = new(x, y, 0);
                    floor.SetTile(cell, backgroundTile);
                    if (walkable.Contains(cell))
                    {
                        int hash = Mathf.Abs(x * 73856093 ^ y * 19349663);
                        path.SetTile(cell, alternatePathTile != null && hash % 9 == 0
                            ? alternatePathTile
                            : pathTile);
                    }
                }
            }

            TileBase collisionWall = CreateCollisionWallTile(wallTile);
            for (int x = WorldXMin; x <= WorldXMax; x++)
            {
                for (int y = WorldYMin; y <= WorldYMax; y++)
                {
                    Vector3Int cell = new(x, y, 0);
                    if (walkable.Contains(cell)) continue;
                    if (TouchesWalkable(cell) || x == WorldXMin || x == WorldXMax ||
                        y == WorldYMin || y == WorldYMax)
                        walls.SetTile(cell, collisionWall);
                }
            }

            PaintDecorations(details);
            ConfigureWallCollision(walls);
            floor.CompressBounds();
            path.CompressBounds();
            walls.CompressBounds();
            details.CompressBounds();
        }

        private void CarveMainRoute()
        {
            Vector3Int[] route =
            {
                StartCell,
                new(0, -42, 0),
                new(-8, -30, 0),
                new(-8, -16, 0),
                new(6, 0, 0),
                new(6, 17, 0),
                new(-4, 31, 0),
                new(0, 45, 0),
                ExitCell
            };

            for (int index = 0; index < route.Length - 1; index++)
                CarveLine(route[index], route[index + 1], 5);

            CarveRoom(StartCell, 6);
            CarveRoom(ExitCell, 6);
        }

        private void CarveExplorationBranches()
        {
            AddBranch(new Vector3Int(-3, -37, 0), new Vector3Int(-18, -37, 0), 5);
            AddBranch(new Vector3Int(-8, -17, 0), new Vector3Int(18, -17, 0), 6);
            AddBranch(new Vector3Int(5, 4, 0), new Vector3Int(-18, 7, 0), 5);
            AddBranch(new Vector3Int(5, 20, 0), new Vector3Int(18, 28, 0), 5);
        }

        private void AddBranch(Vector3Int fork, Vector3Int end, int roomRadius)
        {
            CarveLine(fork, end, 5);
            CarveRoom(end, roomRadius);
            explorationRooms.Add(end);
        }

        private void CarveLine(Vector3Int from, Vector3Int to, int width)
        {
            int radius = Mathf.Max(1, width / 2);
            int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            steps = Mathf.Max(1, steps);
            for (int step = 0; step <= steps; step++)
            {
                float progress = step / (float)steps;
                Vector3Int center = new(
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, progress)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, progress)),
                    0);
                CarveRoom(center, radius);
            }
        }

        private void CarveRoom(Vector3Int center, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y > radius * radius + 2) continue;
                    Vector3Int cell = center + new Vector3Int(x, y, 0);
                    if (cell.x <= WorldXMin || cell.x >= WorldXMax ||
                        cell.y <= WorldYMin || cell.y >= WorldYMax) continue;
                    walkable.Add(cell);
                }
            }
        }

        private bool TouchesWalkable(Vector3Int cell)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    if (walkable.Contains(cell + new Vector3Int(x, y, 0))) return true;
                }
            }
            return false;
        }

        private void PaintDecorations(Tilemap details)
        {
            if (decorationTiles == null || decorationTiles.Length == 0) return;
            Vector3Int[] offsets =
            {
                new(-3, 3, 0), new(3, 3, 0), new(-3, -3, 0), new(3, -3, 0)
            };

            for (int roomIndex = 0; roomIndex < explorationRooms.Count; roomIndex++)
            {
                Vector3Int room = explorationRooms[roomIndex];
                for (int index = 0; index < offsets.Length; index++)
                {
                    Vector3Int cell = room + offsets[index];
                    if (!walkable.Contains(cell)) continue;
                    TileBase tile = decorationTiles[(roomIndex * 2 + index) % decorationTiles.Length];
                    if (tile != null) details.SetTile(cell, tile);
                }
            }
        }

        private static Tilemap CreateTilemap(Transform parent, string objectName, int sortingOrder)
        {
            GameObject tilemapObject = new(objectName);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private TileBase CreateCollisionWallTile(TileBase source)
        {
            if (source is not Tile sourceTile) return source;
            Tile collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.sprite = sourceTile.sprite;
            collisionTile.color = sourceTile.color;
            collisionTile.transform = sourceTile.transform;
            collisionTile.flags = TileFlags.LockAll;
            collisionTile.colliderType = Tile.ColliderType.Grid;
            runtimeTiles.Add(collisionTile);
            return collisionTile;
        }

        private static void ConfigureWallCollision(Tilemap walls)
        {
            Rigidbody2D body = walls.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = walls.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            TilemapCollider2D collider = walls.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        private GameObject SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("WorldPath requires the main Player prefab.", this);
                return null;
            }

            GameObject player = Instantiate(playerPrefab, (Vector3)StartCell + new Vector3(0.5f, 0.5f),
                Quaternion.identity);
            player.name = "Player";

            GameObject poolObject = new("World Projectile Pool");
            ObjectPool pool = poolObject.AddComponent<ObjectPool>();
            AttackPlayer attack = player.GetComponentInChildren<AttackPlayer>(true);
            if (attack != null) attack.ConfigureRuntimePool(pool);
            return player;
        }

        private static void BuildCamera(Transform target)
        {
            GameObject cameraObject = new("World Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.008f, 0.008f, 1f);
            camera.transform.position = new Vector3(StartCell.x + 0.5f, StartCell.y + 0.5f, -10f);
            cameraObject.AddComponent<AudioListener>();

            WorldPathCamera follow = cameraObject.AddComponent<WorldPathCamera>();
            follow.Configure(target, new Vector2(WorldXMin, WorldYMin),
                new Vector2(WorldXMax + 1f, WorldYMax + 1f));
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new("World Interface");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            objectiveHud = CreatePanel("Objective", canvasRect, new Vector2(0.25f, 0.91f),
                new Vector2(0.75f, 0.975f), new Color(0.09f, 0.025f, 0.018f, 0.92f));
            CreateText("Objective Text", objectiveHud.transform as RectTransform,
                Vector2.zero, Vector2.one,
                GameLoadout.IsSpanish
                    ? "OBJETIVO  ·  SIGUE EL SENDERO Y EXPLORA LOS DESVÍOS"
                    : "OBJECTIVE  ·  FOLLOW THE PATH AND EXPLORE ITS BRANCHES",
                25f, new Color(1f, 0.88f, 0.68f), TextAlignmentOptions.Center);
            objectiveHud.SetActive(false);

            exitPrompt = CreatePanel("Exit Prompt", canvasRect, new Vector2(0.34f, 0.075f),
                new Vector2(0.66f, 0.15f), new Color(0.08f, 0.018f, 0.014f, 0.95f));
            CreateText("Exit Prompt Text", exitPrompt.transform as RectTransform,
                Vector2.zero, Vector2.one,
                GameLoadout.IsSpanish ? "[ E ]  ENTRAR A LA GUARIDA" : "[ E ]  ENTER THE LAIR",
                27f, new Color(1f, 0.78f, 0.42f), TextAlignmentOptions.Center);
            exitPrompt.SetActive(false);

            tutorialOverlay = CreatePanel("Tutorial Overlay", canvasRect, Vector2.zero, Vector2.one,
                new Color(0.02f, 0.006f, 0.006f, 0.88f));
            GameObject card = CreatePanel("Tutorial Card", tutorialOverlay.transform as RectTransform,
                new Vector2(0.265f, 0.17f), new Vector2(0.735f, 0.83f),
                new Color(0.12f, 0.035f, 0.023f, 0.98f));

            string title = GameLoadout.IsSpanish ? "EL CAMINO A SPIKE" : "THE ROAD TO SPIKE";
            string subtitle = GameLoadout.IsSpanish
                ? "Antes de entrar en la guarida, aprende a controlar a tu cazador."
                : "Learn to control your hunter before entering the lair.";
            string controls = GameLoadout.IsSpanish
                ? "WASD / FLECHAS     MOVERSE\nRATÓN                 APUNTAR\nCLIC IZQUIERDO        DISPARAR\nSHIFT                  DASH · 3 CARGAS\nR                      RECARGAR\nE                      INTERACTUAR"
                : "WASD / ARROWS      MOVE\nMOUSE                  AIM\nLEFT CLICK             SHOOT\nSHIFT                  DASH · 3 CHARGES\nR                      RELOAD\nE                      INTERACT";
            string continueText = GameLoadout.IsSpanish
                ? "PULSA CUALQUIER TECLA PARA COMENZAR"
                : "PRESS ANY KEY TO BEGIN";

            RectTransform cardRect = card.transform as RectTransform;
            CreateText("Tutorial Title", cardRect, new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.94f),
                title, 47f, new Color(1f, 0.88f, 0.68f), TextAlignmentOptions.Center);
            CreateText("Tutorial Subtitle", cardRect, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.8f),
                subtitle, 23f, new Color(0.83f, 0.63f, 0.48f), TextAlignmentOptions.Center);
            CreateText("Tutorial Controls", cardRect, new Vector2(0.13f, 0.22f), new Vector2(0.87f, 0.65f),
                controls, 26f, new Color(0.96f, 0.86f, 0.72f), TextAlignmentOptions.Left);
            CreateText("Tutorial Continue", cardRect, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.17f),
                continueText, 22f, new Color(1f, 0.53f, 0.24f), TextAlignmentOptions.Center);
        }

        private void OpenTutorial()
        {
            tutorialOpen = true;
            tutorialUnlockTime = Time.unscaledTime + 0.45f;
            Time.timeScale = 0f;
            if (tutorialOverlay != null) tutorialOverlay.SetActive(true);
        }

        private void BuildExitPortal()
        {
            GameObject portal = new("Entrance To Spike's Lair");
            portal.transform.position = ExitCell + new Vector3(0.5f, 1.2f, 0f);

            Sprite markerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            SpriteRenderer marker = portal.AddComponent<SpriteRenderer>();
            marker.sprite = markerSprite;
            marker.color = new Color(0.23f, 0.035f, 0.025f, 0.96f);
            marker.sortingOrder = 3;
            portal.transform.localScale = new Vector3(4.5f, 3.6f, 1f);

            BoxCollider2D trigger = portal.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.4f, 1.2f);

            WorldExitPortal exit = portal.AddComponent<WorldExitPortal>();
            exit.Configure(bossSceneName, exitPrompt);
        }

        private static GameObject CreatePanel(
            string objectName,
            RectTransform parent,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Color color)
        {
            GameObject panel = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMinimum;
            rect.anchorMax = anchorMaximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            RectTransform parent,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMinimum;
            rect.anchorMax = anchorMaximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }

    public sealed class WorldPathCamera : MonoBehaviour
    {
        private Transform target;
        private Vector2 minimum;
        private Vector2 maximum;
        private Camera worldCamera;

        public void Configure(Transform followTarget, Vector2 worldMinimum, Vector2 worldMaximum)
        {
            target = followTarget;
            minimum = worldMinimum;
            maximum = worldMaximum;
            worldCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null || worldCamera == null) return;
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(0.1f, worldCamera.aspect);
            Vector3 desired = target.position;
            desired.x = Mathf.Clamp(desired.x, minimum.x + halfWidth, maximum.x - halfWidth);
            desired.y = Mathf.Clamp(desired.y, minimum.y + halfHeight, maximum.y - halfHeight);
            desired.z = transform.position.z;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }
    }

    public sealed class WorldExitPortal : MonoBehaviour
    {
        private string destination;
        private GameObject prompt;
        private bool playerInside;
        private bool loading;

        public void Configure(string sceneName, GameObject promptObject)
        {
            destination = string.IsNullOrEmpty(sceneName) ? "BossFight" : sceneName;
            prompt = promptObject;
        }

        private void Update()
        {
            if (!playerInside || loading || !Input.GetKeyDown(KeyCode.E)) return;
            loading = true;
            if (prompt != null) prompt.SetActive(false);
            StartCoroutine(LoadDestination());
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            playerInside = true;
            if (prompt != null) prompt.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            playerInside = false;
            if (prompt != null) prompt.SetActive(false);
        }

        private IEnumerator LoadDestination()
        {
            Time.timeScale = 1f;
            AsyncOperation operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            if (operation == null) yield break;
            while (!operation.isDone) yield return null;
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other == null) return false;
            if (other.CompareTag("Player")) return true;
            Transform root = other.transform.root;
            return root != null && root.CompareTag("Player");
        }
    }
}
