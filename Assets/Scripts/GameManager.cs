using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool CanPlayerMove { get; private set; }
    public bool IsDark => phase == Phase.Playing;

    [Header("Level Rules")]
    [SerializeField, Min(0.1f)] private float previewSeconds = 6f;
    [SerializeField, Min(0.1f)] private float revealSeconds = 2f;
    [SerializeField, Min(0)] private int revealCharges = 3;

    [Header("Lantern")]
    [SerializeField, Min(1f)] private float lanternSeconds = 30f;
    [Tooltip("The light starts flickering when this many seconds are left.")]
    [SerializeField, Min(0f)] private float lanternWarningSeconds = 5f;

    [Header("Scenes")]
    [SerializeField] private string menuScene = "MainMenu";

    [Header("Scene References")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Optional. Shows the remaining reveal charges as dots.")]
    [SerializeField] private TMP_Text chargesText;

    private enum Phase
    {
        Preview,
        Playing,
        Revealing,
        Lantern,
        Won,
        Failed
    }

    private const string LanternColor = "#FFD54A";
    private const string DangerColor = "#FF5A4F";
    private const string SuccessColor = "#5CE68A";
    private const string SpentColor = "#FFFFFF33";

    private Phase phase;
    private int chargesRemaining;
    private float runStartTime;
    private Coroutine lightRoutine;

    private bool IsActive =>
        phase == Phase.Playing ||
        phase == Phase.Revealing ||
        phase == Phase.Lantern;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (globalLight == null || statusText == null)
        {
            Debug.LogError(
                "GameManager needs Global Light and Status Text references."
            );
            enabled = false;
            return;
        }

        chargesRemaining = revealCharges;
        UpdateChargesText();
        StartCoroutine(BeginLevel());
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }

        if (keyboard.escapeKey.wasPressedThisFrame &&
            Application.CanStreamedLevelBeLoaded(menuScene))
        {
            SceneManager.LoadScene(menuScene);
        }

        if (phase == Phase.Playing &&
            keyboard.spaceKey.wasPressedThisFrame &&
            chargesRemaining > 0)
        {
            lightRoutine = StartCoroutine(TemporaryReveal());
        }

        if (phase == Phase.Won && keyboard.nKey.wasPressedThisFrame)
        {
            LoadNextLevel();
        }
    }

    private IEnumerator BeginLevel()
    {
        phase = Phase.Preview;
        CanPlayerMove = false;
        globalLight.intensity = 1f;

        float timeRemaining = previewSeconds;

        while (timeRemaining > 0f)
        {
            statusText.text =
                $"Memorize the maze   <b>{Mathf.CeilToInt(timeRemaining)}</b>";

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        globalLight.intensity = 0f;
        phase = Phase.Playing;
        CanPlayerMove = true;
        runStartTime = Time.time;
        UpdateStatusText();
    }

    private IEnumerator TemporaryReveal()
    {
        phase = Phase.Revealing;
        chargesRemaining--;
        globalLight.intensity = 1f;
        UpdateStatusText();
        UpdateChargesText();

        yield return new WaitForSeconds(revealSeconds);

        globalLight.intensity = 0f;
        phase = Phase.Playing;
        lightRoutine = null;
        UpdateStatusText();
    }

    public bool TryCollectLantern()
    {
        if (phase != Phase.Playing && phase != Phase.Revealing)
        {
            return false;
        }

        if (lightRoutine != null)
        {
            StopCoroutine(lightRoutine);
        }

        lightRoutine = StartCoroutine(LanternCountdown());
        return true;
    }

    private IEnumerator LanternCountdown()
    {
        phase = Phase.Lantern;
        float timeRemaining = lanternSeconds;

        while (timeRemaining > 0f)
        {
            globalLight.intensity = timeRemaining > lanternWarningSeconds
                ? 1f
                : Flicker(timeRemaining);

            string warning = timeRemaining > lanternWarningSeconds
                ? ""
                : $"   <color={DangerColor}><b>HURRY!</b></color>";
            statusText.text =
                $"<color={LanternColor}>Lantern lit</color>   " +
                $"Reach the exit: <b>{Mathf.CeilToInt(timeRemaining)}s</b>{warning}";

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        lightRoutine = null;
        FailLevel("The lantern burned out before you escaped");
    }

    private static float Flicker(float timeRemaining)
    {
        // Flickers faster as the lantern gets closer to burning out.
        float speed = Mathf.Lerp(30f, 8f, timeRemaining / 5f);
        return Mathf.Sin(Time.time * speed) > -0.3f ? 0.8f : 0.15f;
    }

    private void UpdateStatusText()
    {
        statusText.text = phase == Phase.Revealing
            ? $"<color={LanternColor}>Light on</color>"
            : "Find the exit in the dark";
    }

    private void UpdateChargesText()
    {
        if (chargesText == null)
        {
            return;
        }

        int used = revealCharges - chargesRemaining;
        chargesText.text =
            $"<color={LanternColor}>{new string('\u25CF', chargesRemaining)}</color>" +
            $"<color={SpentColor}>{new string('\u25CF', used)}</color>";
    }

    public void WinLevel()
    {
        if (!IsActive)
        {
            return;
        }

        StopAllCoroutines();
        phase = Phase.Won;
        CanPlayerMove = false;
        globalLight.intensity = 1f;

        float runSeconds = Time.time - runStartTime;
        statusText.text =
            $"<color={SuccessColor}>Exit found in {runSeconds:0.0}s</color>   " +
            "N  next level   \u00B7   R  replay";
    }

    public void FailLevel(string reason)
    {
        if (!IsActive)
        {
            return;
        }

        StopAllCoroutines();
        phase = Phase.Failed;
        CanPlayerMove = false;
        globalLight.intensity = 1f;
        statusText.text =
            $"<color={DangerColor}>{reason}</color>   Press R to try again";
    }

    public void RestartLevel()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            statusText.text =
                "All levels complete   R  replay   \u00B7   Esc  menu";
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
