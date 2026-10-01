using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(PlayerMovement))]
public class WallBumpFeedback : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField, Min(0.05f)] private float bumpCooldown = 0.5f;
    [SerializeField, Range(0f, 1f)] private float pushThreshold = 0.5f;

    [Header("Light Pulse")]
    [SerializeField] private Color pulseColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField, Min(0f)] private float pulseIntensity = 1.2f;
    [SerializeField, Min(0.1f)] private float pulseRadius = 1.5f;
    [SerializeField, Min(0.05f)] private float pulseSeconds = 0.35f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)] private float shakeMagnitude = 0.08f;
    [SerializeField, Min(0.05f)] private float shakeSeconds = 0.15f;

    [Header("Sound")]
    [Tooltip("Leave empty to use a generated thud.")]
    [SerializeField] private AudioClip bumpClip;
    [SerializeField, Range(0f, 1f)] private float bumpVolume = 0.8f;

    private PlayerMovement movement;
    private AudioSource audioSource;
    private Light2D pulseLight;
    private Transform cameraTransform;
    private Vector3 cameraRestPosition;
    private float nextBumpTime;
    private Coroutine pulseRoutine;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (bumpClip == null)
        {
            bumpClip = CreateThudClip();
        }

        GameObject lightObject = new GameObject("Bump Light");
        lightObject.transform.SetParent(transform, false);
        pulseLight = lightObject.AddComponent<Light2D>();
        pulseLight.lightType = Light2D.LightType.Point;
        pulseLight.color = pulseColor;
        pulseLight.pointLightInnerRadius = 0f;
        pulseLight.pointLightOuterRadius = pulseRadius;
        pulseLight.intensity = 0f;
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            cameraRestPosition = cameraTransform.localPosition;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.time < nextBumpTime || !IsDark())
        {
            return;
        }

        Vector2 input = movement.MoveInput;

        if (input == Vector2.zero)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            // The contact normal points away from the wall, so pushing into
            // the wall means the input points against the normal.
            if (Vector2.Dot(input, collision.GetContact(i).normal) < -pushThreshold)
            {
                TriggerBump();
                return;
            }
        }
    }

    private static bool IsDark()
    {
        return GameManager.Instance != null && GameManager.Instance.IsDark;
    }

    private void TriggerBump()
    {
        nextBumpTime = Time.time + bumpCooldown;

        audioSource.PlayOneShot(bumpClip, bumpVolume);

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }
        pulseRoutine = StartCoroutine(PulseLight());

        if (cameraTransform != null && shakeMagnitude > 0f)
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }
            shakeRoutine = StartCoroutine(ShakeCamera());
        }
    }

    private IEnumerator PulseLight()
    {
        float elapsed = 0f;

        while (elapsed < pulseSeconds)
        {
            float fade = 1f - elapsed / pulseSeconds;
            pulseLight.intensity = pulseIntensity * fade;
            elapsed += Time.deltaTime;
            yield return null;
        }

        pulseLight.intensity = 0f;
        pulseRoutine = null;
    }

    private IEnumerator ShakeCamera()
    {
        float elapsed = 0f;

        while (elapsed < shakeSeconds)
        {
            float strength = shakeMagnitude * (1f - elapsed / shakeSeconds);
            Vector2 offset = Random.insideUnitCircle * strength;
            cameraTransform.localPosition =
                cameraRestPosition + new Vector3(offset.x, offset.y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraTransform.localPosition = cameraRestPosition;
        shakeRoutine = null;
    }

    private void OnDisable()
    {
        if (pulseLight != null)
        {
            pulseLight.intensity = 0f;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = cameraRestPosition;
        }
    }

    private static AudioClip CreateThudClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.15f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 30f);
            // Low sine that drops in pitch, plus a little noise for impact.
            float frequency = Mathf.Lerp(110f, 55f, t / duration);
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
            float noise = (Random.value * 2f - 1f) * 0.25f;
            samples[i] = (tone + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create("Thud", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
