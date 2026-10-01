using UnityEngine;

public class Hazard : MonoBehaviour
{
    [SerializeField] private string failReason = "You stepped on a spike trap";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance?.FailLevel(failReason);
        }
    }
}
