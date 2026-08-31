using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
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
        private const float RoomDoorGracePeriod = 0.75f;

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

        [Header("Destructible cover")]
        [SerializeField] private bool spawnDestructibles = true;
        [SerializeField, Range(0, 8)] private int destructiblesPerRoom = 5;
        [SerializeField, Min(1f)] private float destructibleHealth = 52f;

        [Header("Route rewards")]
        [SerializeField] private GameObject healthKitPrefab;
        [SerializeField] private GameObject manaPotionPrefab;

        [Header("Secondary enemy visuals")]
        [SerializeField] private Sprite spearGoblinIdleSprite;
        [SerializeField] private Sprite spearGoblinAttackSprite;
        [SerializeField] private Sprite archerGoblinIdleSprite;
        [SerializeField] private Sprite archerGoblinAttackSprite;
        [SerializeField] private Sprite[] spearGoblinIdleFrames;
        [SerializeField] private Sprite[] spearGoblinWalkFrames;
        [SerializeField] private Sprite[] spearGoblinAttackFrames;
        [SerializeField] private Sprite[] archerGoblinIdleFrames;
        [SerializeField] private Sprite[] archerGoblinWalkFrames;
        [SerializeField] private Sprite[] archerGoblinAttackFrames;

        [Header("Room presentation")]
        [SerializeField] private bool showRoomGuides = false;

        private readonly List<Tile> runtimeTiles = new();
        private readonly List<GameObject> roomObjects = new();
        private readonly HashSet<Vector2Int> visitedRooms = new();
        private readonly HashSet<Vector2Int> clearedCombatRooms = new();
        private readonly HashSet<Vector2Int> solvedPuzzleRooms = new();
        private readonly Dictionary<Vector2Int, Image> mapCells = new();
        private readonly Dictionary<Vector2Int, TextMeshProUGUI> mapCellLabels = new();
        private readonly List<MapConnection> mapConnections = new();

        private Tilemap background;
        private Tilemap floor;
        private Tilemap walls;
        private Tilemap details;
        private TileBase collisionWall;
        private GameObject playerActor;
        private Rigidbody2D playerBody;
        private Health playerHealth;
        private PlayerMove playerMove;
        private WorldPathCamera worldCamera;
        private CanvasGroup transitionFade;
        private TextMeshProUGUI transitionLabel;
        private GameObject tutorialOverlay;
        private GameObject objectiveHud;
        private TextMeshProUGUI roomLabel;
        private TextMeshProUGUI deathCounter;
        private RectTransform mapPanel;
        private float tutorialUnlockTime;
        private bool tutorialOpen;
        private bool transitioning;
        private bool mapExpanded;
        private bool roomChallengeLocked;
        private int activeRoomThreats;
        private Vector2Int currentRoom;
        private RoomDirection lockedDoorDirection;
        private float doorLockUntil;

        private static readonly Vector2Int StartRoom = Vector2Int.zero;
        private static readonly Vector2Int BossGatewayRoom = new(0, MaximumRoomY);
        private static readonly Vector2Int BossRoom = new(0, MaximumRoomY + 1);
        private static readonly Vector2Int[] RouteRooms =
        {
            StartRoom,
            new Vector2Int(0, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, 2),
            new Vector2Int(1, 1),
            new Vector2Int(1, 2),
            new Vector2Int(0, 2),
            BossGatewayRoom
        };
        private static readonly RouteConnection[] RouteConnections =
        {
            new RouteConnection(StartRoom, new Vector2Int(0, 1)),
            new RouteConnection(new Vector2Int(0, 1), new Vector2Int(0, 2)),
            new RouteConnection(new Vector2Int(0, 2), BossGatewayRoom),
            new RouteConnection(new Vector2Int(0, 1), new Vector2Int(-1, 1)),
            new RouteConnection(new Vector2Int(-1, 1), new Vector2Int(-1, 2)),
            new RouteConnection(new Vector2Int(-1, 2), new Vector2Int(0, 2)),
            new RouteConnection(new Vector2Int(0, 1), new Vector2Int(1, 1)),
            new RouteConnection(new Vector2Int(1, 1), new Vector2Int(1, 2)),
            new RouteConnection(new Vector2Int(1, 2), new Vector2Int(0, 2))
        };

        private void Awake()
        {
            Time.timeScale = 1f;
            RunSession.EnsureRunStarted();
            BuildReusableRoom();
            GameObject player = SpawnPlayer();
            playerActor = player;
            playerBody = ResolvePlayerBody(player);
            playerHealth = player != null ? player.GetComponentInChildren<Health>(true) : null;
            playerMove = player != null ? player.GetComponentInChildren<PlayerMove>(true) : null;
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
            RunSession.UnregisterPlayer(playerHealth);
            Time.timeScale = 1f;
            foreach (Tile tile in runtimeTiles)
            {
                if (tile != null) Destroy(tile);
            }
        }

        internal void RequestRoomChange(RoomDirection direction)
        {
            if (transitioning || tutorialOpen || direction == RoomDirection.None || IsDoorLocked(direction)) return;
            Vector2Int destination = currentRoom + DirectionOffset(direction);
            if (destination == BossRoom)
            {
                StartCoroutine(EnterBossRoom());
                return;
            }

            if (!IsValidRoom(destination)) return;
            StartCoroutine(ChangeRoom(destination, OppositeDirection(direction)));
        }

        private IEnumerator ChangeRoom(Vector2Int destination, RoomDirection entrySide)
        {
            transitioning = true;
            Time.timeScale = 1f;
            SetTransitionLabel(destination);
            SetPlayerTransitionLock(true);

            yield return FadeTo(1f, 0.22f);
            LoadRoom(destination, entrySide);
            yield return new WaitForSecondsRealtime(0.12f);

            if (playerBody != null) playerBody.simulated = true;
            yield return FadeTo(0f, 0.3f);
            SetPlayerTransitionLock(false);
            transitioning = false;
        }

        private IEnumerator EnterBossRoom()
        {
            transitioning = true;
            Time.timeScale = 1f;
            SetTransitionLabel(BossRoom);
            SetPlayerTransitionLock(true);

            yield return FadeTo(1f, 0.34f);
            if (string.IsNullOrWhiteSpace(bossSceneName) ||
                !Application.CanStreamedLevelBeLoaded(bossSceneName))
            {
                Debug.LogError($"WorldPath cannot load the boss scene '{bossSceneName}'.", this);
                SetPlayerTransitionLock(false);
                transitioning = false;
                yield return FadeTo(0f, 0.3f);
                yield break;
            }

            RunSession.MarkBossCheckpoint();
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(bossSceneName);
            if (loadOperation != null)
            {
                yield return loadOperation;
                yield break;
            }

            Debug.LogError($"WorldPath failed to start loading the boss scene '{bossSceneName}'.", this);
            SetPlayerTransitionLock(false);
            transitioning = false;
            yield return FadeTo(0f, 0.3f);
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

        private void LoadRoom(Vector2Int room, RoomDirection entrySide)
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

            lockedDoorDirection = entrySide;
            doorLockUntil = entrySide == RoomDirection.None
                ? 0f
                : Time.unscaledTime + RoomDoorGracePeriod;
            PlacePlayerAtEntry(GetEntryPosition(entrySide));

            if (worldCamera != null) worldCamera.SnapToTarget();
            UpdateRoomHud();
        }

        private void SetPlayerTransitionLock(bool locked)
        {
            playerMove?.SetRoomTransitionLock(locked);
            if (playerBody == null) return;

            playerBody.linearVelocity = Vector2.zero;
            playerBody.simulated = !locked;
        }

        private void PlacePlayerAtEntry(Vector2 entry)
        {
            if (playerActor != null && playerBody != null)
            {
                Vector2 bodyOffset = playerBody.transform.position - playerActor.transform.position;
                Vector3 actorPosition = playerActor.transform.position;
                actorPosition.x = entry.x - bodyOffset.x;
                actorPosition.y = entry.y - bodyOffset.y;
                playerActor.transform.position = actorPosition;
            }
            else if (playerActor != null)
            {
                Vector3 actorPosition = playerActor.transform.position;
                actorPosition.x = entry.x;
                actorPosition.y = entry.y;
                playerActor.transform.position = actorPosition;
            }

            if (playerBody == null) return;
            playerBody.position = entry;
            playerBody.transform.position = new Vector3(entry.x, entry.y, playerBody.transform.position.z);
            playerBody.linearVelocity = Vector2.zero;
            playerBody.Sleep();
        }

        private void ClearRoom()
        {
            roomChallengeLocked = false;
            activeRoomThreats = 0;
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
            BuildRoomPresentation(room);

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

            BuildDestructibles(room);
            BuildRoomPuzzle(room);
            BuildRoomEncounter(room);
        }

        private void BuildDestructibles(Vector2Int room)
        {
            if (!spawnDestructibles || destructiblesPerRoom <= 0) return;

            Vector3Int[] candidates =
            {
                new(-12, 8, 0), new(12, 8, 0), new(-12, -8, 0), new(12, -8, 0),
                new(-6, 7, 0), new(6, 7, 0), new(-6, -7, 0), new(6, -7, 0),
                new(0, 8, 0), new(0, -8, 0)
            };
            RoomProfile profile = GetRoomProfile(room);
            int targetCount = Mathf.Clamp(destructiblesPerRoom + profile.DestructibleBonus,
                0, candidates.Length);
            float roomHealth = Mathf.Max(1f, destructibleHealth + profile.HealthBonus);
            int startIndex = StableHash(room, 13, 7) % candidates.Length;
            int created = 0;

            for (int attempt = 0; attempt < candidates.Length && created < targetCount; attempt++)
            {
                Vector3Int cell = candidates[(startIndex + attempt) % candidates.Length];
                DestructiblePropType type = (attempt + room.x + room.y) % 2 == 0
                    ? DestructiblePropType.Crate
                    : DestructiblePropType.Boulder;
                Vector2 size = type == DestructiblePropType.Crate
                    ? new Vector2(0.95f, 0.95f)
                    : new Vector2(1.25f, 1.1f);
                Color color = type == DestructiblePropType.Crate
                    ? new Color(0.78f, 0.3f, 0.1f, 1f)
                    : new Color(0.18f, 0.66f, 0.74f, 1f);

                DestructibleProp prop = DestructibleProp.CreateRuntime(
                    $"Room Cover {created + 1}", new Vector2(cell.x, cell.y), size, color, type,
                    roomHealth, transform, profile.ManaReward);
                if (prop == null) continue;

                roomObjects.Add(prop.gameObject);
                created++;
            }
        }

        private void SpawnPuzzleReward(Vector2Int room)
        {
            GameObject prefab = GetRoomType(room) switch
            {
                WorldRoomType.PuzzleSequence => healthKitPrefab,
                WorldRoomType.PuzzleCircuit => manaPotionPrefab,
                _ => null
            };
            if (prefab == null) return;

            Vector2 position = GetRoomType(room) == WorldRoomType.PuzzleSequence
                ? new Vector2(0f, -5.2f)
                : new Vector2(0f, 5.2f);
            GameObject reward = Instantiate(prefab, new Vector3(position.x, position.y, -0.2f),
                Quaternion.identity, transform);
            reward.name = GetRoomType(room) == WorldRoomType.PuzzleSequence
                ? "Puzzle Health Reward"
                : "Puzzle Mana Reward";
            roomObjects.Add(reward);
        }

        private void BuildRoomPuzzle(Vector2Int room)
        {
            WorldRoomType type = GetRoomType(room);
            if (!IsPuzzleRoom(room)) return;
            if (solvedPuzzleRooms.Contains(room))
            {
                roomChallengeLocked = false;
                return;
            }

            roomChallengeLocked = true;
            WorldPuzzleKind puzzleKind = type == WorldRoomType.PuzzleSequence
                ? WorldPuzzleKind.Sequence
                : WorldPuzzleKind.Circuit;
            WorldPuzzleController puzzle = WorldPuzzleController.CreateRuntime(
                puzzleKind, playerBody != null
                    ? playerBody.transform
                    : playerActor != null ? playerActor.transform : null,
                transform,
                () => CompletePuzzle(room));
            if (puzzle != null)
            {
                roomObjects.Add(puzzle.gameObject);
                return;
            }

            roomChallengeLocked = false;
        }

        private void CompletePuzzle(Vector2Int room)
        {
            if (room != currentRoom || solvedPuzzleRooms.Contains(room)) return;
            solvedPuzzleRooms.Add(room);
            roomChallengeLocked = false;
            SpawnPuzzleReward(room);
            UpdateRoomHud();
        }

        private void BuildRoomEncounter(Vector2Int room)
        {
            if (GetRoomType(room) != WorldRoomType.Combat) return;
            if (clearedCombatRooms.Contains(room))
            {
                roomChallengeLocked = false;
                activeRoomThreats = 0;
                return;
            }

            Transform target = playerBody != null
                ? playerBody.transform
                : playerActor != null ? playerActor.transform : null;
            if (target == null)
            {
                roomChallengeLocked = false;
                return;
            }

            roomChallengeLocked = true;
            activeRoomThreats = 0;
            int enemyCount = GetCombatEnemyCount(room);
            Vector2[] spawnPositions =
            {
                new(-8f, 6f), new(8f, 6f), new(-8f, -6f), new(8f, -6f),
                new(0f, 6.5f), new(0f, -6.5f)
            };

            for (int index = 0; index < enemyCount; index++)
            {
                WorldEnemyPattern pattern = GetEnemyPattern(room, index);
                Sprite idleSprite = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinIdleSprite
                    : spearGoblinIdleSprite;
                Sprite actionSprite = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinAttackSprite
                    : spearGoblinAttackSprite;
                Sprite[] idleFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinIdleFrames
                    : spearGoblinIdleFrames;
                Sprite[] walkFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinWalkFrames
                    : spearGoblinWalkFrames;
                Sprite[] attackFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinAttackFrames
                    : spearGoblinAttackFrames;
                WorldSecondaryEnemy enemy = WorldSecondaryEnemy.CreateRuntime(
                    $"Secondary Enemy {index + 1}", spawnPositions[index], pattern,
                    GetEnemyHealth(room, index), GetEnemySpeed(pattern), GetEnemyDamage(pattern),
                    idleSprite, actionSprite, idleFrames, walkFrames, attackFrames, target, transform,
                    roomObject => roomObjects.Add(roomObject),
                    NotifyRoomEnemyDefeated);
                if (enemy == null) continue;

                roomObjects.Add(enemy.gameObject);
                activeRoomThreats++;
            }

            if (activeRoomThreats == 0) roomChallengeLocked = false;
        }

        private static int GetCombatEnemyCount(Vector2Int room)
        {
            if (room == new Vector2Int(0, 1)) return 2;
            if (room == new Vector2Int(-1, 1)) return 3;
            if (room == new Vector2Int(1, 2)) return 4;
            return 3;
        }

        private static WorldEnemyPattern GetEnemyPattern(Vector2Int room, int index)
        {
            if (room == new Vector2Int(-1, 1))
                return index == 0 ? WorldEnemyPattern.Charger : WorldEnemyPattern.Chaser;
            if (room == new Vector2Int(1, 2))
                return index % 3 == 0 ? WorldEnemyPattern.Shooter
                    : index % 3 == 1 ? WorldEnemyPattern.Charger
                    : WorldEnemyPattern.Chaser;

            return index % 2 == 0 ? WorldEnemyPattern.Chaser : WorldEnemyPattern.Shooter;
        }

        private static float GetEnemyHealth(Vector2Int room, int index)
        {
            float baseHealth = room == new Vector2Int(1, 2) ? 84f : 64f;
            return baseHealth + index * 8f;
        }

        private static float GetEnemySpeed(WorldEnemyPattern pattern)
        {
            return pattern switch
            {
                WorldEnemyPattern.Charger => 2.9f,
                WorldEnemyPattern.Shooter => 2.2f,
                _ => 2.7f
            };
        }

        private static float GetEnemyDamage(WorldEnemyPattern pattern)
        {
            return pattern switch
            {
                WorldEnemyPattern.Charger => 24f,
                WorldEnemyPattern.Shooter => 13f,
                _ => 16f
            };
        }

        internal void NotifyRoomEnemyDefeated()
        {
            if (activeRoomThreats <= 0) return;
            activeRoomThreats--;
            if (activeRoomThreats > 0) return;

            clearedCombatRooms.Add(currentRoom);
            roomChallengeLocked = false;
            UpdateRoomHud();
        }

        private static WorldRoomType GetRoomType(Vector2Int room)
        {
            if (room == StartRoom) return WorldRoomType.Start;
            if (room == BossGatewayRoom) return WorldRoomType.BossGateway;
            if (room == new Vector2Int(-1, 2)) return WorldRoomType.PuzzleSequence;
            if (room == new Vector2Int(1, 1)) return WorldRoomType.PuzzleCircuit;
            return WorldRoomType.Combat;
        }

        private static bool IsPuzzleRoom(Vector2Int room)
        {
            WorldRoomType type = GetRoomType(room);
            return type == WorldRoomType.PuzzleSequence || type == WorldRoomType.PuzzleCircuit;
        }

        private static RoomProfile GetRoomProfile(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Start => new RoomProfile(-3, -10f, 5f),
                WorldRoomType.Combat => new RoomProfile(1, 10f, 8f),
                WorldRoomType.PuzzleSequence => new RoomProfile(-1, 0f, 8f),
                WorldRoomType.PuzzleCircuit => new RoomProfile(-1, 0f, 8f),
                WorldRoomType.BossGateway => new RoomProfile(-2, 0f, 8f),
                _ => new RoomProfile(0, 0f, 8f)
            };
        }

        private static string GetRoomDisplayName(Vector2Int room, bool spanish)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Start => spanish ? "CAMPAMENTO" : "CAMP",
                WorldRoomType.Combat => spanish ? "COMBATE" : "FIGHT",
                WorldRoomType.PuzzleSequence => spanish ? "ORDEN" : "SEQUENCE",
                WorldRoomType.PuzzleCircuit => spanish ? "CIRCUITO" : "CIRCUIT",
                WorldRoomType.BossGateway => spanish ? "UMBRAL BOSS" : "BOSS GATE",
                _ => spanish ? "SALA" : "ROOM"
            };
        }

        private static Color GetRoomMapColor(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Combat => new Color(0.78f, 0.18f, 0.08f, 1f),
                WorldRoomType.PuzzleSequence => new Color(0.18f, 0.58f, 0.36f, 1f),
                WorldRoomType.PuzzleCircuit => new Color(0.12f, 0.52f, 0.72f, 1f),
                WorldRoomType.BossGateway => new Color(0.78f, 0.34f, 0.08f, 1f),
                _ => new Color(0.48f, 0.18f, 0.07f, 1f)
            };
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
                new(-5, 8, 0), new(5, -8, 0),
                new(-9, 10, 0), new(9, 10, 0), new(-9, -10, 0), new(9, -10, 0),
                new(-15, 0, 0), new(15, 0, 0)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                TileBase decoration = decorationTiles[Mathf.Abs(room.x * 5 + room.y * 3 + index) %
                                                     decorationTiles.Length];
                if (decoration != null) details.SetTile(positions[index], decoration);
            }
        }

        private void BuildRoomPresentation(Vector2Int room)
        {
            if (!showRoomGuides) return;

            RoomVisualPalette palette = GetRoomVisualPalette(room);
            RoomOpenings openings = GetOpenings(room);
            Color shadow = WithAlpha(palette.Shadow, 0.36f);
            Color softAccent = WithAlpha(palette.Accent, 0.42f);
            Color softWarm = WithAlpha(palette.Warm, 0.48f);

            // The boss arena reads as a framed combat space. Reuse that visual grammar here
            // without adding colliders or affecting the procedural room layout.
            CreateRoomVisual("Room North Lintel", new Vector2(0f, 11.22f),
                new Vector2(32f, 0.55f), shadow, 1);
            CreateRoomVisual("Room South Lintel", new Vector2(0f, -11.22f),
                new Vector2(32f, 0.55f), shadow, 1);
            CreateRoomVisual("Room West Lintel", new Vector2(-17.22f, 0f),
                new Vector2(0.55f, 20f), shadow, 1);
            CreateRoomVisual("Room East Lintel", new Vector2(17.22f, 0f),
                new Vector2(0.55f, 20f), shadow, 1);

            CreateRoomVisual("Room North Energy Rail", new Vector2(0f, 10.72f),
                new Vector2(29f, 0.12f), softAccent, 2);
            CreateRoomVisual("Room South Energy Rail", new Vector2(0f, -10.72f),
                new Vector2(29f, 0.12f), softWarm, 2);
            CreateRoomVisual("Room West Energy Rail", new Vector2(-16.72f, 0f),
                new Vector2(0.12f, 19f), softWarm, 2);
            CreateRoomVisual("Room East Energy Rail", new Vector2(16.72f, 0f),
                new Vector2(0.12f, 19f), softAccent, 2);

            BuildCornerDressing(new Vector2(-16.35f, -10.35f), palette, 0);
            BuildCornerDressing(new Vector2(16.35f, -10.35f), palette, 1);
            BuildCornerDressing(new Vector2(-16.35f, 10.35f), palette, 2);
            BuildCornerDressing(new Vector2(16.35f, 10.35f), palette, 3);

            int pattern = Mathf.Abs(room.x * 31 + room.y * 17) % 4;
            Color coreColor = pattern % 2 == 0 ? palette.Accent : palette.Warm;
            CreateRoomVisual("Room Core Shadow", Vector2.zero, new Vector2(4.8f, 4.8f),
                WithAlpha(palette.Shadow, 0.18f), 1);
            CreateRoomVisual("Room Core Horizontal", Vector2.zero, new Vector2(5.8f, 0.1f),
                WithAlpha(coreColor, 0.34f), 2);
            CreateRoomVisual("Room Core Vertical", Vector2.zero, new Vector2(0.1f, 5.8f),
                WithAlpha(coreColor, 0.34f), 2);
            CreateRoomVisual("Room Core Beacon", Vector2.zero, new Vector2(0.46f, 0.46f),
                WithAlpha(palette.Warm, 0.82f), 2);

            if (openings.Up) BuildDoorGuide(RoomDirection.Up, palette);
            if (openings.Down) BuildDoorGuide(RoomDirection.Down, palette);
            if (openings.Left) BuildDoorGuide(RoomDirection.Left, palette);
            if (openings.Right) BuildDoorGuide(RoomDirection.Right, palette);
        }

        private void BuildCornerDressing(Vector2 corner, RoomVisualPalette palette, int index)
        {
            float inwardX = -Mathf.Sign(corner.x);
            float inwardY = -Mathf.Sign(corner.y);
            Color accent = WithAlpha(palette.Warm, 0.82f);
            Color glow = WithAlpha(palette.Accent, 0.18f);

            CreateRoomVisual($"Room Corner Glow {index}", corner, new Vector2(1.25f, 1.25f), glow, 1);
            CreateRoomVisual($"Room Corner Horizontal {index}",
                corner + new Vector2(inwardX * 0.42f, 0f), new Vector2(0.9f, 0.12f), accent, 2);
            CreateRoomVisual($"Room Corner Vertical {index}",
                corner + new Vector2(0f, inwardY * 0.42f), new Vector2(0.12f, 0.9f), accent, 2);
            CreateRoomVisual($"Room Corner Lamp {index}", corner, new Vector2(0.24f, 0.24f),
                Color.white, 2);
        }

        private void BuildDoorGuide(RoomDirection direction, RoomVisualPalette palette)
        {
            Vector2 position;
            Vector2 size;
            switch (direction)
            {
                case RoomDirection.Up:
                    position = new Vector2(0f, 9.25f);
                    size = new Vector2(0.14f, 2.3f);
                    break;
                case RoomDirection.Down:
                    position = new Vector2(0f, -9.25f);
                    size = new Vector2(0.14f, 2.3f);
                    break;
                case RoomDirection.Left:
                    position = new Vector2(-13.85f, 0f);
                    size = new Vector2(2.3f, 0.14f);
                    break;
                case RoomDirection.Right:
                    position = new Vector2(13.85f, 0f);
                    size = new Vector2(2.3f, 0.14f);
                    break;
                default:
                    return;
            }

            CreateRoomVisual($"Room Door Guide {direction}", position, size,
                WithAlpha(palette.Accent, 0.2f), 2);
        }

        private GameObject CreateRoomVisual(string objectName, Vector2 position, Vector2 size,
            Color color, int sortingOrder)
        {
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform, false);
            visual.transform.position = new Vector3(position.x, position.y, 0f);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            roomObjects.Add(visual);
            return visual;
        }

        private static RoomVisualPalette GetRoomVisualPalette(Vector2Int room)
        {
            switch (GetRoomType(room))
            {
                case WorldRoomType.Combat:
                    return new RoomVisualPalette(
                        new Color(0.94f, 0.16f, 0.1f, 1f),
                        new Color(1f, 0.56f, 0.12f, 1f),
                        new Color(0.12f, 0.018f, 0.02f, 1f));
                case WorldRoomType.PuzzleSequence:
                    return new RoomVisualPalette(
                        new Color(0.18f, 0.86f, 0.5f, 1f),
                        new Color(0.74f, 1f, 0.4f, 1f),
                        new Color(0.015f, 0.09f, 0.06f, 1f));
                case WorldRoomType.PuzzleCircuit:
                    return new RoomVisualPalette(
                        new Color(0.16f, 0.7f, 1f, 1f),
                        new Color(0.34f, 0.96f, 0.92f, 1f),
                        new Color(0.015f, 0.055f, 0.12f, 1f));
                case WorldRoomType.BossGateway:
                    return new RoomVisualPalette(
                        new Color(1f, 0.24f, 0.08f, 1f),
                        new Color(1f, 0.72f, 0.18f, 1f),
                        new Color(0.12f, 0.025f, 0.012f, 1f));
            }

            int theme = Mathf.Abs(room.x * 7 + room.y * 13) % 3;
            return theme switch
            {
                0 => new RoomVisualPalette(
                    new Color(0.08f, 0.78f, 0.88f, 1f),
                    new Color(1f, 0.36f, 0.1f, 1f),
                    new Color(0.02f, 0.06f, 0.09f, 1f)),
                1 => new RoomVisualPalette(
                    new Color(0.24f, 0.62f, 1f, 1f),
                    new Color(1f, 0.68f, 0.16f, 1f),
                    new Color(0.04f, 0.05f, 0.14f, 1f)),
                _ => new RoomVisualPalette(
                    new Color(0.22f, 0.86f, 0.66f, 1f),
                    new Color(1f, 0.23f, 0.18f, 1f),
                    new Color(0.1f, 0.03f, 0.05f, 1f))
            };
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void BuildDoors(Vector2Int room)
        {
            RoomOpenings openings = GetOpenings(room);
            if (openings.Up)
                CreateDoor(room == BossGatewayRoom ? "Boss Door" : "North Door", RoomDirection.Up,
                    new Vector2(0f, RoomHalfHeight - 0.15f), new Vector2(3.4f, 1.1f),
                    room == BossGatewayRoom);
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

        private void CreateDoor(string objectName, RoomDirection direction, Vector2 position, Vector2 size,
            bool bossDoor = false)
        {
            GameObject door = new(objectName);
            door.transform.position = position;
            roomObjects.Add(door);

            SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = bossDoor
                ? new Color(0.9f, 0.12f, 0.06f, 1f)
                : new Color(0.72f, 0.29f, 0.1f, 1f);
            renderer.sortingOrder = 6;
            door.transform.localScale = new Vector3(size.x, size.y, 1f);

            GameObject glow = new("Door Glow");
            glow.transform.SetParent(door.transform, false);
            SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = RuntimeWhiteSprite.Instance;
            glowRenderer.color = bossDoor
                ? new Color(1f, 0.22f, 0.05f, 0.55f)
                : new Color(1f, 0.68f, 0.26f, 0.38f);
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
                HasRouteConnection(room, room + Vector2Int.up),
                HasRouteConnection(room, room + Vector2Int.down),
                HasRouteConnection(room, room + Vector2Int.left),
                HasRouteConnection(room, room + Vector2Int.right));
        }

        private static bool HasRouteConnection(Vector2Int first, Vector2Int second)
        {
            if ((first == BossGatewayRoom && second == BossRoom) ||
                (first == BossRoom && second == BossGatewayRoom))
                return true;

            foreach (RouteConnection connection in RouteConnections)
            {
                if ((connection.First == first && connection.Second == second) ||
                    (connection.First == second && connection.Second == first))
                    return true;
            }

            return false;
        }

        internal bool IsDoorLocked(RoomDirection direction)
        {
            if (direction == RoomDirection.None) return false;
            return roomChallengeLocked || (direction == lockedDoorDirection &&
                   Time.unscaledTime < doorLockUntil);
        }

        private static bool IsValidRoom(Vector2Int room)
        {
            foreach (Vector2Int routeRoom in RouteRooms)
            {
                if (routeRoom == room) return true;
            }

            return false;
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

        // La salida de la sala actual se convierte en el lado opuesto de entrada de la nueva.
        private static RoomDirection OppositeDirection(RoomDirection direction)
        {
            return direction switch
            {
                RoomDirection.Up => RoomDirection.Down,
                RoomDirection.Down => RoomDirection.Up,
                RoomDirection.Left => RoomDirection.Right,
                RoomDirection.Right => RoomDirection.Left,
                _ => RoomDirection.None
            };
        }

        private static Vector2 GetEntryPosition(RoomDirection entrySide)
        {
            return entrySide switch
            {
                RoomDirection.Up => new Vector2(0f, RoomHalfHeight - DoorInset - 1f),
                RoomDirection.Down => new Vector2(0f, -RoomHalfHeight + DoorInset + 1f),
                RoomDirection.Left => new Vector2(-RoomHalfWidth + DoorInset + 1f, 0f),
                RoomDirection.Right => new Vector2(RoomHalfWidth - DoorInset - 1f, 0f),
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

            RunSession.RegisterPlayer(player.GetComponentInChildren<Health>(true));

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
            camera.orthographicSize = GetRoomCameraSize(camera.aspect);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.008f, 0.012f, 1f);
            camera.transform.position = new Vector3(0f, 0.5f, -10f);
            cameraObject.AddComponent<AudioListener>();

            WorldPathCamera follow = cameraObject.AddComponent<WorldPathCamera>();
            follow.Configure(target, new Vector2(-RoomHalfWidth, -RoomHalfHeight),
                new Vector2(RoomHalfWidth + 1f, RoomHalfHeight + 1f));
            return follow;
        }

        private static float GetRoomCameraSize(float aspect)
        {
            float roomWidth = (RoomHalfWidth + 1f) - -RoomHalfWidth;
            float roomHeight = (RoomHalfHeight + 1f) - -RoomHalfHeight;
            float verticalHalfSize = roomHeight * 0.5f + 0.35f;
            float horizontalHalfSize = roomWidth / (2f * Mathf.Max(0.1f, aspect)) + 0.35f;
            return Mathf.Max(verticalHalfSize, horizontalHalfSize);
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

            GameObject deathPanel = CreatePanel("Death Counter", canvasRect,
                new Vector2(0.025f, 0.91f), new Vector2(0.20f, 0.975f),
                new Color(0.09f, 0.025f, 0.018f, 0.9f));
            deathCounter = CreateText("Death Counter Text", deathPanel.transform as RectTransform,
                new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), string.Empty, 20f,
                new Color(1f, 0.62f, 0.38f), TextAlignmentOptions.Center);
            UpdateDeathCounter(RunSession.PlayerDeaths);
            BuildMap(canvasRect);

            tutorialOverlay = CreatePanel("Tutorial Overlay", canvasRect, Vector2.zero, Vector2.one,
                new Color(0.1f, 0.035f, 0.018f, 0.08f));
            GameObject card = CreatePanel("Tutorial Card", tutorialOverlay.transform as RectTransform,
                new Vector2(0.27f, 0.18f), new Vector2(0.73f, 0.82f),
                new Color(0.19f, 0.065f, 0.032f, 0.96f));

            bool spanish = GameLoadout.IsSpanish;
            string title = spanish ? "SALAS DE LA CAVERNA" : "CAVERN ROOMS";
            string subtitle = spanish
                ? "Elige la ruta directa o un desvio: dos salas tienen puzzle y el resto combate."
                : "Choose the direct route or a detour: two rooms have puzzles and the rest are fights.";
            string controls = spanish
                ? "WASD / FLECHAS     MOVERSE\nRATÓN                 APUNTAR\nCLIC IZQUIERDO        DISPARAR\nESPACIO               DASH · GOLPE · CARGAS\nR                      RECARGAR\nE                      INTERACTUAR\nPUERTAS                CAMBIAR DE SALA"
                : "WASD / ARROWS      MOVE\nMOUSE                  AIM\nLEFT CLICK             SHOOT\nSPACE                  DASH · STRIKE · CHARGES\nR                      RELOAD\nE                      INTERACT\nDOORS                   CHANGE ROOM";
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
            CreateText("Map Legend", mapPanel, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.11f),
                GameLoadout.IsSpanish ? "! COMBATE   A/C PUZZLE" : "! FIGHT   A/C PUZZLE", 12f,
                new Color(0.78f, 0.58f, 0.42f), TextAlignmentOptions.Center);

            foreach (RouteConnection connection in RouteConnections)
                CreateMapConnection(connection.First, connection.Second);

            CreateMapConnection(BossGatewayRoom, BossRoom);

            foreach (Vector2Int room in RouteRooms)
                CreateMapCell(room);

            CreateMapCell(BossRoom);
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
            float cellHalfSize = room == BossRoom ? 0.07f : 0.052f;
            cell.anchorMin = center - Vector2.one * cellHalfSize;
            cell.anchorMax = center + Vector2.one * cellHalfSize;
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = Vector2.zero;

            Image image = cellObject.GetComponent<Image>();
            image.color = new Color(0.025f, 0.012f, 0.008f, 0.96f);
            image.raycastTarget = false;
            mapCells.Add(room, image);

            TextMeshProUGUI label = CreateText("Room State", cell, Vector2.zero, Vector2.one,
                room == BossRoom ? "BOSS" : GetRoomMapSymbol(room), room == BossRoom ? 14f : 20f,
                room == BossRoom ? new Color(1f, 0.2f, 0.08f) : new Color(0.38f, 0.28f, 0.22f),
                TextAlignmentOptions.Center);
            mapCellLabels.Add(room, label);
        }

        private void UpdateMap()
        {
            bool bossUnlocked = visitedRooms.Contains(BossGatewayRoom);
            foreach (KeyValuePair<Vector2Int, Image> pair in mapCells)
            {
                bool visited = visitedRooms.Contains(pair.Key);
                bool current = pair.Key == currentRoom;
                Image image = pair.Value;
                TextMeshProUGUI label = mapCellLabels[pair.Key];

                if (pair.Key == BossRoom)
                {
                    image.color = bossUnlocked
                        ? new Color(0.78f, 0.12f, 0.05f, 1f)
                        : new Color(0.22f, 0.025f, 0.018f, 1f);
                    label.text = "BOSS";
                    label.color = bossUnlocked
                        ? new Color(1f, 0.9f, 0.6f, 1f)
                        : new Color(0.76f, 0.14f, 0.08f, 1f);
                    image.rectTransform.localScale = bossUnlocked
                        ? Vector3.one * 1.12f
                        : Vector3.one;
                    continue;
                }

                if (!visited)
                {
                    image.color = new Color(0.025f, 0.012f, 0.008f, 0.96f);
                    label.text = GetRoomMapSymbol(pair.Key);
                    label.color = new Color(0.38f, 0.28f, 0.22f);
                    image.rectTransform.localScale = Vector3.one;
                    continue;
                }

                int number = GetRoomNumber(pair.Key);
                Color roomColor = GetRoomMapColor(pair.Key);
                image.color = current
                    ? new Color(1f, 0.46f, 0.12f, 1f)
                    : roomColor;
                label.text = number.ToString("00");
                label.color = current ? Color.white : new Color(1f, 0.82f, 0.58f);
                image.rectTransform.localScale = current ? Vector3.one * 1.16f : Vector3.one;
            }

            foreach (MapConnection connection in mapConnections)
            {
                bool bossConnection = connection.First == BossRoom || connection.Second == BossRoom;
                bool unlocked = bossConnection
                    ? bossUnlocked
                    : (visitedRooms.Contains(connection.First) && visitedRooms.Contains(connection.Second)) ||
                      connection.First == currentRoom || connection.Second == currentRoom;
                connection.Image.gameObject.SetActive(unlocked);
            }
        }

        private static string GetRoomMapSymbol(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Start => "S",
                WorldRoomType.Combat => "!",
                WorldRoomType.PuzzleSequence => "A",
                WorldRoomType.PuzzleCircuit => "C",
                WorldRoomType.BossGateway => "G",
                _ => "?"
            };
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
            float normalizedY = Mathf.InverseLerp(MinimumRoomY, BossRoom.y, room.y);
            return new Vector2(Mathf.Lerp(0.18f, 0.82f, normalizedX),
                Mathf.Lerp(0.12f, 0.8f, normalizedY));
        }

        private void BuildTransitionFade()
        {
            GameObject canvasObject = new("Room Transition");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

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
            transitionLabel = CreateText("Transition Label", rect, new Vector2(0.2f, 0.44f),
                new Vector2(0.8f, 0.56f), string.Empty, 38f,
                new Color(1f, 0.82f, 0.56f), TextAlignmentOptions.Center);
            transitionLabel.raycastTarget = false;
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
            int roomNumber = GetRoomNumber(currentRoom);
            bool spanish = GameLoadout.IsSpanish;
            string roomName = GetRoomDisplayName(currentRoom, spanish);
            roomLabel.text = spanish
                ? $"SALA {roomNumber:00}  ·  {roomName}  ·  {visitedRooms.Count:00}/{RouteRooms.Length:00}"
                : $"ROOM {roomNumber:00}  ·  {roomName}  ·  {visitedRooms.Count:00}/{RouteRooms.Length:00}";
            UpdateMap();
        }

        private static int GetRoomNumber(Vector2Int room)
        {
            for (int index = 0; index < RouteRooms.Length; index++)
            {
                if (RouteRooms[index] == room) return index + 1;
            }

            return room == BossRoom ? RouteRooms.Length + 1 : 0;
        }

        private void OnEnable()
        {
            RunSession.OnPlayerDeathsChanged += UpdateDeathCounter;
        }

        private void OnDisable()
        {
            RunSession.OnPlayerDeathsChanged -= UpdateDeathCounter;
        }

        private void UpdateDeathCounter(int deaths)
        {
            if (deathCounter == null) return;
            deathCounter.text = GameLoadout.IsSpanish
                ? $"MUERTES  {deaths:00}"
                : $"DEATHS  {deaths:00}";
        }

        private enum WorldRoomType
        {
            Start,
            Combat,
            PuzzleSequence,
            PuzzleCircuit,
            BossGateway
        }

        private readonly struct RouteConnection
        {
            public RouteConnection(Vector2Int first, Vector2Int second)
            {
                First = first;
                Second = second;
            }

            public Vector2Int First { get; }
            public Vector2Int Second { get; }
        }

        private readonly struct RoomProfile
        {
            public RoomProfile(int destructibleBonus, float healthBonus, float manaReward)
            {
                DestructibleBonus = destructibleBonus;
                HealthBonus = healthBonus;
                ManaReward = manaReward;
            }

            public int DestructibleBonus { get; }
            public float HealthBonus { get; }
            public float ManaReward { get; }
        }

        private void SetTransitionLabel(Vector2Int destination)
        {
            if (transitionLabel == null) return;

            if (destination == BossRoom)
            {
                transitionLabel.text = GameLoadout.IsSpanish ? "SALA DEL JEFE" : "BOSS ROOM";
                return;
            }

            int roomNumber = GetRoomNumber(destination);
            string roomName = GetRoomDisplayName(destination, GameLoadout.IsSpanish);
            transitionLabel.text = GameLoadout.IsSpanish
                ? $"SALA {roomNumber:00}  ·  {roomName}"
                : $"ROOM {roomNumber:00}  ·  {roomName}";
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

        private readonly struct RoomVisualPalette
        {
            public RoomVisualPalette(Color accent, Color warm, Color shadow)
            {
                Accent = accent;
                Warm = warm;
                Shadow = shadow;
            }

            public Color Accent { get; }
            public Color Warm { get; }
            public Color Shadow { get; }
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
            if (entered || !IsPlayer(other) || world == null || world.IsDoorLocked(direction)) return;
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
            FrameEntireRoom();
        }

        public void SnapToTarget()
        {
            if (target == null || worldCamera == null) return;
            transform.position = ResolveDesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null || worldCamera == null) return;
            FrameEntireRoom();
            Vector3 desired = ResolveDesiredPosition();
            transform.position = Vector3.Lerp(transform.position, desired,
                1f - Mathf.Exp(-11f * Time.unscaledDeltaTime));
        }

        private void FrameEntireRoom()
        {
            if (worldCamera == null || !worldCamera.orthographic) return;
            float requiredSize = Mathf.Max(
                (maximum.y - minimum.y) * 0.5f + 0.35f,
                (maximum.x - minimum.x) / (2f * Mathf.Max(0.1f, worldCamera.aspect)) + 0.35f);
            worldCamera.orthographicSize = requiredSize;
        }

        private Vector3 ResolveDesiredPosition()
        {
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(0.1f, worldCamera.aspect);
            Vector3 desired = target.position;
            float minimumX = minimum.x + halfWidth;
            float maximumX = maximum.x - halfWidth;
            float minimumY = minimum.y + halfHeight;
            float maximumY = maximum.y - halfHeight;
            desired.x = minimumX <= maximumX
                ? Mathf.Clamp(desired.x, minimumX, maximumX)
                : (minimum.x + maximum.x) * 0.5f;
            desired.y = minimumY <= maximumY
                ? Mathf.Clamp(desired.y, minimumY, maximumY)
                : (minimum.y + maximum.y) * 0.5f;
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
