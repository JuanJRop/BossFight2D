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
        private const float MapDecorationScaleMultiplier = 1.55f;
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

        [Header("Dungeon room art kit")]
        [SerializeField] private Sprite[] ambientPropSprites;
        [SerializeField] private Sprite mineEntranceSprite;
        [SerializeField] private Sprite mineEntranceAltSprite;
        [SerializeField] private Sprite mineCartSprite;
        [SerializeField] private Sprite mineLadderSprite;
        [SerializeField] private Sprite mineBeamSprite;

        [Header("Reserved destination")]
        [SerializeField] private string bossSceneName = "BossFight";

        [Header("Destructible cover")]
        [SerializeField] private bool spawnDestructibles = true;
        [SerializeField, Range(0, 8)] private int destructiblesPerRoom = 5;
        [SerializeField, Min(1f)] private float destructibleHealth = 52f;

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
        [SerializeField] private Sprite[] spearGoblinIdleUpFrames;
        [SerializeField] private Sprite[] spearGoblinIdleSideFrames;
        [SerializeField] private Sprite[] spearGoblinWalkUpFrames;
        [SerializeField] private Sprite[] spearGoblinWalkSideFrames;
        [SerializeField] private Sprite[] spearGoblinAttackUpFrames;
        [SerializeField] private Sprite[] spearGoblinAttackSideFrames;
        [SerializeField] private Sprite[] archerGoblinIdleUpFrames;
        [SerializeField] private Sprite[] archerGoblinIdleSideFrames;
        [SerializeField] private Sprite[] archerGoblinWalkUpFrames;
        [SerializeField] private Sprite[] archerGoblinWalkSideFrames;
        [SerializeField] private Sprite[] archerGoblinAttackUpFrames;
        [SerializeField] private Sprite[] archerGoblinAttackSideFrames;

        [Header("Room presentation")]
        [SerializeField] private bool showRoomGuides = true;

        private readonly List<Tile> runtimeTiles = new();
        private readonly List<GameObject> roomObjects = new();
        private readonly HashSet<Vector2Int> visitedRooms = new();
        private readonly HashSet<Vector2Int> clearedCombatRooms = new();
        private readonly HashSet<Vector2Int> solvedPuzzleRooms = new();
        private readonly HashSet<Vector2Int> claimedPuzzleRewardRooms = new();
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
        private CanvasGroup objectiveHudGroup;
        private TextMeshProUGUI roomLabel;
        private TextMeshProUGUI lessonLabel;
        private TextMeshProUGUI deathCounter;
        private float objectiveHudVisibleUntil;
        private RectTransform mapPanel;
        private float tutorialUnlockTime;
        private bool tutorialOpen;
        private bool transitioning;
        private bool mapExpanded;
        private bool roomChallengeLocked;
        private bool puzzleChestClaimed;
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
                objectiveHudVisibleUntil = Time.unscaledTime + 4.5f;
                return;
            }

            if (Input.GetKeyDown(KeyCode.M)) ToggleMapSize();
            UpdateRoomHudVisibility();
        }

        private void OnDestroy()
        {
            RunSession.UnregisterPlayer(playerHealth);
            Time.timeScale = 1f;
            foreach (Tile tile in runtimeTiles)
            {
                if (tile != null) Destroy(tile);
            }
            RuntimeCaveArt.Release();
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
            RunAbilityController.ResetRoomEffects();
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
                        TileBase roomFloor = pathTile != null ? pathTile : startAreaTile;
                        if (roomFloor == null) roomFloor = alternatePathTile;
                        if (room == StartRoom && startAreaAccentTile != null && hash % 31 == 0)
                            roomFloor = startAreaAccentTile;
                        floor.SetTile(cell, roomFloor);
                    }
                }
            }

            RoomOpenings openings = GetOpenings(room);
            PaintHorizontalWall(RoomHalfHeight, openings.Up);
            PaintHorizontalWall(-RoomHalfHeight, openings.Down);
            PaintVerticalWall(-RoomHalfWidth, openings.Left);
            PaintVerticalWall(RoomHalfWidth, openings.Right);
            PaintDungeonDetails(room);
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
            BuildRoomHazards(room);
            BuildPuzzleChest(room);
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
                DestructibleRewardType rewardType = GetDestructibleRewardType(room, created);

                DestructibleProp prop = DestructibleProp.CreateRuntime(
                    $"Room Cover {created + 1}", new Vector2(cell.x, cell.y), size, color, type,
                    roomHealth, transform, rewardType,
                    GetDestructibleRewardAmount(room, created, rewardType),
                    rewardObject =>
                    {
                        if (rewardObject != null) roomObjects.Add(rewardObject);
                    });
                if (prop == null) continue;

                roomObjects.Add(prop.gameObject);
                created++;
            }
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
            BuildPuzzleChest(room);
            UpdateRoomHud();
        }

        private void BuildPuzzleChest(Vector2Int room)
        {
            Transform target = playerBody != null
                ? playerBody.transform
                : playerActor != null ? playerActor.transform : null;
            if (target == null) return;

            if (IsPuzzleRoom(room) && solvedPuzzleRooms.Contains(room) &&
                !claimedPuzzleRewardRooms.Contains(room))
            {
                WorldPuzzleChest chest = WorldPuzzleChest.CreateRuntime(
                    new Vector2(0f, -5.2f), target, transform,
                    () => OpenPuzzleRewardChest(room), false);
                if (chest != null) roomObjects.Add(chest.gameObject);
                return;
            }

            if (room != BossGatewayRoom || puzzleChestClaimed || solvedPuzzleRooms.Count < 2) return;

            WorldPuzzleChest finalChest = WorldPuzzleChest.CreateRuntime(new Vector2(0f, -1.8f),
                target, transform, OpenPuzzleChest, true);
            if (finalChest != null) roomObjects.Add(finalChest.gameObject);
        }

        private void OpenPuzzleRewardChest(Vector2Int room)
        {
            if (!claimedPuzzleRewardRooms.Add(room)) return;
            RunSession.GrantPuzzleReward(85, 1, 0.2f);
            UpdateRoomHud();
        }

        private void OpenPuzzleChest()
        {
            if (puzzleChestClaimed) return;
            puzzleChestClaimed = true;
            RunSession.GrantPuzzleChestReward();
            UpdateRoomHud();
        }

        private void BuildRoomEncounter(Vector2Int room)
        {
            if (!IsCombatRoom(room)) return;
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
                Sprite[] idleUpFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinIdleUpFrames
                    : spearGoblinIdleUpFrames;
                Sprite[] idleSideFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinIdleSideFrames
                    : spearGoblinIdleSideFrames;
                Sprite[] walkUpFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinWalkUpFrames
                    : spearGoblinWalkUpFrames;
                Sprite[] walkSideFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinWalkSideFrames
                    : spearGoblinWalkSideFrames;
                Sprite[] attackUpFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinAttackUpFrames
                    : spearGoblinAttackUpFrames;
                Sprite[] attackSideFrames = pattern == WorldEnemyPattern.Shooter
                    ? archerGoblinAttackSideFrames
                    : spearGoblinAttackSideFrames;
                WorldSecondaryEnemy enemy = WorldSecondaryEnemy.CreateRuntime(
                    $"Secondary Enemy {index + 1}", spawnPositions[index], pattern,
                    GetEnemyHealth(room, index), GetEnemySpeed(pattern), GetEnemyDamage(pattern),
                    idleSprite, actionSprite, idleFrames, walkFrames, attackFrames,
                    idleUpFrames, idleSideFrames, walkUpFrames, walkSideFrames,
                    attackUpFrames, attackSideFrames, target, transform,
                    roomObject => roomObjects.Add(roomObject),
                    NotifyRoomEnemyDefeated);
                if (enemy == null) continue;

                roomObjects.Add(enemy.gameObject);
                activeRoomThreats++;
            }

            if (activeRoomThreats == 0) roomChallengeLocked = false;
        }

        private void BuildRoomHazards(Vector2Int room)
        {
            WorldRoomHazardTheme? hazardTheme = GetRoomType(room) switch
            {
                WorldRoomType.CoverCombat => WorldRoomHazardTheme.SawGrid,
                WorldRoomType.PatternCombat => WorldRoomHazardTheme.MovingLasers,
                WorldRoomType.ConvergenceCombat => WorldRoomHazardTheme.Hybrid,
                _ => null
            };
            if (!hazardTheme.HasValue) return;

            Transform target = playerBody != null
                ? playerBody.transform
                : playerActor != null ? playerActor.transform : null;
            WorldRoomHazardController hazards = WorldRoomHazardController.CreateRuntime(
                hazardTheme.Value, target, transform, StableHash(room, 41, 19));
            if (hazards != null) roomObjects.Add(hazards.gameObject);
        }

        private static int GetCombatEnemyCount(Vector2Int room)
        {
            if (room == new Vector2Int(0, 1)) return 2;
            if (room == new Vector2Int(-1, 1)) return 3;
            if (room == new Vector2Int(1, 2)) return 4;
            if (room == new Vector2Int(0, 2)) return 5;
            return 3;
        }

        private static WorldEnemyPattern GetEnemyPattern(Vector2Int room, int index)
        {
            if (room == new Vector2Int(0, 1))
                return index == 0 ? WorldEnemyPattern.Chaser : WorldEnemyPattern.Shooter;
            if (room == new Vector2Int(-1, 1))
                return index == 0 ? WorldEnemyPattern.Shooter : WorldEnemyPattern.Chaser;
            if (room == new Vector2Int(1, 2))
                return index % 3 == 0 ? WorldEnemyPattern.Shooter
                    : index % 3 == 1 ? WorldEnemyPattern.Charger
                    : WorldEnemyPattern.Chaser;
            if (room == new Vector2Int(0, 2))
                return index % 4 == 0 ? WorldEnemyPattern.Shooter
                    : index % 4 == 1 ? WorldEnemyPattern.Charger
                    : WorldEnemyPattern.Chaser;

            return index % 2 == 0 ? WorldEnemyPattern.Chaser : WorldEnemyPattern.Shooter;
        }

        private static float GetEnemyHealth(Vector2Int room, int index)
        {
            float baseHealth = GetRoomType(room) switch
            {
                WorldRoomType.ShootingTutorial => 54f,
                WorldRoomType.CoverCombat => 68f,
                WorldRoomType.PatternCombat => 84f,
                WorldRoomType.ConvergenceCombat => 76f,
                _ => 64f
            };
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

        private static DestructibleRewardType GetDestructibleRewardType(Vector2Int room, int index)
        {
            return (StableHash(room, 73 + index, 29) % 3) switch
            {
                0 => DestructibleRewardType.Health,
                1 => DestructibleRewardType.Mana,
                _ => DestructibleRewardType.Experience
            };
        }

        private static float GetDestructibleRewardAmount(Vector2Int room, int index,
            DestructibleRewardType rewardType)
        {
            RoomProfile profile = GetRoomProfile(room);
            return rewardType switch
            {
                DestructibleRewardType.Health => 30f + Mathf.Max(0f, profile.HealthBonus) * 0.35f,
                DestructibleRewardType.Experience => 24f + room.y * 5f + index * 4f,
                _ => Mathf.Max(8f, profile.ManaReward + index * 1.5f)
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
            if (room == new Vector2Int(0, 1)) return WorldRoomType.ShootingTutorial;
            if (room == new Vector2Int(-1, 1)) return WorldRoomType.CoverCombat;
            if (room == new Vector2Int(1, 2)) return WorldRoomType.PatternCombat;
            if (room == new Vector2Int(0, 2)) return WorldRoomType.ConvergenceCombat;
            return WorldRoomType.Combat;
        }

        private static bool IsCombatRoom(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Combat => true,
                WorldRoomType.ShootingTutorial => true,
                WorldRoomType.CoverCombat => true,
                WorldRoomType.PatternCombat => true,
                WorldRoomType.ConvergenceCombat => true,
                _ => false
            };
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
                WorldRoomType.ShootingTutorial => new RoomProfile(0, -16f, 6f),
                WorldRoomType.CoverCombat => new RoomProfile(2, 6f, 8f),
                WorldRoomType.Combat => new RoomProfile(1, 10f, 8f),
                WorldRoomType.PuzzleSequence => new RoomProfile(-1, 0f, 10f),
                WorldRoomType.PuzzleCircuit => new RoomProfile(-1, 0f, 10f),
                WorldRoomType.PatternCombat => new RoomProfile(1, 18f, 11f),
                WorldRoomType.ConvergenceCombat => new RoomProfile(2, 24f, 13f),
                WorldRoomType.BossGateway => new RoomProfile(-2, 0f, 12f),
                _ => new RoomProfile(0, 0f, 8f)
            };
        }

        private static string GetRoomDisplayName(Vector2Int room, bool spanish)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Start => spanish ? "CAMPAMENTO" : "CAMP",
                WorldRoomType.ShootingTutorial => spanish ? "PRACTICA DE TIRO" : "AIM PRACTICE",
                WorldRoomType.CoverCombat => spanish ? "COBERTURA" : "COVER",
                WorldRoomType.Combat => spanish ? "COMBATE" : "FIGHT",
                WorldRoomType.PuzzleSequence => spanish ? "ORDEN" : "SEQUENCE",
                WorldRoomType.PuzzleCircuit => spanish ? "CIRCUITO" : "CIRCUIT",
                WorldRoomType.PatternCombat => spanish ? "PATRONES" : "PATTERNS",
                WorldRoomType.ConvergenceCombat => spanish ? "COMBINACION" : "COMBINATION",
                WorldRoomType.BossGateway => spanish ? "UMBRAL BOSS" : "BOSS GATE",
                _ => spanish ? "SALA" : "ROOM"
            };
        }

        private static Color GetRoomMapColor(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.ShootingTutorial => new Color(0.88f, 0.3f, 0.1f, 1f),
                WorldRoomType.CoverCombat => new Color(0.68f, 0.56f, 0.12f, 1f),
                WorldRoomType.Combat => new Color(0.78f, 0.18f, 0.08f, 1f),
                WorldRoomType.PuzzleSequence => new Color(0.18f, 0.58f, 0.36f, 1f),
                WorldRoomType.PuzzleCircuit => new Color(0.12f, 0.52f, 0.72f, 1f),
                WorldRoomType.PatternCombat => new Color(0.68f, 0.18f, 0.32f, 1f),
                WorldRoomType.ConvergenceCombat => new Color(0.52f, 0.28f, 0.72f, 1f),
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

        private void PaintDungeonDetails(Vector2Int room)
        {
            if (details == null || backgroundTile == null) return;

            int layout = StableHash(room, 101, 47) % 4;
            switch (layout)
            {
                case 0:
                    PaintVoidPatch(new Vector3Int(-11, 7, 0), 6, 3);
                    PaintRockShelf(new Vector3Int(7, -8, 0), true, 5);
                    break;
                case 1:
                    PaintVoidPatch(new Vector3Int(10, -6, 0), 5, 4);
                    PaintRockShelf(new Vector3Int(-9, 8, 0), true, 6);
                    break;
                case 2:
                    PaintVoidPatch(new Vector3Int(-10, -6, 0), 5, 4);
                    PaintRockShelf(new Vector3Int(8, 8, 0), false, 5);
                    break;
                default:
                    PaintVoidPatch(new Vector3Int(10, 7, 0), 6, 3);
                    PaintRockShelf(new Vector3Int(-9, -8, 0), true, 6);
                    break;
            }
        }

        private void PaintVoidPatch(Vector3Int center, int width, int height)
        {
            int halfWidth = Mathf.Max(1, width / 2);
            int halfHeight = Mathf.Max(1, height / 2);
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                for (int y = -halfHeight; y <= halfHeight; y++)
                {
                    float normalizedX = x / (float)(halfWidth + 0.5f);
                    float normalizedY = y / (float)(halfHeight + 0.5f);
                    if (normalizedX * normalizedX + normalizedY * normalizedY > 1.05f) continue;
                    details.SetTile(center + new Vector3Int(x, y, 0), backgroundTile);
                }
            }
        }

        private void PaintRockShelf(Vector3Int start, bool horizontal, int length)
        {
            if (alternatePathTile == null) return;
            for (int index = 0; index < length; index++)
            {
                Vector3Int offset = horizontal
                    ? new Vector3Int(index, 0, 0)
                    : new Vector3Int(0, index, 0);
                details.SetTile(start + offset, alternatePathTile);
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
                if (decoration == null) continue;

                details.SetTile(positions[index], decoration);
                details.SetTileFlags(positions[index], TileFlags.None);
                details.SetTransformMatrix(positions[index],
                    Matrix4x4.Scale(Vector3.one * MapDecorationScaleMultiplier));
            }
        }

        private void BuildRoomPresentation(Vector2Int room)
        {
            RoomVisualPalette palette = GetRoomVisualPalette(room);
            RoomOpenings openings = GetOpenings(room);

            BuildDungeonSetPieces(room, palette);
            if (!showRoomGuides) return;

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
            BuildRoomMechanicPresentation(room, palette);
        }

        private void BuildDungeonSetPieces(Vector2Int room, RoomVisualPalette palette)
        {
            BuildWaterFeature(room);
            BuildMineRail(room);
            BuildAmbientProps(room);
            BuildTorches(room, palette);
            int roomLayout = StableHash(room, 113, 23) % 5;
            BuildRoomArchitecture(room, roomLayout);

            int entranceLayout = StableHash(room, 61, 13) % 3;
            if (mineEntranceSprite != null && (room == StartRoom || room == BossGatewayRoom || entranceLayout == 0))
            {
                CreatePixelProp("Mine Entrance", mineEntranceSprite,
                    new Vector2(-12.6f, 8.3f), 4.8f, 4);
            }

            if (mineEntranceAltSprite != null && (room == BossGatewayRoom || entranceLayout == 1))
            {
                CreatePixelProp("Mine Entrance Alternate", mineEntranceAltSprite,
                    new Vector2(12.4f, 7.8f), 4.8f, 4);
            }
        }

        private void BuildRoomArchitecture(Vector2Int room, int layout)
        {
            WorldRoomType roomType = GetRoomType(room);
            switch (roomType)
            {
                case WorldRoomType.Start:
                    CreateRoomSprite("Camp Shelter", RuntimeCaveArt.Camp,
                        new Vector2(-9.8f, -6.5f), 4);
                    CreateRoomSprite("Campfire", RuntimeCaveArt.Campfire,
                        new Vector2(-4.2f, -6.2f), 4);
                    BuildPropCluster(new Vector2(-12.1f, -7.5f), StableHash(room, 127, 5), 3, 4.4f);
                    break;
                case WorldRoomType.PuzzleSequence:
                    CreateRoomSprite("Water Crossing Bridge", RuntimeCaveArt.Bridge,
                        GetWaterPosition(room) + new Vector2(0f, 0.15f), 4);
                    CreateRoomSprite("Sequence Crystals", RuntimeCaveArt.CrystalCluster,
                        new Vector2(11.2f, -6.2f), 4, 0f, 0.82f);
                    BuildPropCluster(new Vector2(11.4f, -7.8f), StableHash(room, 127, 11), 3, 4.2f);
                    break;
                case WorldRoomType.PuzzleCircuit:
                    CreateRoomSprite("Mine Workshop", RuntimeCaveArt.Workshop,
                        new Vector2(-10.2f, 6.4f), 4);
                    CreateRoomSprite("Circuit Crystals", RuntimeCaveArt.CrystalCluster,
                        new Vector2(-10.8f, -6.3f), 4, 0f, 0.74f);
                    BuildPropCluster(new Vector2(-12.0f, 7.8f), StableHash(room, 127, 17), 3, 4.0f);
                    break;
                case WorldRoomType.BossGateway:
                    CreateRoomSprite("Boss Gate Structure", RuntimeCaveArt.BossGate,
                        new Vector2(0f, 8.25f), 4);
                    CreateRoomSprite("Gateway Fire Left", RuntimeCaveArt.Campfire,
                        new Vector2(-5.7f, 7.0f), 4, 0f, 0.8f);
                    CreateRoomSprite("Gateway Fire Right", RuntimeCaveArt.Campfire,
                        new Vector2(5.7f, 7.0f), 4, 0f, 0.8f);
                    break;
                default:
                    Vector2 outcropPosition = layout % 2 == 0
                        ? new Vector2(-12.0f, 7.0f)
                        : new Vector2(11.8f, -6.8f);
                    CreateRoomSprite("Rock Outcrop", RuntimeCaveArt.RockOutcrop,
                        outcropPosition, 2, 0f, 0.82f);
                    CreateRoomSprite("Cave Crystal Cluster", RuntimeCaveArt.CrystalCluster,
                        layout % 2 == 0 ? new Vector2(11.3f, 6.3f) : new Vector2(-11.0f, -6.4f),
                        4, 0f, 0.7f);
                    BuildPropCluster(layout % 2 == 0
                        ? new Vector2(11.2f, -7.9f)
                        : new Vector2(-11.8f, 7.8f), StableHash(room, 127, 29), 3, 4.1f);
                    break;
            }

            BuildMineStructures(room, layout);
        }

        private void BuildMineStructures(Vector2Int room, int layout)
        {
            Vector2 beamPosition = layout % 2 == 0
                ? new Vector2(-7.4f, 9.0f)
                : new Vector2(7.5f, -8.8f);
            if (mineBeamSprite != null)
            {
                CreatePixelProp("Timber Beam", mineBeamSprite, beamPosition, 3.9f, 4);
            }

            if (mineCartSprite != null && (layout == 1 || room == StartRoom || room == BossGatewayRoom))
            {
                Vector2 cartPosition = layout % 2 == 0
                    ? new Vector2(4.6f, -7.6f)
                    : new Vector2(-4.8f, 7.0f);
                CreatePixelProp("Mine Cart", mineCartSprite, cartPosition, 3.4f, 4);
            }

            if (mineLadderSprite != null && (layout == 2 || room == BossGatewayRoom))
            {
                Vector2 ladderPosition = layout % 2 == 0
                    ? new Vector2(13.1f, 4.5f)
                    : new Vector2(-13.2f, -4.5f);
                CreatePixelProp("Mine Ladder", mineLadderSprite, ladderPosition, 3.6f, 4);
            }
        }

        private void BuildPropCluster(Vector2 center, int seed, int count, float scale)
        {
            if (ambientPropSprites == null || ambientPropSprites.Length == 0) return;
            Vector2[] offsets =
            {
                new(-0.9f, 0f), new(0.1f, 0.35f), new(1.0f, -0.05f),
                new(-0.35f, -0.55f), new(0.75f, 0.55f)
            };
            for (int index = 0; index < Mathf.Min(count, offsets.Length); index++)
            {
                int spriteIndex = Mathf.Abs(seed + index * 5) % ambientPropSprites.Length;
                Sprite prop = ambientPropSprites[spriteIndex];
                if (prop == null) continue;
                CreatePixelProp($"Cave Prop Cluster {index}", prop,
                    center + offsets[index], scale, 4);
            }
        }

        private void BuildWaterFeature(Vector2Int room)
        {
            if (room == BossGatewayRoom || room == BossRoom) return;
            Vector2 position = GetWaterPosition(room);
            CreateRoomSprite("Cave Water Pool", RuntimeCaveArt.WaterPool, position, 2);
        }

        private static Vector2 GetWaterPosition(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Start => new Vector2(-8.4f, 6.4f),
                WorldRoomType.ShootingTutorial => new Vector2(10.2f, 5.8f),
                WorldRoomType.CoverCombat => new Vector2(-9.8f, -5.8f),
                WorldRoomType.PuzzleSequence => new Vector2(8.2f, -5.4f),
                WorldRoomType.PuzzleCircuit => new Vector2(-8.5f, 5.4f),
                WorldRoomType.PatternCombat => new Vector2(10.2f, 6.2f),
                WorldRoomType.ConvergenceCombat => new Vector2(-9.8f, -6.1f),
                _ => new Vector2(9.6f, -5.7f)
            };
        }

        private void BuildMineRail(Vector2Int room)
        {
            int layout = StableHash(room, 71, 29) % 3;
            if (layout == 2 && room != StartRoom && room != BossGatewayRoom) return;

            Vector2 position = layout == 1
                ? new Vector2(-5.5f, -7.2f)
                : new Vector2(4.2f, -7.5f);
            CreateRoomSprite("Mine Rail", RuntimeCaveArt.RailHorizontal, position, 2);
            if (layout == 0 || room == StartRoom)
            {
                CreateRoomSprite("Mine Rail Turn", RuntimeCaveArt.RailHorizontal,
                    position + new Vector2(-7.5f, 3.2f), 2, 90f);
            }
        }

        private void BuildAmbientProps(Vector2Int room)
        {
            if (ambientPropSprites == null || ambientPropSprites.Length == 0) return;

            Vector2[] positions =
            {
                new(-14.1f, 8.2f), new(14.0f, 8.0f),
                new(-14.2f, -8.3f), new(14.1f, -8.0f),
                new(-7.3f, 9.4f), new(7.5f, -9.3f),
                new(-15.0f, 1.9f), new(15.0f, -1.7f),
                new(-5.7f, -9.1f), new(5.8f, 9.1f)
            };
            int seed = StableHash(room, 83, 37);
            for (int index = 0; index < positions.Length; index++)
            {
                Sprite prop = ambientPropSprites[(seed + index * 7) % ambientPropSprites.Length];
                if (prop == null) continue;
                CreatePixelProp($"Cave Prop {index}", prop, positions[index], 6.25f, 4);
            }
        }

        private void BuildTorches(Vector2Int room, RoomVisualPalette palette)
        {
            // The torch sprite already contains its warm pixel palette; keep it un-tinted.
            Color torchColor = Color.white;
            Vector2[] positions =
            {
                new(-15.4f, 5.8f), new(15.4f, 5.8f),
                new(-15.4f, -5.8f), new(15.4f, -5.8f)
            };
            int first = StableHash(room, 97, 11) % positions.Length;
            for (int index = 0; index < 2; index++)
            {
                GameObject torch = CreateRoomSprite($"Cave Torch {index}", RuntimeCaveArt.Torch,
                    positions[(first + index) % positions.Length], 4);
                SpriteRenderer renderer = torch != null ? torch.GetComponent<SpriteRenderer>() : null;
                if (renderer != null) renderer.color = torchColor;
            }
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

        private void BuildRoomMechanicPresentation(Vector2Int room, RoomVisualPalette palette)
        {
            Color accent = WithAlpha(palette.Accent, 0.2f);
            Color warm = WithAlpha(palette.Warm, 0.26f);

            switch (GetRoomType(room))
            {
                case WorldRoomType.ShootingTutorial:
                    CreateRoomVisual("Aim Practice Lane", new Vector2(0f, 0f),
                        new Vector2(24f, 0.1f), accent, 2);
                    BuildLessonMarker("Aim Target Left", new Vector2(-8f, 0f), warm);
                    BuildLessonMarker("Aim Target Center", new Vector2(0f, 0f), warm);
                    BuildLessonMarker("Aim Target Right", new Vector2(8f, 0f), warm);
                    break;
                case WorldRoomType.CoverCombat:
                    CreateRoomVisual("Cover Lane Upper", new Vector2(0f, 4.8f),
                        new Vector2(23f, 0.08f), accent, 2);
                    CreateRoomVisual("Cover Lane Lower", new Vector2(0f, -4.8f),
                        new Vector2(23f, 0.08f), accent, 2);
                    BuildLessonMarker("Cover Marker", new Vector2(0f, 0f), warm);
                    break;
                case WorldRoomType.PuzzleSequence:
                    CreateRoomVisual("Sequence Route Left", new Vector2(-3.5f, 2.6f),
                        new Vector2(7.2f, 0.08f), accent, 2);
                    CreateRoomVisual("Sequence Route Right", new Vector2(3.5f, 2.6f),
                        new Vector2(7.2f, 0.08f), accent, 2);
                    CreateRoomVisual("Sequence Route Down", new Vector2(0f, -1.3f),
                        new Vector2(0.08f, 3.2f), accent, 2);
                    break;
                case WorldRoomType.PuzzleCircuit:
                    CreateRoomVisual("Circuit Spine", new Vector2(0f, 0f),
                        new Vector2(18f, 0.08f), accent, 2);
                    CreateRoomVisual("Circuit Branch", new Vector2(0f, 2.2f),
                        new Vector2(0.08f, 4.4f), warm, 2);
                    break;
                case WorldRoomType.PatternCombat:
                    CreateRoomVisual("Pattern Warning Upper", new Vector2(0f, 4.1f),
                        new Vector2(25f, 0.12f), warm, 2);
                    CreateRoomVisual("Pattern Warning Lower", new Vector2(0f, -4.1f),
                        new Vector2(25f, 0.12f), warm, 2);
                    CreateRoomVisual("Pattern Warning Left", new Vector2(-8.2f, 0f),
                        new Vector2(0.12f, 8.2f), accent, 2);
                    CreateRoomVisual("Pattern Warning Right", new Vector2(8.2f, 0f),
                        new Vector2(0.12f, 8.2f), accent, 2);
                    break;
                case WorldRoomType.ConvergenceCombat:
                    CreateRoomVisual("Build Convergence Horizontal", Vector2.zero,
                        new Vector2(11f, 0.12f), accent, 2);
                    CreateRoomVisual("Build Convergence Vertical", Vector2.zero,
                        new Vector2(0.12f, 11f), accent, 2);
                    BuildLessonMarker("Build Convergence Core", Vector2.zero, warm);
                    break;
                case WorldRoomType.BossGateway:
                    CreateRoomVisual("Boss Approach Left", new Vector2(-4.4f, 4.2f),
                        new Vector2(0.12f, 7.4f), accent, 2);
                    CreateRoomVisual("Boss Approach Right", new Vector2(4.4f, 4.2f),
                        new Vector2(0.12f, 7.4f), accent, 2);
                    CreateRoomVisual("Boss Approach Threshold", new Vector2(0f, 8.1f),
                        new Vector2(8.8f, 0.12f), warm, 2);
                    break;
            }
        }

        private void BuildLessonMarker(string objectName, Vector2 position, Color color)
        {
            CreateRoomVisual($"{objectName} Horizontal", position, new Vector2(2.2f, 0.1f), color, 2);
            CreateRoomVisual($"{objectName} Vertical", position, new Vector2(0.1f, 2.2f), color, 2);
            CreateRoomVisual($"{objectName} Core", position, new Vector2(0.28f, 0.28f),
                Color.white, 2);
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

        private GameObject CreateRoomSprite(string objectName, Sprite sprite, Vector2 center,
            int sortingOrder, float rotation = 0f, float scale = 1f)
        {
            if (sprite == null) return null;
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform, false);
            visual.transform.position = new Vector3(center.x, center.y, 0f);
            visual.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            float finalScale = Mathf.Max(0.01f, scale) * MapDecorationScaleMultiplier;
            visual.transform.localScale = Vector3.one * finalScale;

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            roomObjects.Add(visual);
            return visual;
        }

        private GameObject CreatePixelProp(string objectName, Sprite sprite, Vector2 center,
            float scale, int sortingOrder)
        {
            GameObject visual = CreateRoomSprite(objectName, sprite, center, sortingOrder, 0f, scale);
            if (visual == null) return null;

            float finalScale = Mathf.Max(0.01f, scale) * MapDecorationScaleMultiplier;
            Vector2 pivotOffset = sprite.bounds.center * finalScale;
            visual.transform.position = new Vector3(
                center.x - pivotOffset.x,
                center.y - pivotOffset.y,
                0f);
            return visual;
        }

        private static RoomVisualPalette GetRoomVisualPalette(Vector2Int room)
        {
            switch (GetRoomType(room))
            {
                case WorldRoomType.ShootingTutorial:
                    return new RoomVisualPalette(
                        new Color(0.1f, 0.78f, 0.92f, 1f),
                        new Color(1f, 0.42f, 0.12f, 1f),
                        new Color(0.02f, 0.06f, 0.1f, 1f));
                case WorldRoomType.CoverCombat:
                    return new RoomVisualPalette(
                        new Color(0.28f, 0.82f, 0.48f, 1f),
                        new Color(0.96f, 0.76f, 0.18f, 1f),
                        new Color(0.03f, 0.09f, 0.05f, 1f));
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
                case WorldRoomType.PatternCombat:
                    return new RoomVisualPalette(
                        new Color(0.94f, 0.18f, 0.34f, 1f),
                        new Color(1f, 0.6f, 0.16f, 1f),
                        new Color(0.11f, 0.02f, 0.05f, 1f));
                case WorldRoomType.ConvergenceCombat:
                    return new RoomVisualPalette(
                        new Color(0.42f, 0.48f, 1f, 1f),
                        new Color(0.98f, 0.34f, 0.16f, 1f),
                        new Color(0.06f, 0.035f, 0.12f, 1f));
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
            wallMap.gameObject.tag = "Wall";
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
            objectiveHud = CreatePanel("Room HUD", canvasRect, new Vector2(0.24f, 0.905f),
                new Vector2(0.76f, 0.985f), new Color(0.09f, 0.025f, 0.018f, 0.76f));
            objectiveHudGroup = objectiveHud.AddComponent<CanvasGroup>();
            objectiveHudGroup.alpha = 0f;
            roomLabel = CreateText("Room Text", objectiveHud.transform as RectTransform,
                new Vector2(0.035f, 0.58f), new Vector2(0.965f, 0.96f), string.Empty, 16f,
                new Color(1f, 0.88f, 0.68f), TextAlignmentOptions.Center);
            lessonLabel = CreateText("Room Lesson", objectiveHud.transform as RectTransform,
                new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.55f), string.Empty, 10f,
                new Color(0.96f, 0.7f, 0.42f), TextAlignmentOptions.Center);
            objectiveHud.SetActive(false);

            GameObject deathPanel = CreatePanel("Death Counter", canvasRect,
                new Vector2(0.018f, 0.935f), new Vector2(0.16f, 0.985f),
                new Color(0.09f, 0.025f, 0.018f, 0.72f));
            deathCounter = CreateText("Death Counter Text", deathPanel.transform as RectTransform,
                new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), string.Empty, 15f,
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
                ? "Aprende una mecanica por sala y elige entre dos rutas antes del boss."
                : "Learn one mechanic per room and choose between two routes before the boss.";
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
                new Vector2(0.83f, 0.72f), new Vector2(0.985f, 0.985f),
                new Color(0.055f, 0.018f, 0.012f, 0.78f));
            mapPanel = panel.transform as RectTransform;

            CreateText("Map Title", mapPanel, new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.98f),
                GameLoadout.IsSpanish ? "MAPA  ·  M" : "MAP  ·  M", 15f,
                new Color(1f, 0.78f, 0.48f), TextAlignmentOptions.Center);
            CreateText("Map Legend", mapPanel, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.16f),
                GameLoadout.IsSpanish ? "F TIRO  C COBERTURA\nA/E PUZLE  P PATRON" : "F AIM  C COVER\nA/E PUZZLE  P PATTERN", 8f,
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
            float cellHalfSize = room == BossRoom ? 0.08f : 0.05f;
            cell.anchorMin = center - Vector2.one * cellHalfSize;
            cell.anchorMax = center + Vector2.one * cellHalfSize;
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = Vector2.zero;

            Image image = cellObject.GetComponent<Image>();
            image.color = new Color(0.025f, 0.012f, 0.008f, 0.96f);
            image.raycastTarget = false;
            mapCells.Add(room, image);

            TextMeshProUGUI label = CreateText("Room State", cell, Vector2.zero, Vector2.one,
                room == BossRoom ? "BOSS" : GetRoomMapSymbol(room), room == BossRoom ? 11f : 13f,
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
                WorldRoomType.ShootingTutorial => "F",
                WorldRoomType.CoverCombat => "C",
                WorldRoomType.Combat => "!",
                WorldRoomType.PuzzleSequence => "A",
                WorldRoomType.PuzzleCircuit => "E",
                WorldRoomType.PatternCombat => "P",
                WorldRoomType.ConvergenceCombat => "+",
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
                mapPanel.anchorMin = new Vector2(0.83f, 0.72f);
                mapPanel.anchorMax = new Vector2(0.985f, 0.985f);
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
            transitionLabel = CreateText("Transition Label", rect, new Vector2(0.16f, 0.37f),
                new Vector2(0.84f, 0.63f), string.Empty, 32f,
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
            objectiveHudVisibleUntil = Time.unscaledTime + 4.5f;
            int roomNumber = GetRoomNumber(currentRoom);
            bool spanish = GameLoadout.IsSpanish;
            string roomName = GetRoomDisplayName(currentRoom, spanish);
            RoomLesson lesson = GetRoomLesson(currentRoom);
            roomLabel.text = spanish
                ? $"SALA {roomNumber:00}  ·  {roomName}  ·  {visitedRooms.Count:00}/{RouteRooms.Length:00}"
                : $"ROOM {roomNumber:00}  ·  {roomName}  ·  {visitedRooms.Count:00}/{RouteRooms.Length:00}";
            if (lessonLabel != null)
            {
                lessonLabel.text = spanish
                    ? $"APRENDE: {lesson.SpanishMechanic}\nOBJETIVO: {lesson.SpanishObjective}"
                    : $"LEARN: {lesson.EnglishMechanic}\nOBJECTIVE: {lesson.EnglishObjective}";
            }
            UpdateMap();
        }

        private void UpdateRoomHudVisibility()
        {
            if (objectiveHudGroup == null || objectiveHud == null || !objectiveHud.activeSelf) return;
            float targetAlpha = Time.unscaledTime < objectiveHudVisibleUntil ? 1f : 0.22f;
            objectiveHudGroup.alpha = Mathf.MoveTowards(objectiveHudGroup.alpha, targetAlpha,
                2.8f * Time.unscaledDeltaTime);
        }

        private static RoomLesson GetRoomLesson(Vector2Int room)
        {
            return GetRoomType(room) switch
            {
                WorldRoomType.Start => new RoomLesson(
                    "MOVIMIENTO Y MAPA", "llega al pasillo y observa las dos rutas",
                    "MOVEMENT AND MAP", "reach the hall and read both routes"),
                WorldRoomType.ShootingTutorial => new RoomLesson(
                    "DISPARO Y APUNTADO", "limpia la sala y elige una ruta",
                    "AIM AND SHOOT", "clear the room and choose a route"),
                WorldRoomType.CoverCombat => new RoomLesson(
                    "COBERTURA DESTRUCTIBLE", "rompe cajas y usa muros para abrir angulos",
                    "DESTRUCTIBLE COVER", "break crates and use walls to open angles"),
                WorldRoomType.PuzzleSequence => new RoomLesson(
                    "ORDEN DE COLORES", "activa los terminales con E en el orden mostrado",
                    "COLOR SEQUENCE", "activate terminals with E in the shown order"),
                WorldRoomType.PuzzleCircuit => new RoomLesson(
                    "CIRCUITO Y POSICION", "conecta los nodos y controla tu espacio",
                    "CIRCUIT AND POSITION", "connect the nodes and control your space"),
                WorldRoomType.PatternCombat => new RoomLesson(
                    "PATRONES DE ENEMIGOS", "lee las senales, esquiva y contraataca",
                    "ENEMY PATTERNS", "read the signals, dodge and counterattack"),
                WorldRoomType.ConvergenceCombat => new RoomLesson(
                    "COMBINAR HABILIDADES", "limpia la arena y prueba tu build",
                    "COMBINE ABILITIES", "clear the arena and test your build"),
                WorldRoomType.BossGateway => new RoomLesson(
                    "CHECKPOINT Y COFRE", "preparate, recoge la mejora y entra al boss",
                    "CHECKPOINT AND CHEST", "prepare, claim the upgrade and enter the boss"),
                _ => new RoomLesson(
                    "COMBATE", "derrota a los enemigos para abrir las salidas",
                    "COMBAT", "defeat enemies to open the exits")
            };
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
            ShootingTutorial,
            CoverCombat,
            PuzzleSequence,
            PuzzleCircuit,
            PatternCombat,
            ConvergenceCombat,
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

        private readonly struct RoomLesson
        {
            public RoomLesson(string spanishMechanic, string spanishObjective,
                string englishMechanic, string englishObjective)
            {
                SpanishMechanic = spanishMechanic;
                SpanishObjective = spanishObjective;
                EnglishMechanic = englishMechanic;
                EnglishObjective = englishObjective;
            }

            public string SpanishMechanic { get; }
            public string SpanishObjective { get; }
            public string EnglishMechanic { get; }
            public string EnglishObjective { get; }
        }

        private void SetTransitionLabel(Vector2Int destination)
        {
            if (transitionLabel == null) return;

            if (destination == BossRoom)
            {
                transitionLabel.text = GameLoadout.IsSpanish
                    ? "SALA DEL JEFE\nCHECKPOINT ACTIVO"
                    : "BOSS ROOM\nCHECKPOINT ACTIVE";
                return;
            }

            int roomNumber = GetRoomNumber(destination);
            string roomName = GetRoomDisplayName(destination, GameLoadout.IsSpanish);
            RoomLesson lesson = GetRoomLesson(destination);
            transitionLabel.text = GameLoadout.IsSpanish
                ? $"SALA {roomNumber:00}  ·  {roomName}\n{lesson.SpanishMechanic}"
                : $"ROOM {roomNumber:00}  ·  {roomName}\n{lesson.EnglishMechanic}";
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

    internal static class RuntimeCaveArt
    {
        private static Sprite waterPool;
        private static Sprite railHorizontal;
        private static Sprite torch;
        private static Sprite camp;
        private static Sprite bridge;
        private static Sprite campfire;
        private static Sprite rockOutcrop;
        private static Sprite crystalCluster;
        private static Sprite workshop;
        private static Sprite bossGate;
        private static Texture2D waterTexture;
        private static Texture2D railTexture;
        private static Texture2D torchTexture;
        private static Texture2D campTexture;
        private static Texture2D bridgeTexture;
        private static Texture2D campfireTexture;
        private static Texture2D rockOutcropTexture;
        private static Texture2D crystalClusterTexture;
        private static Texture2D workshopTexture;
        private static Texture2D bossGateTexture;

        public static Sprite WaterPool => waterPool != null ? waterPool : waterPool = BuildWaterPool();
        public static Sprite RailHorizontal => railHorizontal != null
            ? railHorizontal
            : railHorizontal = BuildRailHorizontal();
        public static Sprite Torch => torch != null ? torch : torch = BuildTorch();
        public static Sprite Camp => camp != null ? camp : camp = BuildCamp();
        public static Sprite Bridge => bridge != null ? bridge : bridge = BuildBridge();
        public static Sprite Campfire => campfire != null ? campfire : campfire = BuildCampfire();
        public static Sprite RockOutcrop => rockOutcrop != null
            ? rockOutcrop
            : rockOutcrop = BuildRockOutcrop();
        public static Sprite CrystalCluster => crystalCluster != null
            ? crystalCluster
            : crystalCluster = BuildCrystalCluster();
        public static Sprite Workshop => workshop != null ? workshop : workshop = BuildWorkshop();
        public static Sprite BossGate => bossGate != null ? bossGate : bossGate = BuildBossGate();

        public static void Release()
        {
            if (waterPool != null) Object.Destroy(waterPool);
            if (railHorizontal != null) Object.Destroy(railHorizontal);
            if (torch != null) Object.Destroy(torch);
            if (camp != null) Object.Destroy(camp);
            if (bridge != null) Object.Destroy(bridge);
            if (campfire != null) Object.Destroy(campfire);
            if (rockOutcrop != null) Object.Destroy(rockOutcrop);
            if (crystalCluster != null) Object.Destroy(crystalCluster);
            if (workshop != null) Object.Destroy(workshop);
            if (bossGate != null) Object.Destroy(bossGate);
            if (waterTexture != null) Object.Destroy(waterTexture);
            if (railTexture != null) Object.Destroy(railTexture);
            if (torchTexture != null) Object.Destroy(torchTexture);
            if (campTexture != null) Object.Destroy(campTexture);
            if (bridgeTexture != null) Object.Destroy(bridgeTexture);
            if (campfireTexture != null) Object.Destroy(campfireTexture);
            if (rockOutcropTexture != null) Object.Destroy(rockOutcropTexture);
            if (crystalClusterTexture != null) Object.Destroy(crystalClusterTexture);
            if (workshopTexture != null) Object.Destroy(workshopTexture);
            if (bossGateTexture != null) Object.Destroy(bossGateTexture);
            waterPool = null;
            railHorizontal = null;
            torch = null;
            camp = null;
            bridge = null;
            campfire = null;
            rockOutcrop = null;
            crystalCluster = null;
            workshop = null;
            bossGate = null;
            waterTexture = null;
            railTexture = null;
            torchTexture = null;
            campTexture = null;
            bridgeTexture = null;
            campfireTexture = null;
            rockOutcropTexture = null;
            crystalClusterTexture = null;
            workshopTexture = null;
            bossGateTexture = null;
        }

        private static Sprite BuildWaterPool()
        {
            const int blockSize = 8;
            string[] mask =
            {
                "   ########   ",
                "  ##########  ",
                "  ########### ",
                "##############",
                "##############",
                "  ########### ",
                "  ##########  ",
                "    ######    "
            };
            int width = mask[0].Length * blockSize;
            int height = mask.Length * blockSize;
            waterTexture = CreateTexture("Runtime Cave Water", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cellX = x / blockSize;
                    int cellY = mask.Length - 1 - y / blockSize;
                    if (!IsActive(mask, cellX, cellY)) continue;

                    bool edge = (x % blockSize == 0 && !IsActive(mask, cellX - 1, cellY)) ||
                                (x % blockSize == blockSize - 1 && !IsActive(mask, cellX + 1, cellY)) ||
                                (y % blockSize == 0 && !IsActive(mask, cellX, cellY - 1)) ||
                                (y % blockSize == blockSize - 1 && !IsActive(mask, cellX, cellY + 1));
                    Color color = edge
                        ? new Color(0.08f, 0.7f, 0.94f, 1f)
                        : new Color(0.035f, 0.39f, 0.8f, 1f);

                    if (!edge && (cellX * 7 + cellY * 11) % 6 == 0 && y % blockSize < 2)
                        color = new Color(0.12f, 0.55f, 0.92f, 1f);
                    pixels[y * width + x] = color;
                }
            }

            waterTexture.SetPixels(pixels);
            waterTexture.Apply();
            return CreateSprite(waterTexture, "Runtime Cave Water Sprite");
        }

        private static Sprite BuildRailHorizontal()
        {
            const int width = 176;
            const int height = 24;
            railTexture = CreateTexture("Runtime Mine Rail", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);

            for (int y = 3; y <= 20; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = new Color(0f, 0f, 0f, 0f);
                    if (y is 4 or 5 or 18 or 19)
                        color = new Color(0.56f, 0.6f, 0.64f, 1f);
                    else if (y is 6 or 17)
                        color = new Color(0.18f, 0.2f, 0.25f, 1f);
                    else if (x % 20 >= 4 && x % 20 <= 8)
                        color = new Color(0.43f, 0.22f, 0.12f, 1f);
                    else if (y is 7 or 16)
                        color = new Color(0.27f, 0.12f, 0.08f, 1f);
                    pixels[y * width + x] = color;
                }
            }

            railTexture.SetPixels(pixels);
            railTexture.Apply();
            return CreateSprite(railTexture, "Runtime Mine Rail Sprite");
        }

        private static Sprite BuildTorch()
        {
            const int width = 24;
            const int height = 32;
            torchTexture = CreateTexture("Runtime Cave Torch", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            for (int y = 5; y < 22; y++)
            {
                pixels[y * width + 11] = new Color(0.24f, 0.09f, 0.05f, 1f);
                pixels[y * width + 12] = new Color(0.42f, 0.17f, 0.08f, 1f);
            }

            for (int y = 20; y < 31; y++)
            {
                for (int x = 7; x < 17; x++)
                {
                    int distance = Mathf.Abs(x - 12) + Mathf.Abs(y - 25);
                    if (distance <= 7)
                        pixels[y * width + x] = new Color(1f, 0.26f, 0.04f, 1f);
                    if (distance <= 4)
                        pixels[y * width + x] = new Color(1f, 0.72f, 0.12f, 1f);
                    if (distance <= 2)
                        pixels[y * width + x] = new Color(1f, 0.96f, 0.55f, 1f);
                }
            }

            torchTexture.SetPixels(pixels);
            torchTexture.Apply();
            return CreateSprite(torchTexture, "Runtime Cave Torch Sprite");
        }

        private static Sprite BuildCamp()
        {
            const int width = 112;
            const int height = 72;
            campTexture = CreateTexture("Runtime Mine Camp", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            Color shadow = new(0.08f, 0.025f, 0.035f, 0.82f);
            Color woodDark = new(0.2f, 0.065f, 0.045f, 1f);
            Color wood = new(0.54f, 0.2f, 0.1f, 1f);
            Color woodLight = new(0.78f, 0.36f, 0.15f, 1f);
            Color roof = new(0.29f, 0.09f, 0.1f, 1f);

            FillRect(pixels, width, height, 7, 105, 4, 12, shadow);
            FillRect(pixels, width, height, 12, 100, 10, 17, woodDark);
            for (int x = 16; x < 98; x += 16)
                FillRect(pixels, width, height, x, x + 3, 12, 17, woodLight);

            FillRect(pixels, width, height, 17, 95, 48, 55, roof);
            FillRect(pixels, width, height, 23, 89, 55, 62, roof);
            FillRect(pixels, width, height, 31, 81, 62, 68, woodDark);
            FillRect(pixels, width, height, 21, 91, 49, 53, wood);
            FillRect(pixels, width, height, 27, 85, 55, 59, wood);
            FillRect(pixels, width, height, 35, 77, 61, 65, woodLight);

            FillRect(pixels, width, height, 20, 29, 17, 51, woodDark);
            FillRect(pixels, width, height, 83, 92, 17, 51, woodDark);
            FillRect(pixels, width, height, 25, 29, 21, 50, wood);
            FillRect(pixels, width, height, 83, 87, 21, 50, wood);
            FillRect(pixels, width, height, 32, 80, 17, 22, wood);
            FillRect(pixels, width, height, 37, 75, 22, 48, new(0.07f, 0.025f, 0.03f, 1f));
            FillRect(pixels, width, height, 41, 45, 29, 34, woodLight);
            FillRect(pixels, width, height, 67, 71, 29, 34, woodLight);
            return CreateSprite(campTexture, "Runtime Mine Camp Sprite");
        }

        private static Sprite BuildBridge()
        {
            const int width = 144;
            const int height = 48;
            bridgeTexture = CreateTexture("Runtime Water Bridge", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            Color shadow = new(0.06f, 0.02f, 0.025f, 0.78f);
            Color woodDark = new(0.2f, 0.07f, 0.045f, 1f);
            Color wood = new(0.55f, 0.23f, 0.11f, 1f);
            Color woodLight = new(0.8f, 0.39f, 0.16f, 1f);
            Color metal = new(0.38f, 0.42f, 0.45f, 1f);

            FillRect(pixels, width, height, 5, 139, 5, 12, shadow);
            FillRect(pixels, width, height, 7, 137, 10, 15, metal);
            FillRect(pixels, width, height, 7, 137, 33, 38, metal);
            FillRect(pixels, width, height, 10, 134, 15, 34, woodDark);
            FillRect(pixels, width, height, 12, 132, 18, 32, wood);
            for (int x = 14; x < 132; x += 18)
            {
                FillRect(pixels, width, height, x, x + 4, 16, 35, woodDark);
                FillRect(pixels, width, height, x + 1, x + 3, 19, 32, woodLight);
            }
            FillRect(pixels, width, height, 7, 13, 12, 37, woodDark);
            FillRect(pixels, width, height, 131, 137, 12, 37, woodDark);
            return CreateSprite(bridgeTexture, "Runtime Water Bridge Sprite");
        }

        private static Sprite BuildCampfire()
        {
            const int width = 48;
            const int height = 48;
            campfireTexture = CreateTexture("Runtime Campfire", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            Color shadow = new(0.08f, 0.02f, 0.02f, 0.78f);
            Color stone = new(0.34f, 0.28f, 0.27f, 1f);
            Color stoneLight = new(0.62f, 0.48f, 0.36f, 1f);
            Color ember = new(1f, 0.2f, 0.03f, 1f);
            Color flame = new(1f, 0.63f, 0.08f, 1f);
            Color flameCore = new(1f, 0.95f, 0.44f, 1f);

            FillRect(pixels, width, height, 4, 44, 5, 13, shadow);
            FillRect(pixels, width, height, 7, 15, 10, 18, stone);
            FillRect(pixels, width, height, 15, 23, 7, 15, stoneLight);
            FillRect(pixels, width, height, 24, 32, 6, 14, stone);
            FillRect(pixels, width, height, 33, 41, 9, 17, stoneLight);
            FillRect(pixels, width, height, 13, 36, 13, 17, new(0.28f, 0.08f, 0.04f, 1f));
            FillRect(pixels, width, height, 15, 34, 15, 18, ember);

            for (int y = 18; y < 38; y += 4)
            {
                int halfWidth = Mathf.Clamp((y - 14) / 3, 1, 6);
                FillRect(pixels, width, height, 24 - halfWidth, 25 + halfWidth, y, y + 4, flame);
            }
            FillRect(pixels, width, height, 22, 27, 22, 35, flameCore);
            FillRect(pixels, width, height, 20, 29, 27, 31, flameCore);
            return CreateSprite(campfireTexture, "Runtime Campfire Sprite");
        }

        private static Sprite BuildRockOutcrop()
        {
            const int blockSize = 8;
            string[] mask =
            {
                "     ####     ",
                "   ########   ",
                "  ##########  ",
                "  ########### ",
                "##############",
                "##############",
                " ###########  ",
                "  ##########  ",
                "   ########   ",
                "     ####     "
            };
            int width = mask[0].Length * blockSize;
            int height = mask.Length * blockSize;
            rockOutcropTexture = CreateTexture("Runtime Rock Outcrop", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cellX = x / blockSize;
                    int cellY = mask.Length - 1 - y / blockSize;
                    if (!IsActive(mask, cellX, cellY)) continue;

                    bool edge = !IsActive(mask, cellX - 1, cellY) ||
                                !IsActive(mask, cellX + 1, cellY) ||
                                !IsActive(mask, cellX, cellY - 1) ||
                                !IsActive(mask, cellX, cellY + 1);
                    Color color = edge
                        ? new Color(0.43f, 0.17f, 0.11f, 1f)
                        : new Color(0.13f, 0.04f, 0.07f, 1f);
                    if (!edge && (cellX * 5 + cellY * 9) % 7 == 0 && y % blockSize < 2)
                        color = new Color(0.28f, 0.08f, 0.09f, 1f);
                    pixels[y * width + x] = color;
                }
            }
            rockOutcropTexture.SetPixels(pixels);
            rockOutcropTexture.Apply();
            return CreateSprite(rockOutcropTexture, "Runtime Rock Outcrop Sprite");
        }

        private static Sprite BuildCrystalCluster()
        {
            const int width = 56;
            const int height = 56;
            crystalClusterTexture = CreateTexture("Runtime Crystal Cluster", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            Color shadow = new(0.07f, 0.02f, 0.08f, 0.82f);
            Color blueDark = new(0.04f, 0.28f, 0.62f, 1f);
            Color blue = new(0.08f, 0.68f, 0.92f, 1f);
            Color glint = new(0.57f, 0.96f, 1f, 1f);
            FillRect(pixels, width, height, 5, 51, 5, 13, shadow);
            FillRect(pixels, width, height, 11, 19, 12, 35, blueDark);
            FillRect(pixels, width, height, 12, 18, 17, 40, blue);
            FillRect(pixels, width, height, 14, 16, 25, 42, glint);
            FillRect(pixels, width, height, 22, 31, 12, 45, blueDark);
            FillRect(pixels, width, height, 23, 30, 18, 50, blue);
            FillRect(pixels, width, height, 25, 29, 28, 52, glint);
            FillRect(pixels, width, height, 34, 43, 11, 32, blueDark);
            FillRect(pixels, width, height, 35, 42, 15, 37, blue);
            FillRect(pixels, width, height, 37, 40, 22, 39, glint);
            FillRect(pixels, width, height, 8, 45, 9, 13, new(0.15f, 0.08f, 0.3f, 1f));
            return CreateSprite(crystalClusterTexture, "Runtime Crystal Cluster Sprite");
        }

        private static Sprite BuildWorkshop()
        {
            const int width = 112;
            const int height = 80;
            workshopTexture = CreateTexture("Runtime Mine Workshop", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            Color shadow = new(0.07f, 0.02f, 0.025f, 0.82f);
            Color woodDark = new(0.18f, 0.055f, 0.04f, 1f);
            Color wood = new(0.49f, 0.17f, 0.08f, 1f);
            Color woodLight = new(0.76f, 0.32f, 0.13f, 1f);
            Color metal = new(0.32f, 0.36f, 0.4f, 1f);

            FillRect(pixels, width, height, 6, 106, 5, 13, shadow);
            FillRect(pixels, width, height, 10, 102, 17, 24, woodDark);
            FillRect(pixels, width, height, 14, 98, 20, 27, wood);
            FillRect(pixels, width, height, 16, 96, 27, 33, woodLight);
            FillRect(pixels, width, height, 14, 22, 27, 69, woodDark);
            FillRect(pixels, width, height, 90, 98, 27, 69, woodDark);
            FillRect(pixels, width, height, 18, 22, 31, 67, wood);
            FillRect(pixels, width, height, 90, 94, 31, 67, wood);
            FillRect(pixels, width, height, 22, 90, 61, 68, woodDark);
            FillRect(pixels, width, height, 24, 88, 64, 67, woodLight);
            FillRect(pixels, width, height, 27, 47, 38, 57, metal);
            FillRect(pixels, width, height, 30, 44, 41, 53, new(0.08f, 0.2f, 0.28f, 1f));
            FillRect(pixels, width, height, 62, 84, 38, 45, woodDark);
            FillRect(pixels, width, height, 66, 80, 42, 49, woodLight);
            FillRect(pixels, width, height, 68, 78, 49, 53, wood);
            return CreateSprite(workshopTexture, "Runtime Mine Workshop Sprite");
        }

        private static Sprite BuildBossGate()
        {
            const int width = 96;
            const int height = 112;
            bossGateTexture = CreateTexture("Runtime Boss Gate", width, height);
            Color[] pixels = CreateTransparentPixels(width * height);
            Color shadow = new(0.06f, 0.015f, 0.025f, 0.88f);
            Color stoneDark = new(0.22f, 0.07f, 0.08f, 1f);
            Color stone = new(0.48f, 0.17f, 0.11f, 1f);
            Color stoneLight = new(0.74f, 0.32f, 0.14f, 1f);
            Color rune = new(1f, 0.18f, 0.05f, 1f);

            FillRect(pixels, width, height, 7, 89, 5, 14, shadow);
            FillRect(pixels, width, height, 12, 28, 13, 94, stoneDark);
            FillRect(pixels, width, height, 68, 84, 13, 94, stoneDark);
            FillRect(pixels, width, height, 18, 28, 20, 91, stone);
            FillRect(pixels, width, height, 68, 78, 20, 91, stone);
            FillRect(pixels, width, height, 20, 76, 85, 104, stoneDark);
            FillRect(pixels, width, height, 26, 70, 91, 102, stone);
            FillRect(pixels, width, height, 32, 64, 97, 102, stoneLight);
            FillRect(pixels, width, height, 28, 68, 22, 82, new(0.055f, 0.01f, 0.02f, 1f));
            FillRect(pixels, width, height, 34, 38, 38, 44, rune);
            FillRect(pixels, width, height, 58, 62, 38, 44, rune);
            FillRect(pixels, width, height, 42, 46, 52, 58, rune);
            FillRect(pixels, width, height, 42, 46, 64, 70, rune);
            FillRect(pixels, width, height, 22, 26, 32, 47, stoneLight);
            FillRect(pixels, width, height, 70, 74, 32, 47, stoneLight);
            return CreateSprite(bossGateTexture, "Runtime Boss Gate Sprite");
        }

        private static void FillRect(Color[] pixels, int width, int height,
            int xMin, int xMax, int yMin, int yMax, Color color)
        {
            for (int y = Mathf.Max(0, yMin); y < Mathf.Min(height, yMax); y++)
            {
                for (int x = Mathf.Max(0, xMin); x < Mathf.Min(width, xMax); x++)
                    pixels[y * width + x] = color;
            }
        }

        private static Texture2D CreateTexture(string objectName, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = objectName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
        }

        private static Color[] CreateTransparentPixels(int length)
        {
            Color[] pixels = new Color[length];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = new Color(0f, 0f, 0f, 0f);
            return pixels;
        }

        private static bool IsActive(string[] mask, int x, int y)
        {
            return y >= 0 && y < mask.Length && x >= 0 && x < mask[y].Length && mask[y][x] == '#';
        }

        private static Sprite CreateSprite(Texture2D texture, string spriteName)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 16f);
            sprite.name = spriteName;
            return sprite;
        }
    }
}
