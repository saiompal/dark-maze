using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TorchPickup : MonoBehaviour
{
    [Header("Maze lighting")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, Range(0f, 1f)] private float darkIntensity = 0.05f;
    [SerializeField, Range(0f, 2f)] private float brightIntensity = 1f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

    private bool collected;

    private void Awake()
    {
        // This fallback uses the exact object name shown in your hierarchy.
        if (globalLight == null)
        {
            GameObject lightObject = GameObject.Find("GlobalLight");

            if (lightObject != null)
            {
                globalLight = lightObject.GetComponent<Light2D>();
            }
        }

        if (globalLight == null)
        {
            Debug.LogError(
                "TorchPickup: assign the GlobalLight Light2D component.",
                this);
            return;
        }

        globalLight.intensity = darkIntensity;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player"))
        {
            return;
        }

        collected = true;

        Collider2D torchCollider = GetComponent<Collider2D>();
        if (torchCollider != null)
        {
            torchCollider.enabled = false;
        }

        SpriteRenderer torchRenderer = GetComponent<SpriteRenderer>();
        if (torchRenderer != null)
        {
            torchRenderer.enabled = false;
        }

        StartCoroutine(BrightenMaze());
    }

    private IEnumerator BrightenMaze()
    {
        if (globalLight == null)
        {
            Destroy(gameObject);
            yield break;
        }

        float startingIntensity = globalLight.intensity;

        if (fadeDuration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                globalLight.intensity = Mathf.Lerp(
                    startingIntensity,
                    brightIntensity,
                    progress);

                yield return null;
            }
        }

        globalLight.intensity = brightIntensity;
        Destroy(gameObject);
    }
}
