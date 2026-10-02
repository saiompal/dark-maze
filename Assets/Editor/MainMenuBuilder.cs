using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates a basic MainMenu scene and puts it first in the build settings.
// Run it from the menu: Tools > Main Menu > Build Main Menu Scene.
public static class MainMenuBuilder
{
    private const string GameTitle = "IN THE DARK";

    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    // One menu button per level, in play order.
    private static readonly string[] LevelPaths =
    {
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level2.unity",
    };

    [MenuItem("Tools/Main Menu/Build Main Menu Scene")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (File.Exists(MenuScenePath) &&
            !EditorUtility.DisplayDialog(
                "Rebuild Main Menu?",
                $"{MenuScenePath} already exists. Replace it with a fresh menu?",
                "Replace", "Cancel"))
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.AddComponent<AudioListener>();

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(HudBuilder.ReferenceWidth, HudBuilder.ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        VerticalLayoutGroup layout = canvasObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        MainMenu menu = canvasObject.AddComponent<MainMenu>();

        GameObject title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(canvasObject.transform, false);
        ((RectTransform)title.transform).sizeDelta = new Vector2(1200f, 160f);
        TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = GameTitle;
        titleText.fontSize = 96f;
        titleText.alignment = TextAlignmentOptions.Center;

        Button firstLevel = null;
        for (int i = 0; i < LevelPaths.Length; i++)
        {
            Button level = CreateButton(canvasObject.transform, $"Level {i + 1}");
            UnityEventTools.AddStringPersistentListener(
                level.onClick, menu.LoadLevel,
                Path.GetFileNameWithoutExtension(LevelPaths[i]));
            firstLevel ??= level;
        }

        Button quit = CreateButton(canvasObject.transform, "Quit");
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>().firstSelectedGameObject = firstLevel.gameObject;
        eventSystem.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        SetBuildOrder();

        Debug.Log($"MainMenuBuilder: created {MenuScenePath} and set the build order.");
    }

    // A standard Unity UI button.
    private static Button CreateButton(Transform parent, string label)
    {
        TMP_DefaultControls.Resources resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
        };

        GameObject buttonObject = TMP_DefaultControls.CreateButton(resources);
        buttonObject.name = $"{label} Button";
        buttonObject.transform.SetParent(parent, false);
        ((RectTransform)buttonObject.transform).sizeDelta = new Vector2(320f, 70f);

        TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 32f;

        return buttonObject.GetComponent<Button>();
    }

    // MainMenu first, then the levels in order, then any other scenes
    // already listed. The empty SampleScene template is left out so N
    // after the last level doesn't load a blank scene.
    private static void SetBuildOrder()
    {
        string[] pinned = new[] { MenuScenePath }.Concat(LevelPaths).ToArray();

        List<EditorBuildSettingsScene> scenes = pinned
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToList();

        scenes.AddRange(EditorBuildSettings.scenes.Where(s =>
            !pinned.Contains(s.path) &&
            !s.path.EndsWith("/SampleScene.unity")));

        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }
}
