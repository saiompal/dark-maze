using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rebuilds the Level 1 maze from the text map below.
// Run it from the menu: Tools > Level 1 > Build Maze From Layout.
// Edit the map, run it again, then save the scene.
public static class Level1Builder
{
    // # wall   . floor   S player start   E exit   L lantern   X spike trap
    // Each character is one world unit. S, E and L are placed at the
    // centre of all tiles that use that letter.
    private static readonly string[] Layout =
    {
        "##################################",
        "#.....#...........#............EE#",
        "#.....#...........#............EE#",
        "#..#..#..#######..#..####..####..#",
        "#..#..#.....#.....#.....#..#..#..#",
        "#..#..#.....#.....#.....#..#..#..#",
        "#..#..####..#..#######..#..#..#..#",
        "#..#........#...........#..#.....#",
        "#..#........#...........#..#.....#",
        "#..############################.X#",
        "#.....X.....X.....X.....X........#",
        "#........X.....X.....X.....X.....#",
        "#..###############################",
        "#..#........#..#.................#",
        "#..#........#..#.................#",
        "#..#..#..#..#..#..#..#..#######..#",
        "#SS...#..#........#..#LL#........#",
        "#SS...#..#........#..#LL#........#",
        "##################################",
    };

    private const float PreviewSeconds = 10f;

    private static readonly Color DefaultWallColor = new Color(0.83f, 0.83f, 0.83f);
    private static readonly Color SpikeColor = new Color(0.8f, 0.12f, 0.1f);
    private static readonly Color LanternColor = new Color(1f, 0.85f, 0.3f);

    [MenuItem("Tools/Level 1/Build Maze From Layout")]
    private static void Build()
    {
        GameObject floor = GameObject.Find("Floor");
        GameObject walls = GameObject.Find("Walls");
        GameObject player = GameObject.Find("Player");
        GameObject exit = GameObject.Find("Exit");
        Camera camera = Camera.main;
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();

        if (floor == null || walls == null || player == null ||
            exit == null || camera == null || gameManager == null)
        {
            Debug.LogError(
                "Level1Builder: open the Level1 scene first. It needs Floor, " +
                "Walls, Player, Exit, Main Camera and GameManager objects."
            );
            return;
        }

        SpriteRenderer floorRenderer = floor.GetComponent<SpriteRenderer>();
        Sprite squareSprite = floorRenderer.sprite;
        Material litMaterial = floorRenderer.sharedMaterial;
        Color wallColor = DefaultWallColor;

        SpriteRenderer existingWall = walls.GetComponentInChildren<SpriteRenderer>();
        if (existingWall != null)
        {
            wallColor = existingWall.color;
        }

        int width = Layout[0].Length;
        int height = Layout.Length;

        if (Layout.Any(row => row.Length != width))
        {
            Debug.LogError("Level1Builder: every layout row must be the same length.");
            return;
        }

        Undo.SetCurrentGroupName("Build Level 1 Maze");
        int undoGroup = Undo.GetCurrentGroup();

        // Walls
        for (int i = walls.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(walls.transform.GetChild(i).gameObject);
        }

        List<RectInt> wallRects = MergeWallTiles(width, height);
        foreach (RectInt rect in wallRects)
        {
            Vector2 center = RectCenter(rect, width, height);
            GameObject wall = CreateSprite(
                "Wall", walls.transform, center, squareSprite, litMaterial,
                wallColor, 0
            );
            wall.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            wall.AddComponent<BoxCollider2D>();
        }

        // Spike traps
        GameObject hazards = RecreateRoot("Hazards");
        int spikeCount = 0;

        foreach (Vector2Int tile in TilesWith('X'))
        {
            GameObject spike = CreateSprite(
                "Spike Trap", hazards.transform, TileCenter(tile, width, height),
                squareSprite, litMaterial, SpikeColor, 1
            );
            spike.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            BoxCollider2D trigger = spike.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.85f, 0.85f);

            spike.AddComponent<Hazard>();
            spikeCount++;
        }

        // Lantern
        GameObject oldLantern = GameObject.Find("Lantern");
        if (oldLantern != null)
        {
            Undo.DestroyObjectImmediate(oldLantern);
        }

        if (TryGetMarker('L', width, height, out Vector2 lanternPosition))
        {
            GameObject lantern = CreateSprite(
                "Lantern", null, lanternPosition, squareSprite, litMaterial,
                LanternColor, 6
            );
            lantern.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            lantern.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

            CircleCollider2D trigger = lantern.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.8f;

            lantern.AddComponent<LanternPickup>();
        }

        // Player, exit and floor
        if (TryGetMarker('S', width, height, out Vector2 startPosition))
        {
            Undo.RecordObject(player.transform, "Move Player");
            player.transform.position = startPosition;
        }

        if (TryGetMarker('E', width, height, out Vector2 exitPosition))
        {
            Undo.RecordObject(exit.transform, "Move Exit");
            exit.transform.position = exitPosition;
        }

        Undo.RecordObject(floor.transform, "Resize Floor");
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(width, height, 1f);

        if (player.GetComponent<WallBumpFeedback>() == null)
        {
            Undo.AddComponent<WallBumpFeedback>(player);
        }

        HudBuilder.FrameCamera(camera);
        SetPreviewSeconds(gameManager);
        EnsureSceneInBuildSettings();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            $"Level1Builder: built a {width}x{height} maze with " +
            $"{wallRects.Count} walls and {spikeCount} spike traps. Save the scene to keep it."
        );
    }

    // Greedily merges wall tiles into rectangles so the maze uses a few
    // large colliders instead of hundreds of tiny ones.
    private static List<RectInt> MergeWallTiles(int width, int height)
    {
        bool[,] used = new bool[width, height];
        List<RectInt> rects = new List<RectInt>();

        bool IsFreeWall(int x, int y) => Layout[y][x] == '#' && !used[x, y];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsFreeWall(x, y))
                {
                    continue;
                }

                int rectWidth = 1;
                while (x + rectWidth < width && IsFreeWall(x + rectWidth, y))
                {
                    rectWidth++;
                }

                int rectHeight = 1;
                while (y + rectHeight < height &&
                       Enumerable.Range(x, rectWidth).All(cx => IsFreeWall(cx, y + rectHeight)))
                {
                    rectHeight++;
                }

                for (int dy = 0; dy < rectHeight; dy++)
                {
                    for (int dx = 0; dx < rectWidth; dx++)
                    {
                        used[x + dx, y + dy] = true;
                    }
                }

                rects.Add(new RectInt(x, y, rectWidth, rectHeight));
            }
        }

        return rects;
    }

    private static IEnumerable<Vector2Int> TilesWith(char marker)
    {
        for (int y = 0; y < Layout.Length; y++)
        {
            for (int x = 0; x < Layout[y].Length; x++)
            {
                if (Layout[y][x] == marker)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }
    }

    private static bool TryGetMarker(char marker, int width, int height, out Vector2 position)
    {
        List<Vector2Int> tiles = TilesWith(marker).ToList();
        position = Vector2.zero;

        if (tiles.Count == 0)
        {
            Debug.LogWarning($"Level1Builder: layout has no '{marker}' tile.");
            return false;
        }

        foreach (Vector2Int tile in tiles)
        {
            position += TileCenter(tile, width, height);
        }

        position /= tiles.Count;
        return true;
    }

    // Row 0 of the layout is the top of the maze, and the maze is centred on (0, 0).
    private static Vector2 TileCenter(Vector2Int tile, int width, int height)
    {
        return new Vector2(
            tile.x - (width - 1) * 0.5f,
            (height - 1) * 0.5f - tile.y
        );
    }

    private static Vector2 RectCenter(RectInt rect, int width, int height)
    {
        Vector2 topLeft = TileCenter(rect.position, width, height);
        return topLeft + new Vector2((rect.width - 1) * 0.5f, -(rect.height - 1) * 0.5f);
    }

    private static GameObject CreateSprite(
        string name, Transform parent, Vector2 position, Sprite sprite,
        Material material, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static GameObject RecreateRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
        return root;
    }

    private static void SetPreviewSeconds(GameManager gameManager)
    {
        SerializedObject serialized = new SerializedObject(gameManager);
        serialized.FindProperty("previewSeconds").floatValue = PreviewSeconds;
        serialized.ApplyModifiedProperties();
    }

    // Restart (R) reloads the scene by build index, so the scene must be in
    // the build list for it to work.
    private static void EnsureSceneInBuildSettings()
    {
        string scenePath = SceneManager.GetActiveScene().path;
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

        if (scenes.Any(s => s.path == scenePath))
        {
            return;
        }

        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Level1Builder: added {scenePath} to the build settings.");
    }
}
