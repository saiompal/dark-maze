using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LanternPickup : MonoBehaviour
{
    [Header("Glow In The Dark")]
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField, Min(0.1f)] private float glowRadius = 1.4f;
    [SerializeField, Min(0f)] private float minGlow = 0.35f;
    [SerializeField, Min(0f)] private float maxGlow = 0.8f;
    [SerializeField, Min(0.1f)] private float pulseSpeed = 3f;

    private Light2D glow;

    private void Awake()
    {
        GameObject glowObject = new GameObject("Lantern Glow");
        glowObject.transform.SetParent(transform, false);
        glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = glowColor;
        glow.pointLightInnerRadius = 0f;
        glow.pointLightOuterRadius = glowRadius;
    }

    private void Update()
    {
        // A slow pulse so the lantern catches the player's eye in the dark.
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        glow.intensity = Mathf.Lerp(minGlow, maxGlow, t);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.TryCollectLantern())
        {
            Destroy(gameObject);
        }
    }
}
