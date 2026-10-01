using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the Level 2 HUD in the currently open scene.
// Run it from Unity's menu: Tools > Level 2 > Build HUD.
//
// This must be stored inside an Assets/Editor folder because it uses UnityEditor.
public static class Level2HudBuilder
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float TopBarHeight = 80f;
    private const float BottomBarHeight = 130f;

    private const string TopBarName = "HUD Top Bar";
    private const string BottomBarName = "HUD Bottom Bar";

    private static readonly Color BarColor =
        new Color(0.06f, 0.07f, 0.10f, 0.92f);

    private static readonly Color BarEdgeColor =
        new Color(1f, 1f, 1f, 0.08f);

    private static readonly Color HeaderColor =
        new Color(1f, 1f, 1f, 0.45f);

    private static readonly Color LabelColor =
        new Color(0.95f, 0.95f, 0.97f);

    private static readonly Color HintColor =
        new Color(1f, 1f, 1f, 0.50f);

    private static readonly Color KeyCapColor =
        new Color(0.92f, 0.93f, 0.95f);

    private static readonly Color KeyLabelColor =
        new Color(0.10f, 0.10f, 0.13f);

    private static TMP_FontAsset font;
    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    [MenuItem("Tools/Level 2/Build HUD")]
    private static void Build()
    {
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        Camera mainCamera = Camera.main;

        if (gameManager == null || mainCamera == null)
        {
            Debug.LogError(
                "Level2HudBuilder: open the Level 2 scene and make sure it " +
                "contains a GameManager and a camera tagged MainCamera.");
            return;
        }

        SerializedObject managerData = new SerializedObject(gameManager);
        SerializedProperty statusProperty = managerData.FindProperty("statusText");
        TMP_Text statusText =
            statusProperty != null
                ? statusProperty.objectReferenceValue as TMP_Text
                : null;

        if (statusText == null || statusText.canvas == null)
        {
            Debug.LogError(
                "Level2HudBuilder: assign the Canvas status text to the " +
                "GameManager's Status Text field first.");
            return;
        }

        font = statusText.font;
        roundedSprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        Canvas canvas = statusText.canvas.rootCanvas;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Level 2 HUD");
        int undoGroup = Undo.GetCurrentGroup();

        ConfigureCanvasScaler(canvas);
        RemoveOldBars(canvas.transform, statusText);

        RectTransform topBar = BuildTopBar(canvas.transform, statusText);
        BuildBottomBar(canvas.transform);
        ReserveHudSpace(mainCamera);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = topBar.gameObject;

        Debug.Log(
            "Level2HudBuilder: HUD built. Save the Level 2 scene to keep it.");
    }

    private static void ConfigureCanvasScaler(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

        if (scaler == null)
        {
            scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        }

        Undo.RecordObject(scaler, "Configure Canvas Scaler");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
    }

    private static void RemoveOldBars(Transform canvas, TMP_Text statusText)
    {
        Transform oldTop = canvas.Find(TopBarName);
        Transform oldBottom = canvas.Find(BottomBarName);

        // The status text lives inside the top bar after the first build.
        // Move it back to the Canvas before deleting an old HUD.
        if (oldTop != null && statusText.transform.IsChildOf(oldTop))
        {
            Undo.SetTransformParent(
                statusText.transform,
                canvas,
                "Move Status Text");
        }

        if (oldTop != null)
        {
            Undo.DestroyObjectImmediate(oldTop.gameObject);
        }

        if (oldBottom != null)
        {
            Undo.DestroyObjectImmediate(oldBottom.gameObject);
        }
    }

    private static RectTransform BuildTopBar(
        Transform canvas,
        TMP_Text statusText)
    {
        RectTransform bar = CreateBar(TopBarName, canvas, true);
        Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create HUD Top Bar");

        Undo.SetTransformParent(
            statusText.transform,
            bar,
            "Move Status Text");
        Undo.RecordObject(statusText, "Style Status Text");
        Undo.RecordObject(statusText.rectTransform, "Style Status Text");

        RectTransform textRect = statusText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(40f, 0f);
        textRect.offsetMax = new Vector2(-40f, 0f);
        textRect.localScale = Vector3.one;

        statusText.fontSize = 34f;
        statusText.color = LabelColor;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.richText = true;

        return bar;
    }

    private static RectTransform BuildBottomBar(Transform canvas)
    {
        RectTransform bar = CreateBar(BottomBarName, canvas, false);
        Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create HUD Bottom Bar");

        HorizontalLayoutGroup layout = AddRow(bar, 0f);
        layout.padding = new RectOffset(56, 56, 14, 14);

        // Controls shown on the left.
        RectTransform controls = CreateSection(bar, "CONTROLS");
        RectTransform controlRow = CreateUi("Items", controls);
        AddRow(controlRow, 48f);

        RectTransform move = CreateUi("Move", controlRow);
        AddRow(move, 14f);
        BuildWasdCluster(move);
        BuildLabelColumn(move, "Move", "or ← ↑ ↓ →");

        RectTransform restart = CreateUi("Restart", controlRow);
        AddRow(restart, 14f);
        CreateKeyCap(restart, "R", 40f, 40f);
        BuildLabelColumn(restart, "Restart", null);

        RectTransform menu = CreateUi("Menu", controlRow);
        AddRow(menu, 14f);
        CreateKeyCap(menu, "ESC", 56f, 40f);
        BuildLabelColumn(menu, "Menu", null);

        // Flexible space pushes the legend to the right edge.
        RectTransform spacer = CreateUi("Spacer", bar);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Legend shown on the right.
        RectTransform legend = CreateSection(bar, "LEGEND");
        RectTransform grid = CreateUi("Items", legend);
        GridLayoutGroup gridLayout =
            grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(190f, 34f);
        gridLayout.spacing = new Vector2(20f, 8f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        gridLayout.childAlignment = TextAnchor.MiddleLeft;

        SpriteRenderer player = FindRenderer("Player");
        SpriteRenderer exit = FindRenderer("Exit");
        SpriteRenderer portal = FindRenderer("Portals");
        SpriteRenderer torch = FindRenderer("Torch");

        WarnIfRendererMissing(player, "Player");
        WarnIfRendererMissing(exit, "Exit");
        WarnIfRendererMissing(portal, "Portals");
        WarnIfRendererMissing(torch, "Torch");

        CreateLegendItem(
            grid,
            "You",
            player != null ? player.sprite : circleSprite,
            player != null ? player.color : Color.cyan,
            28f,
            0f);

        CreateLegendItem(
            grid,
            "Exit",
            exit != null ? exit.sprite : roundedSprite,
            exit != null ? exit.color : Color.green,
            24f,
            0f);

        CreateLegendItem(
            grid,
            "Portal",
            portal != null ? portal.sprite : roundedSprite,
            portal != null ? portal.color : Color.white,
            28f,
            0f);

        CreateLegendItem(
            grid,
            "Torch",
            torch != null ? torch.sprite : roundedSprite,
            torch != null ? torch.color : Color.white,
            30f,
            0f);

        return bar;
    }

    private static void ReserveHudSpace(Camera mainCamera)
    {
        Undo.RecordObject(mainCamera, "Reserve Camera Space For HUD");

        float bottom = BottomBarHeight / ReferenceHeight;
        float top = TopBarHeight / ReferenceHeight;

        mainCamera.rect = new Rect(
            0f,
            bottom,
            1f,
            1f - bottom - top);
    }

    private static RectTransform CreateBar(
        string name,
        Transform canvas,
        bool top)
    {
        RectTransform bar = CreateUi(name, canvas);
        float edgeY = top ? 1f : 0f;

        bar.anchorMin = new Vector2(0f, edgeY);
        bar.anchorMax = new Vector2(1f, edgeY);
        bar.pivot = new Vector2(0.5f, edgeY);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta =
            new Vector2(0f, top ? TopBarHeight : BottomBarHeight);

        AddImage(bar, null, BarColor);

        // Thin line on the edge facing the maze.
        RectTransform edge = CreateUi("Edge", bar);
        edge.anchorMin = new Vector2(0f, 1f - edgeY);
        edge.anchorMax = new Vector2(1f, 1f - edgeY);
        edge.pivot = new Vector2(0.5f, 1f - edgeY);
        edge.sizeDelta = new Vector2(0f, 2f);
        AddImage(edge, null, BarEdgeColor);
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        return bar;
    }

    private static RectTransform CreateSection(
        Transform parent,
        string title)
    {
        RectTransform section = CreateUi(title, parent);
        VerticalLayoutGroup layout =
            section.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI header = AddText(
            CreateUi("Header", section),
            title,
            16f,
            HeaderColor);

        header.fontStyle = FontStyles.Bold;
        header.characterSpacing = 8f;

        return section;
    }

    // Creates W above A/S/D, like the physical keyboard layout.
    private static void BuildWasdCluster(Transform parent)
    {
        RectTransform cluster = CreateUi("WASD", parent);
        VerticalLayoutGroup column =
            cluster.gameObject.AddComponent<VerticalLayoutGroup>();

        column.spacing = 4f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;

        CreateKeyCap(cluster, "W", 30f, 30f);

        RectTransform bottomRow = CreateUi("ASD", cluster);
        AddRow(bottomRow, 4f);
        CreateKeyCap(bottomRow, "A", 30f, 30f);
        CreateKeyCap(bottomRow, "S", 30f, 30f);
        CreateKeyCap(bottomRow, "D", 30f, 30f);
    }

    private static TextMeshProUGUI BuildLabelColumn(
        Transform parent,
        string label,
        string hint)
    {
        RectTransform column = CreateUi("Label", parent);
        VerticalLayoutGroup layout =
            column.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        AddText(CreateUi("Text", column), label, 24f, LabelColor);

        if (hint == null)
        {
            return null;
        }

        return AddText(CreateUi("Hint", column), hint, 18f, HintColor);
    }

    private static void CreateKeyCap(
        Transform parent,
        string key,
        float width,
        float height)
    {
        RectTransform cap = CreateUi($"Key {key}", parent);
        Image image = AddImage(cap, roundedSprite, KeyCapColor);
        image.type = Image.Type.Sliced;

        Shadow shadow = cap.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.60f);
        shadow.effectDistance = new Vector2(0f, -3f);

        LayoutElement size = cap.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.preferredHeight = height;

        RectTransform labelRect = CreateUi("Label", cap);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = AddText(
            labelRect,
            key,
            height * 0.5f,
            KeyLabelColor);

        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateLegendItem(
        Transform parent,
        string label,
        Sprite sprite,
        Color color,
        float iconSize,
        float rotation)
    {
        RectTransform item = CreateUi(label, parent);
        AddRow(item, 14f);

        // Fixed-size slot keeps all legend labels aligned.
        RectTransform slot = CreateUi("Icon", item);
        LayoutElement slotSize =
            slot.gameObject.AddComponent<LayoutElement>();
        slotSize.preferredWidth = 30f;
        slotSize.preferredHeight = 30f;

        RectTransform icon = CreateUi("Shape", slot);
        icon.sizeDelta = new Vector2(iconSize, iconSize);
        icon.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image image = AddImage(icon, sprite, color);
        image.preserveAspect = true;

        AddText(CreateUi("Text", item), label, 22f, LabelColor);
    }

    // Searches the named object and all of its children. This is important
    // for Portals because the two triangle SpriteRenderers are children of
    // the Portals parent object in the Level 2 hierarchy.
    private static SpriteRenderer FindRenderer(string objectName)
    {
        GameObject target = GameObject.Find(objectName);

        return target != null
            ? target.GetComponentInChildren<SpriteRenderer>(true)
            : null;
    }

    private static void WarnIfRendererMissing(
        SpriteRenderer renderer,
        string objectName)
    {
        if (renderer == null)
        {
            Debug.LogWarning(
                $"Level2HudBuilder: could not find a SpriteRenderer under " +
                $"'{objectName}'. Check the object's name in the Hierarchy.");
        }
    }

    private static RectTransform CreateUi(
        string name,
        Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.layer = LayerMask.NameToLayer("UI");
        created.transform.SetParent(parent, false);

        return (RectTransform)created.transform;
    }

    private static HorizontalLayoutGroup AddRow(
        RectTransform rect,
        float spacing)
    {
        HorizontalLayoutGroup row =
            rect.gameObject.AddComponent<HorizontalLayoutGroup>();

        row.spacing = spacing;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        return row;
    }

    private static Image AddImage(
        RectTransform rect,
        Sprite sprite,
        Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static TextMeshProUGUI AddText(
        RectTransform rect,
        string text,
        float size,
        Color color)
    {
        TextMeshProUGUI label =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;

        return label;
    }
}
