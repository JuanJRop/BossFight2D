using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Project.Scripts.Arena
{
    [DefaultExecutionOrder(-500)]
    public sealed class ArenaBounds : MonoBehaviour
    {
        public static ArenaBounds Instance { get; private set; }

        [Header("Bounds")]
        [SerializeField] private Tilemap arenaTilemap;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private bool fitCameraToTilemap = true;
        [SerializeField, Min(0f)] private float cameraMargin = 0.55f;
        [SerializeField] private Vector2 viewportPadding = new(0.025f, 0.04f);
        [SerializeField, Min(0.1f)] private float wallThickness = 0.5f;
        [SerializeField, Min(0f)] private float actorPadding = 0.2f;

        [Header("Map Presentation")]
        [SerializeField] private Color gridColor = new(0.08f, 0.72f, 0.84f, 0.16f);
        [SerializeField, Range(2, 16)] private int gridColumns = 8;
        [SerializeField, Range(2, 12)] private int gridRows = 5;
        [SerializeField, Min(0.005f)] private float gridWidth = 0.025f;
        [SerializeField, Min(0f)] private float innerFrameInset = 0.35f;

        [Header("Boundary Presentation")]
        [SerializeField] private Color boundaryColor = new(1f, 0.28f, 0.04f, 0.95f);
        [SerializeField, Min(0.01f)] private float boundaryWidth = 0.16f;

        private readonly List<GameObject> runtimeObjects = new();
        private Transform player;
        private Transform enemy;
        private Material boundaryMaterial;

        public Vector2 Minimum { get; private set; }
        public Vector2 Maximum { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveBounds();
            FitCameraToArena();
            DisableLegacyWallColliders();
            BuildMapDecoration();
            BuildPhysicalWalls();
            BuildBoundaryVisual();
        }

        private void Start()
        {
            FitCameraToArena();
            ResolveActors();
        }

        private void LateUpdate()
        {
            if (player == null || enemy == null) ResolveActors();
            ConfineActor(player);
            ConfineActor(enemy);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (boundaryMaterial != null) Destroy(boundaryMaterial);
        }

        public static bool TryGet(out ArenaBounds bounds)
        {
            bounds = Instance;
            return bounds != null;
        }

        public void GetInnerBounds(out Vector2 minimum, out Vector2 maximum, float margin = 0f)
        {
            float safeMargin = Mathf.Max(0f, margin);
            minimum = Minimum + Vector2.one * safeMargin;
            maximum = Maximum - Vector2.one * safeMargin;
        }

        public Vector2 Clamp(Vector2 position, float margin = 0f)
        {
            GetInnerBounds(out Vector2 minimum, out Vector2 maximum, margin);
            return new Vector2(
                Mathf.Clamp(position.x, minimum.x, maximum.x),
                Mathf.Clamp(position.y, minimum.y, maximum.y));
        }

        public bool Contains(Vector2 position, float margin = 0f)
        {
            GetInnerBounds(out Vector2 minimum, out Vector2 maximum, margin);
            return position.x >= minimum.x && position.x <= maximum.x &&
                   position.y >= minimum.y && position.y <= maximum.y;
        }

        public void GetHorizontalPath(float normalizedY, out Vector2 start, out Vector2 end, float margin = 0.05f)
        {
            GetInnerBounds(out Vector2 minimum, out Vector2 maximum, margin);
            float y = Mathf.Lerp(minimum.y, maximum.y, Mathf.Clamp01(normalizedY));
            start = new Vector2(minimum.x, y);
            end = new Vector2(maximum.x, y);
        }

        public void GetVerticalPath(float normalizedX, out Vector2 start, out Vector2 end, float margin = 0.05f)
        {
            GetInnerBounds(out Vector2 minimum, out Vector2 maximum, margin);
            float x = Mathf.Lerp(minimum.x, maximum.x, Mathf.Clamp01(normalizedX));
            start = new Vector2(x, minimum.y);
            end = new Vector2(x, maximum.y);
        }

        private void ResolveBounds()
        {
            ResolveTilemap();
            if (arenaTilemap != null && arenaTilemap.cellBounds.size.x > 0 && arenaTilemap.cellBounds.size.y > 0)
            {
                Bounds localBounds = arenaTilemap.localBounds;
                Vector3 worldMinimum = arenaTilemap.transform.TransformPoint(localBounds.min);
                Vector3 worldMaximum = arenaTilemap.transform.TransformPoint(localBounds.max);
                Minimum = Vector2.Min(worldMinimum, worldMaximum);
                Maximum = Vector2.Max(worldMinimum, worldMaximum);
                return;
            }

            if (arenaCamera == null) arenaCamera = Camera.main;
            if (arenaCamera != null && arenaCamera.orthographic)
            {
                float distance = Mathf.Abs(arenaCamera.transform.position.z - transform.position.z);
                Vector3 bottomLeft = arenaCamera.ViewportToWorldPoint(
                    new Vector3(viewportPadding.x, viewportPadding.y, distance));
                Vector3 topRight = arenaCamera.ViewportToWorldPoint(
                    new Vector3(1f - viewportPadding.x, 1f - viewportPadding.y, distance));
                Minimum = Vector2.Min(bottomLeft, topRight);
                Maximum = Vector2.Max(bottomLeft, topRight);
                return;
            }

            Minimum = new Vector2(-18f, -13f);
            Maximum = new Vector2(19f, 14f);
        }

        private void ResolveTilemap()
        {
            if (arenaTilemap != null) return;
            GameObject background = GameObject.Find("BackGround");
            if (background != null) arenaTilemap = background.GetComponent<Tilemap>();
        }

        private void FitCameraToArena()
        {
            if (!fitCameraToTilemap) return;
            if (arenaCamera == null) arenaCamera = Camera.main;
            if (arenaCamera == null || !arenaCamera.orthographic) return;

            Vector2 size = Maximum - Minimum;
            Vector2 center = (Minimum + Maximum) * 0.5f;
            float aspect = Mathf.Max(0.1f, arenaCamera.aspect);
            float verticalSize = size.y * 0.5f;
            float horizontalSize = size.x / (2f * aspect);
            float orthographicSize = Mathf.Max(verticalSize, horizontalSize) + cameraMargin;

            arenaCamera.orthographicSize = orthographicSize;
            Vector3 cameraPosition = arenaCamera.transform.position;
            arenaCamera.transform.position = new Vector3(center.x, center.y, cameraPosition.z);

            CinemachineCamera virtualCamera = FindFirstObjectByType<CinemachineCamera>();
            if (virtualCamera == null) return;

            LensSettings lens = virtualCamera.Lens;
            lens.OrthographicSize = orthographicSize;
            virtualCamera.Lens = lens;

            Vector3 virtualPosition = virtualCamera.transform.position;
            virtualCamera.transform.position = new Vector3(center.x, center.y, virtualPosition.z);
        }

        private void DisableLegacyWallColliders()
        {
            GameObject[] legacyWalls = GameObject.FindGameObjectsWithTag("Wall");
            foreach (GameObject legacyWall in legacyWalls)
            {
                if (legacyWall == null || legacyWall.transform.IsChildOf(transform)) continue;
                foreach (Collider2D collider in legacyWall.GetComponentsInChildren<Collider2D>(true))
                {
                    collider.enabled = false;
                }

            }
        }

        private void BuildMapDecoration()
        {
            Vector2 size = Maximum - Minimum;
            for (int column = 1; column < gridColumns; column++)
            {
                float x = Mathf.Lerp(Minimum.x, Maximum.x, column / (float)gridColumns);
                CreateDecorativeLine(
                    "Arena Grid Vertical",
                    new Vector2(x, Minimum.y),
                    new Vector2(x, Maximum.y),
                    gridColor,
                    gridWidth,
                    4);
            }

            for (int row = 1; row < gridRows; row++)
            {
                float y = Mathf.Lerp(Minimum.y, Maximum.y, row / (float)gridRows);
                CreateDecorativeLine(
                    "Arena Grid Horizontal",
                    new Vector2(Minimum.x, y),
                    new Vector2(Maximum.x, y),
                    gridColor,
                    gridWidth,
                    4);
            }

            float safeInset = Mathf.Min(innerFrameInset, Mathf.Min(size.x, size.y) * 0.2f);
            Color frameColor = new(boundaryColor.r, boundaryColor.g, boundaryColor.b, 0.28f);
            CreateFrame(
                "Arena Inner Frame",
                Minimum + Vector2.one * safeInset,
                Maximum - Vector2.one * safeInset,
                frameColor,
                Mathf.Max(0.01f, boundaryWidth * 0.35f),
                5);
        }

        private void BuildPhysicalWalls()
        {
            Vector2 size = Maximum - Minimum;
            Vector2 center = (Minimum + Maximum) * 0.5f;
            CreateWall("Arena Wall Left",
                new Vector2(Minimum.x - wallThickness * 0.5f, center.y),
                new Vector2(wallThickness, size.y + wallThickness * 2f));
            CreateWall("Arena Wall Right",
                new Vector2(Maximum.x + wallThickness * 0.5f, center.y),
                new Vector2(wallThickness, size.y + wallThickness * 2f));
            CreateWall("Arena Wall Bottom",
                new Vector2(center.x, Minimum.y - wallThickness * 0.5f),
                new Vector2(size.x + wallThickness * 2f, wallThickness));
            CreateWall("Arena Wall Top",
                new Vector2(center.x, Maximum.y + wallThickness * 0.5f),
                new Vector2(size.x + wallThickness * 2f, wallThickness));
        }

        private void CreateWall(string objectName, Vector2 position, Vector2 size)
        {
            GameObject wall = new(objectName);
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.layer = gameObject.layer;
            wall.tag = "Wall";
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
            runtimeObjects.Add(wall);
        }

        private void BuildBoundaryVisual()
        {
            CreateFrame(
                "Arena Boundary Visual",
                Minimum,
                Maximum,
                boundaryColor,
                boundaryWidth,
                20);
        }

        private void CreateFrame(
            string objectName,
            Vector2 minimum,
            Vector2 maximum,
            Color color,
            float width,
            int sortingOrder)
        {
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform);
            runtimeObjects.Add(visual);

            LineRenderer line = ConfigureLine(visual, color, width, sortingOrder);
            line.loop = true;
            line.positionCount = 4;
            line.numCornerVertices = 3;
            line.SetPosition(0, new Vector3(minimum.x, minimum.y));
            line.SetPosition(1, new Vector3(minimum.x, maximum.y));
            line.SetPosition(2, new Vector3(maximum.x, maximum.y));
            line.SetPosition(3, new Vector3(maximum.x, minimum.y));
        }

        private void CreateDecorativeLine(
            string objectName,
            Vector2 start,
            Vector2 end,
            Color color,
            float width,
            int sortingOrder)
        {
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform);
            runtimeObjects.Add(visual);

            LineRenderer line = ConfigureLine(visual, color, width, sortingOrder);
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private LineRenderer ConfigureLine(GameObject visual, Color color, float width, int sortingOrder)
        {
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            line.material = GetBoundaryMaterial();
            return line;
        }

        private Material GetBoundaryMaterial()
        {
            if (boundaryMaterial != null) return boundaryMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            boundaryMaterial = shader != null ? new Material(shader) : null;
            return boundaryMaterial;
        }

        private void ResolveActors()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            if (enemy == null)
            {
                GameObject enemyObject = GameObject.FindGameObjectWithTag("Enemy");
                if (enemyObject != null) enemy = enemyObject.transform;
            }
        }

        private void ConfineActor(Transform actor)
        {
            if (actor == null) return;

            Collider2D actorCollider = actor.GetComponent<Collider2D>();
            if (actorCollider == null) actorCollider = actor.GetComponentInChildren<Collider2D>();

            Vector2 current = actor.position;
            Vector2 extents = actorCollider != null ? actorCollider.bounds.extents : Vector2.zero;
            Vector2 colliderOffset = actorCollider != null
                ? (Vector2)actorCollider.bounds.center - current
                : Vector2.zero;

            Vector2 minimum = Minimum + extents + Vector2.one * actorPadding - colliderOffset;
            Vector2 maximum = Maximum - extents - Vector2.one * actorPadding - colliderOffset;
            Vector2 confined = new(
                Mathf.Clamp(current.x, minimum.x, maximum.x),
                Mathf.Clamp(current.y, minimum.y, maximum.y));
            if ((confined - current).sqrMagnitude < 0.0001f) return;

            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            if (body == null) body = actor.GetComponentInChildren<Rigidbody2D>();
            if (body != null)
            {
                body.position = confined;
                Vector2 velocity = body.linearVelocity;
                if (!Mathf.Approximately(confined.x, current.x)) velocity.x = 0f;
                if (!Mathf.Approximately(confined.y, current.y)) velocity.y = 0f;
                body.linearVelocity = velocity;
            }
            else
            {
                actor.position = confined;
            }
        }

        private void OnValidate()
        {
            cameraMargin = Mathf.Max(0f, cameraMargin);
            viewportPadding.x = Mathf.Clamp(viewportPadding.x, 0f, 0.45f);
            viewportPadding.y = Mathf.Clamp(viewportPadding.y, 0f, 0.45f);
            wallThickness = Mathf.Max(0.1f, wallThickness);
            actorPadding = Mathf.Max(0f, actorPadding);
            gridColumns = Mathf.Clamp(gridColumns, 2, 16);
            gridRows = Mathf.Clamp(gridRows, 2, 12);
            gridWidth = Mathf.Max(0.005f, gridWidth);
            innerFrameInset = Mathf.Max(0f, innerFrameInset);
            boundaryWidth = Mathf.Max(0.01f, boundaryWidth);
        }
    }
}
