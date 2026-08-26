using System.Collections;
using System.Collections.Generic;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Project.Scripts.World
{
    [DefaultExecutionOrder(-600)]
    public sealed class WorldPathBootstrap : MonoBehaviour
    {
        private const int RoomHalfWidth = 18;
        private const int RoomHalfHeight = 12;
        private const int MinimumRoomX = -1;
        private const int MaximumRoomX = 1;
        private const int MinimumRoomY = 0;
        private const int MaximumRoomY = 3;
        private const float DoorInset = 1.35f;

        [Header("Shared project assets")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private TileBase backgroundTile;
        [SerializeField] private TileBase pathTile;
        [SerializeField] private TileBase alternatePathTile;
        [SerializeField] private TileBase wallTile;
        [SerializeField] private TileBase startAreaTile;
        [SerializeField] private TileBase startAreaAccentTile;
        [SerializeField] private TileBase[] decorationTiles;

        [Header("Reserved destination")]
        [SerializeField] private string bossSceneName = "BossFight";

        private readonly List<Tile> runtimeTiles = new();
        private readonly List<GameObject> roomObjects = new();
        private readonly HashSet<Vector2Int> visitedRooms = new();
        private readonly Dictionary<Vector2Int, Image> mapCells = new();
        private readonly Dictionary<Vector2Int, TextMeshProUGUI> mapCellLabels = new();
        private readonly List<MapConnection> mapConnections = new();

        private Tilemap background;
        private Tilemap floor;
        private Tilemap walls;
        private Tilemap details;
        private TileBase collisionWall;
        private Rigidbody2D playerBody;
        private WorldPathCamera worldCamera;
        private CanvasGroup transitionFade;
        private GameObject tutorialOverlay;
        private GameObject objectiveHud;
        private TextMeshProUGUI roomLabel;
        private RectTransform mapPanel;
        private float tutorialUnlockTime;
        private bool tutorialOpen;
        private bool transitioning;
        private bool mapExpanded;
        private Vector2Int currentRoom;

        private static readonly Vector2Int StartRoom = Vector2Int.zero;

        private void Awake()
        {
            Time.timeScale = 1f;
            BuildReusableRoom();
            GameObject player = SpawnPlayer();
            playerBody = ResolvePlayerBody(player);
            worldCamera = BuildCamera(playerBody != null
                ? playerBody.transform
                : player != null ? player.transform : null);
            BuildWorldLighting();
            BuildInterface();
            BuildTransitionFade();
            LoadRoom(StartRoom, RoomDirection.None);
            OpenTutorial();
            StartCoroutine(FadeFromBlack());
        }

        private void Update()
        {
            if (tutorialOpen)
            {
                if (Time.unscaledTime < tutorialUnlockTime || !Input.anyKeyDown) return;
                tutorialOpen = false;
                Time.timeScale = 1f;
                if (tutorialOverlay != null) tutorialOverlay.SetActive(false);
                if (objectiveHud != null) objectiveHud.SetActive(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.M)) ToggleMapSize();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            foreach (Tile tile in runtimeTiles)
            {
                if (tile != null) Destroy(tile);
            }
        }

        internal void RequestRoomChange(RoomDirection direction)
        {
            if (transitioning || tutorialOpen || direction == RoomDirection.None) return;
            Vector2Int destination = currentRoom + DirectionOffset(direction);
            if (!IsValidRoom(destination)) return;
            StartCoroutine(ChangeRoom(destination, direction));
        }

        private IEnumerator ChangeRoom(Vector2Int destination, RoomDirection travelDirection)
        {
            transitioning = true;
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.simulated = false;
            }

            yield return FadeTo(1f, 0.22f);
            LoadRoom(destination, travelDirection);
            yield return new WaitForSecondsRealtime(0.08f);

            if (playerBody != null) playerBody.simulated = true;
            yield return FadeTo(0f, 0.3f);
            transitioning = false;
        }

        private void BuildReusableRoom()
        {
            if (backgroundTile == null || pathTile == null || wallTile == null)
            {
                Debug.LogError("WorldPath requires background, floor and wall tiles.", this);
                return;
            }

            GameObject gridObject = new("Room Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            background = CreateTilemap(gridObject.transform, "Room Background", 0);
            floor = CreateTilemap(gridObject.transform, "Room Floor", 1);
            details = CreateTilemap(gridObject.transform, "Room Details", 3);
            walls = CreateTilemap(gridObject.transform, "Room Walls", 5);
            collisionWall = CreateCollisionWallTile(wallTile);
            ConfigureWallCollision(walls);
        }

        private void LoadRoom(Vector2Int room, RoomDirection enteredThrough)
        {
            currentRoom = room;
            visitedRooms.Add(room);
            ClearRoom();
            PaintRoom(room);
            BuildDoors(room);

            Vector2 roomMinimum = new(-RoomHalfWidth, -RoomHalfHeight);
            Vector2 roomMaximum = new(RoomHalfWidth + 1f, RoomHalfHeight + 1f);
            if (worldCamera != null)
            {
                worldCamera.SetBounds(roomMinimum, roomMaximum);
            }

            Vector2 entry = GetEntryPosition(enteredThrough);
            if (playerBody != null)
            {
                playerBody.position = entry;
                playerBody.linearVelocity = Vector2.zero;
            }

            if (worldCamera != null) worldCamera.SnapToTarget();
            UpdateRoomHud();
        }

        private void ClearRoom()
        {
            if (background != null) background.ClearAllTiles();
            if (floor != null) floor.ClearAllTiles();
            if (walls != null) walls.ClearAllTiles();
            if (details != null) details.ClearAllTiles();

            foreach (GameObject roomObject in roomObjects)
            {
                if (roomObject != null) Destroy(roomObject);
            }
            roomObjects.Clear();
        }

        private void PaintRoom(Vector2Int room)
        {
            if (background == null || floor == null || walls == null) return;

            for (int x = -RoomHalfWidth - 1; x <= RoomHalfWidth + 1; x++)
            {
                for (int y = -RoomHalfHeight - 1; y <= RoomHalfHeight + 1; y++)
                {
                    Vector3Int cell = new(x, y, 0);
                    background.SetTile(cell, backgroundTile);

                    if (x > -RoomHalfWidth && x < RoomHalfWidth &&
                        y > -RoomHalfHeight && y < RoomHalfHeight)
                    {
                        int hash = StableHash(room, x, y);
                        TileBase roomFloor = startAreaTile != null ? startAreaTile : pathTile;
                        if (startAreaAccentTile != null && hash % 11 == 0)
                            roomFloor = startAreaAccentTile;
                        else if (alternatePathTile != null && hash % 17 == 0)
                            roomFloor = alternatePathTile;
                        floor.SetTile(cell, roomFloor);
                    }
                }
            }

            RoomOpenings openings = GetOpenings(room);
            PaintHorizontalWall(RoomHalfHeight, openings.Up);
            PaintHorizontalWall(-RoomHalfHeight, openings.Down);
            PaintVerticalWall(-RoomHalfWidth, openings.Left);
            PaintVerticalWall(RoomHalfWidth, openings.Right);
            PaintRoomFeatures(room);
            PaintDecorations(room);

            background.CompressBounds();
            floor.CompressBounds();
            walls.CompressBounds();
            details.CompressBounds();
        }

        private void PaintHorizontalWall(int y, bool hasDoor)
        {
            for (int x = -RoomHalfWidth; x <= RoomHalfWidth; x++)
            {
                if (hasDoor && Mathf.Abs(x) <= 1) continue;
                walls.SetTile(new Vector3Int(x, y, 0), collisionWall);
            }
        }

        private void PaintVerticalWall(int x, bool hasDoor)
        {
            for (int y = -RoomHalfHeight; y <= RoomHalfHeight; y++)
            {
                if (hasDoor && Mathf.Abs(y) <= 1) continue;
                walls.SetTile(new Vector3Int(x, y, 0), collisionWall);
            }
        }

        private void PaintRoomFeatures(Vector2Int room)
        {
            int pattern = Mathf.Abs(room.x * 31 + room.y * 17) % 4;
            switch (pattern)
            {
                case 0:
                    PaintPillarCluster(new Vector3Int(-7, 4, 0));
                    PaintPillarCluster(new Vector3Int(7, -4, 0));
                    break;
                case 1:
                    PaintShortWall(new Vector3Int(-9, 4, 0), true, 6);
                    PaintShortWall(new Vector3Int(4, -5, 0), true, 6);
                    break;
                case 2:
                    PaintShortWall(new Vector3Int(-7, -6, 0), false, 5);
                    PaintShortWall(new Vector3Int(7, 2, 0), false, 5);
                    break;
                default:
                    PaintPillarCluster(new Vector3Int(-8, -5, 0));
                    PaintPillarCluster(new Vector3Int(8, 5, 0));
                    PaintPillarCluster(new Vector3Int(0, 0, 0));
                    break;
            }
        }

        private void PaintPillarCluster(Vector3Int center)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) > 1) continue;
                    walls.SetTile(center + new Vector3Int(x, y, 0), collisionWall);
                }
            }
        }

        private void PaintShortWall(Vector3Int start, bool horizontal, int length)
        {
            for (int index = 0; index < length; index++)
            {
                Vector3Int offset = horizontal
                    ? new Vector3Int(index, 0, 0)
                    : new Vector3Int(0, index, 0);
                walls.SetTile(start + offset, collisionWall);
            }
        }

        private void PaintDecorations(Vector2Int room)
        {
            if (details == null || decorationTiles == null || decorationTiles.Length == 0) return;
            Vector3Int[] positions =
            {
                new(-13, 8, 0), new(13, 8, 0), new(-13, -8, 0), new(13, -8, 0),
                new(-5, 8, 0), new(5, -8, 0)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                TileBase decoration = decorationTiles[Mathf.Abs(room.x * 5 + room.y * 3 + index) %
                                                     decorationTiles.Length];
                if (decoration != null) details.SetTile(positions[index], decoration);
            }
        }

        private void BuildDoors(Vector2Int room)
        {
            RoomOpenings openings = GetOpenings(room);
            if (openings.Up)
                CreateDoor("North Door", RoomDirection.Up, new Vector2(0f, RoomHalfHeight - 0.15f),
                    new Vector2(3.4f, 1.1f));
            if (openings.Down)
                CreateDoor("South Door", RoomDirection.Down, new Vector2(0f, -RoomHalfHeight + 0.15f),
                    new Vector2(3.4f, 1.1f));
            if (openings.Left)
                CreateDoor("West Door", RoomDirection.Left, new Vector2(-RoomHalfWidth + 0.15f, 0f),
                    new Vector2(1.1f, 3.4f));
            if (openings.Right)
                CreateDoor("East Door", RoomDirection.Right, new Vector2(RoomHalfWidth - 0.15f, 0f),
                    new Vector2(1.1f, 3.4f));
        }

        private void CreateDoor(string objectName, RoomDirection direction, Vector2 position, Vector2 size)
        {
            GameObject door = new(objectName);
            door.transform.position = position;
            roomObjects.Add(door);

            SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = new Color(0.72f, 0.29f, 0.1f, 1f);
            renderer.sortingOrder = 6;
            door.transform.localScale = new Vector3(size.x, size.y, 1f);

            GameObject glow = new("Door Glow");
            glow.transform.SetParent(door.transform, false);
            SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = RuntimeWhiteSprite.Instance;
            glowRenderer.color = new Color(1f, 0.68f, 0.26f, 0.38f);
            glowRenderer.sortingOrder = 5;
            glow.transform.localScale = direction is RoomDirection.Up or RoomDirection.Down
                ? new Vector3(1.22f, 2.1f, 1f)
                : new Vector3(2.1f, 1.22f, 1f);

            BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = Vector2.one;

            WorldRoomDoor roomDoor = door.AddComponent<WorldRoomDoor>();
            roomDoor.Configure(this, direction);
        }

        private RoomOpenings GetOpenings(Vector2Int room)
        {
            return new RoomOpenings(
                room.y < MaximumRoomY,
                room.y > MinimumRoomY,
                room.x > MinimumRoomX,
                room.x < MaximumRoomX);
        }

        private static bool IsValidRoom(Vector2Int room)
        {
            return room.x >= MinimumRoomX && room.x <= MaximumRoomX &&
                   room.y >= MinimumRoomY && room.y <= MaximumRoomY;
        }

        private static Vector2Int DirectionOffset(RoomDirection direction)
        {
            return direction switch
            {
                RoomDirection.Up => Vector2Int.up,
                RoomDirection.Down => Vector2Int.down,
                RoomDirection.Left => Vector2Int.left,
                RoomDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };
        }

        private static Vector2 GetEntryPosition(RoomDirection travelDirection)
        {
            return travelDirection switch
            {
                RoomDirection.Up => new Vector2(0f, -RoomHalfHeight + DoorInset + 1f),
                RoomDirection.Down => new Vector2(0f, RoomHalfHeight - DoorInset - 1f),
                RoomDirection.Left => new Vector2(RoomHalfWidth - DoorInset - 1f, 0f),
                RoomDirection.Right => new Vector2(-RoomHalfWidth + DoorInset + 1f, 0f),
                _ => new Vector2(0f, -5f)
            };
        }

        private static int StableHash(Vector2Int room, int x, int y)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + room.x;
                hash = hash * 31 + room.y;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                return hash == int.MinValue ? 0 : Mathf.Abs(hash);
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

        private static void ConfigureWallCollision(Tilemap wallMap)
        {
            Rigidbody2D body = wallMap.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = wallMap.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            TilemapCollider2D collider = wallMap.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        private GameObject SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("WorldPath requires the Player prefab.", this);
                return null;
            }

            GameObject player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player.name = "Player";

            GameObject poolObject = new("World Projectile Pool");
            ObjectPool pool = poolObject.AddComponent<ObjectPool>();
            AttackPlayer attack = player.GetComponentInChildren<AttackPlayer>(true);
            if (attack != null) attack.ConfigureRuntimePool(pool);
            return player;
        }

        private static Rigidbody2D ResolvePlayerBody(GameObject player)
        {
            return player != null ? player.GetComponentInChildren<Rigidbody2D>(true) : null;
        }

        private static WorldPathCamera BuildCamera(Transform target)
        {
            GameObject cameraObject = new("World Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.24f, 0.12f, 0.055f, 1f);
            camera.transform.position = new Vector3(0f, -5f, -10f);
            cameraObject.AddComponent<AudioListener>();

            WorldPathCamera follow = cameraObject.AddComponent<WorldPathCamera>();
            follow.Configure(target, new Vector2(-RoomHalfWidth, -RoomHalfHeight),
                new Vector2(RoomHalfWidth + 1f, RoomHalfHeight + 1f));
            return follow;
        }

        private static void BuildWorldLighting()
        {
            GameObject lightObject = new("World Global Light 2D");
            Light2D globalLight = lightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.color = new Color(1f, 0.94f, 0.86f, 1f);
            globalLight.intensity = 1.15f;
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
            objectiveHud = CreatePanel("Room HUD", canvasRect, new Vector2(0.28f, 0.91f),
                new Vector2(0.72f, 0.975f), new Color(0.09f, 0.025f, 0.018f, 0.9f));
            roomLabel = CreateText("Room Text", objectiveHud.transform as RectTransform,
                Vector2.zero, Vector2.one, string.Empty, 24f,
                new Color(1f, 0.88f, 0.68f), TextAlignmentOptions.Center);
            objectiveHud.SetActive(false);
            BuildMap(canvasRect);

            tutorialOverlay = CreatePanel("Tutorial Overlay", canvasRect, Vector2.zero, Vector2.one,
                new Color(0.1f, 0.035f, 0.018f, 0.08f));
            GameObject card = CreatePanel("Tutorial Card", tutorialOverlay.transform as RectTransform,
                new Vector2(0.27f, 0.18f), new Vector2(0.73f, 0.82f),
                new Color(0.19f, 0.065f, 0.032f, 0.96f));

            bool spanish = GameLoadout.IsSpanish;
            string title = spanish ? "SALAS DE LA CAVERNA" : "CAVERN ROOMS";
            string subtitle = spanish
                ? "Explora libremente. Cada puerta conduce a una sala distinta."
                : "Explore freely. Every door leads to a different room.";
            string controls = spanish
                ? "WASD / FLECHAS     MOVERSE\nRATÓN                 APUNTAR\nCLIC IZQUIERDO        DISPARAR\nSHIFT                  DASH · 3 CARGAS\nR                      RECARGAR\nPUERTAS                CAMBIAR DE SALA"
                : "WASD / ARROWS      MOVE\nMOUSE                  AIM\nLEFT CLICK             SHOOT\nSHIFT                  DASH · 3 CHARGES\nR                      RELOAD\nDOORS                   CHANGE ROOM";
            string continueText = spanish
                ? "PULSA CUALQUIER TECLA PARA EXPLORAR"
                : "PRESS ANY KEY TO EXPLORE";

            RectTransform cardRect = card.transform as RectTransform;
            CreateText("Tutorial Title", cardRect, new Vector2(0.06f, 0.8f), new Vector2(0.94f, 0.94f),
                title, 44f, new Color(1f, 0.88f, 0.68f), TextAlignmentOptions.Center);
            CreateText("Tutorial Subtitle", cardRect, new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.8f),
                subtitle, 23f, new Color(0.87f, 0.67f, 0.5f), TextAlignmentOptions.Center);
            CreateText("Tutorial Controls", cardRect, new Vector2(0.13f, 0.21f), new Vector2(0.87f, 0.63f),
                controls, 25f, new Color(0.96f, 0.86f, 0.72f), TextAlignmentOptions.Left);
            CreateText("Tutorial Continue", cardRect, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.17f),
                continueText, 22f, new Color(1f, 0.53f, 0.24f), TextAlignmentOptions.Center);
        }

        private void BuildMap(RectTransform canvasRect)
        {
            GameObject panel = CreatePanel("Exploration Map", canvasRect,
                new Vector2(0.79f, 0.59f), new Vector2(0.975f, 0.95f),
                new Color(0.055f, 0.018f, 0.012f, 0.94f));
            mapPanel = panel.transform as RectTransform;

            CreateText("Map Title", mapPanel, new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.98f),
                GameLoadout.IsSpanish ? "MAPA  ·  M" : "MAP  ·  M", 22f,
                new Color(1f, 0.78f, 0.48f), TextAlignmentOptions.Center);

            for (int y = MinimumRoomY; y <= MaximumRoomY; y++)
            {
                for (int x = MinimumRoomX; x <= MaximumRoomX; x++)
                {
                    Vector2Int room = new(x, y);
                    if (x < MaximumRoomX)
                        CreateMapConnection(room, new Vector2Int(x + 1, y));
                    if (y < MaximumRoomY)
                        CreateMapConnection(room, new Vector2Int(x, y + 1));
                }
            }

            for (int y = MinimumRoomY; y <= MaximumRoomY; y++)
            {
                for (int x = MinimumRoomX; x <= MaximumRoomX; x++)
                {
                    CreateMapCell(new Vector2Int(x, y));
                }
            }
        }

        private void CreateMapConnection(Vector2Int first, Vector2Int second)
        {
            Vector2 a = GetMapPosition(first);
            Vector2 b = GetMapPosition(second);
            GameObject lineObject = new($"Connection {first} - {second}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform line = lineObject.GetComponent<RectTransform>();
            line.SetParent(mapPanel, false);

            if (first.y == second.y)
            {
                line.anchorMin = new Vector2(Mathf.Min(a.x, b.x) + 0.055f, a.y - 0.012f);
                line.anchorMax = new Vector2(Mathf.Max(a.x, b.x) - 0.055f, a.y + 0.012f);
            }
            else
            {
                line.anchorMin = new Vector2(a.x - 0.012f, Mathf.Min(a.y, b.y) + 0.055f);
                line.anchorMax = new Vector2(a.x + 0.012f, Mathf.Max(a.y, b.y) - 0.055f);
            }

            line.offsetMin = Vector2.zero;
            line.offsetMax = Vector2.zero;
            Image image = lineObject.GetComponent<Image>();
            image.color = new Color(0.75f, 0.3f, 0.1f, 0.82f);
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            mapConnections.Add(new MapConnection(image, first, second));
        }

        private void CreateMapCell(Vector2Int room)
        {
            Vector2 center = GetMapPosition(room);
            GameObject cellObject = new($"Map Room {room.x},{room.y}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform cell = cellObject.GetComponent<RectTransform>();
            cell.SetParent(mapPanel, false);
            cell.anchorMin = center - Vector2.one * 0.052f;
            cell.anchorMax = center + Vector2.one * 0.052f;
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = Vector2.zero;

            Image image = cellObject.GetComponent<Image>();
            image.color = new Color(0.025f, 0.012f, 0.008f, 0.96f);
            image.raycastTarget = false;
            mapCells.Add(room, image);

            TextMeshProUGUI label = CreateText("Room State", cell, Vector2.zero, Vector2.one,
                "?", 20f, new Color(0.38f, 0.28f, 0.22f), TextAlignmentOptions.Center);
            mapCellLabels.Add(room, label);
        }

        private void UpdateMap()
        {
            foreach (KeyValuePair<Vector2Int, Image> pair in mapCells)
            {
                bool visited = visitedRooms.Contains(pair.Key);
                bool current = pair.Key == currentRoom;
                Image image = pair.Value;
                TextMeshProUGUI label = mapCellLabels[pair.Key];

                if (!visited)
                {
                    image.color = new Color(0.025f, 0.012f, 0.008f, 0.96f);
                    label.text = "?";
                    label.color = new Color(0.38f, 0.28f, 0.22f);
                    image.rectTransform.localScale = Vector3.one;
                    continue;
                }

                int number = (pair.Key.y - MinimumRoomY) * (MaximumRoomX - MinimumRoomX + 1) +
                             (pair.Key.x - MinimumRoomX) + 1;
                image.color = current
                    ? new Color(1f, 0.46f, 0.12f, 1f)
                    : new Color(0.48f, 0.18f, 0.07f, 1f);
                label.text = number.ToString("00");
                label.color = current ? Color.white : new Color(1f, 0.82f, 0.58f);
                image.rectTransform.localScale = current ? Vector3.one * 1.16f : Vector3.one;
            }

            foreach (MapConnection connection in mapConnections)
            {
                bool unlocked = visitedRooms.Contains(connection.First) &&
                                visitedRooms.Contains(connection.Second);
                connection.Image.gameObject.SetActive(unlocked);
            }
        }

        private void ToggleMapSize()
        {
            if (mapPanel == null) return;
            mapExpanded = !mapExpanded;
            if (mapExpanded)
            {
                mapPanel.anchorMin = new Vector2(0.31f, 0.14f);
                mapPanel.anchorMax = new Vector2(0.69f, 0.88f);
            }
            else
            {
                mapPanel.anchorMin = new Vector2(0.79f, 0.59f);
                mapPanel.anchorMax = new Vector2(0.975f, 0.95f);
            }
            mapPanel.offsetMin = Vector2.zero;
            mapPanel.offsetMax = Vector2.zero;
        }

        private static Vector2 GetMapPosition(Vector2Int room)
        {
            float normalizedX = Mathf.InverseLerp(MinimumRoomX, MaximumRoomX, room.x);
            float normalizedY = Mathf.InverseLerp(MinimumRoomY, MaximumRoomY, room.y);
            return new Vector2(Mathf.Lerp(0.18f, 0.82f, normalizedX),
                Mathf.Lerp(0.12f, 0.8f, normalizedY));
        }

        private void BuildTransitionFade()
        {
            GameObject canvasObject = new("Room Transition");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvasObject.AddComponent<CanvasScaler>();

            GameObject black = new("Black Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(CanvasGroup));
            RectTransform rect = black.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.GetComponent<RectTransform>(), false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = black.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            transitionFade = black.GetComponent<CanvasGroup>();
            transitionFade.alpha = 1f;
            transitionFade.blocksRaycasts = true;
        }

        private IEnumerator FadeFromBlack()
        {
            yield return null;
            yield return FadeTo(0f, 0.42f);
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (transitionFade == null) yield break;
            float start = transitionFade.alpha;
            float elapsed = 0f;
            transitionFade.blocksRaycasts = true;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                transitionFade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            transitionFade.alpha = target;
            transitionFade.blocksRaycasts = target > 0.01f;
        }

        private void OpenTutorial()
        {
            tutorialOpen = true;
            tutorialUnlockTime = Time.unscaledTime + 0.45f;
            Time.timeScale = 0f;
            if (tutorialOverlay != null) tutorialOverlay.SetActive(true);
        }

        private void UpdateRoomHud()
        {
            if (roomLabel == null) return;
            int roomNumber = (currentRoom.y - MinimumRoomY) * (MaximumRoomX - MinimumRoomX + 1) +
                             (currentRoom.x - MinimumRoomX) + 1;
            roomLabel.text = GameLoadout.IsSpanish
                ? $"SALA {roomNumber:00}  ·  EXPLORADAS {visitedRooms.Count:00}/12"
                : $"ROOM {roomNumber:00}  ·  EXPLORED {visitedRooms.Count:00}/12";
            UpdateMap();
        }

        private static GameObject CreatePanel(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, Color color)
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

        private static TextMeshProUGUI CreateText(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, string value, float fontSize,
            Color color, TextAlignmentOptions alignment)
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

        private readonly struct MapConnection
        {
            public MapConnection(Image image, Vector2Int first, Vector2Int second)
            {
                Image = image;
                First = first;
                Second = second;
            }

            public Image Image { get; }
            public Vector2Int First { get; }
            public Vector2Int Second { get; }
        }

        private readonly struct RoomOpenings
        {
            public RoomOpenings(bool up, bool down, bool left, bool right)
            {
                Up = up;
                Down = down;
                Left = left;
                Right = right;
            }

            public bool Up { get; }
            public bool Down { get; }
            public bool Left { get; }
            public bool Right { get; }
        }
    }

    public enum RoomDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    public sealed class WorldRoomDoor : MonoBehaviour
    {
        private WorldPathBootstrap world;
        private RoomDirection direction;
        private bool entered;

        public void Configure(WorldPathBootstrap owner, RoomDirection doorDirection)
        {
            world = owner;
            direction = doorDirection;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (entered || !IsPlayer(other)) return;
            entered = true;
            world?.RequestRoomChange(direction);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other)) entered = false;
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other == null) return false;
            if (other.CompareTag("Player")) return true;
            Transform root = other.transform.root;
            return root != null && root.CompareTag("Player");
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
            worldCamera = GetComponent<Camera>();
            SetBounds(worldMinimum, worldMaximum);
        }

        public void SetBounds(Vector2 worldMinimum, Vector2 worldMaximum)
        {
            minimum = worldMinimum;
            maximum = worldMaximum;
        }

        public void SnapToTarget()
        {
            if (target == null || worldCamera == null) return;
            transform.position = ResolveDesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null || worldCamera == null) return;
            Vector3 desired = ResolveDesiredPosition();
            transform.position = Vector3.Lerp(transform.position, desired,
                1f - Mathf.Exp(-11f * Time.unscaledDeltaTime));
        }

        private Vector3 ResolveDesiredPosition()
        {
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(0.1f, worldCamera.aspect);
            Vector3 desired = target.position;
            desired.x = Mathf.Clamp(desired.x, minimum.x + halfWidth, maximum.x - halfWidth);
            desired.y = Mathf.Clamp(desired.y, minimum.y + halfHeight, maximum.y - halfHeight);
            desired.z = -10f;
            return desired;
        }
    }

    internal static class RuntimeWhiteSprite
    {
        private static Sprite instance;

        public static Sprite Instance
        {
            get
            {
                if (instance != null) return instance;
                Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "Runtime White Pixel",
                    filterMode = FilterMode.Point
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                instance = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f), 1f);
                instance.name = "Runtime White Sprite";
                return instance;
            }
        }
    }
}
