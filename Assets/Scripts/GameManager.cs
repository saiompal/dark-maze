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

    [Header("Level Rules")]
    [SerializeField, Min(0.1f)] private float previewSeconds = 6f;
    [SerializeField, Min(0.1f)] private float revealSeconds = 2f;
    [SerializeField, Min(0)] private int revealCharges = 3;

    [Header("Scene References")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private TMP_Text statusText;

    private enum Phase
    {
        Preview,
        Playing,
        Revealing,
        Won
    }

    private Phase phase;
    private int chargesRemaining;

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

        if (phase == Phase.Playing &&
            keyboard.spaceKey.wasPressedThisFrame &&
            chargesRemaining > 0)
        {
            StartCoroutine(TemporaryReveal());
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
                $"Memorize the maze: {Mathf.CeilToInt(timeRemaining)}";

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        globalLight.intensity = 0f;
        phase = Phase.Playing;
        CanPlayerMove = true;
        UpdateStatusText();
    }

    private IEnumerator TemporaryReveal()
    {
        phase = Phase.Revealing;
        chargesRemaining--;
        globalLight.intensity = 1f;
        UpdateStatusText();

        yield return new WaitForSeconds(revealSeconds);

        if (phase == Phase.Won)
        {
            yield break;
        }

        globalLight.intensity = 0f;
        phase = Phase.Playing;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (phase == Phase.Revealing)
        {
            statusText.text =
                $"Light on   Charges remaining: {chargesRemaining}";
            return;
        }

        statusText.text =
            $"Dark   Space reveals   Charges: {chargesRemaining}   R restarts";
    }

    public void WinLevel()
    {
        if (phase == Phase.Won)
        {
            return;
        }

        StopAllCoroutines();
        phase = Phase.Won;
        CanPlayerMove = false;
        globalLight.intensity = 1f;
        statusText.text =
            "Exit found   Press N for the next level or R to replay";
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
                "All levels complete   Press R to replay this level";
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