using UnityEngine;

public class PortalController : MonoBehaviour
{
    public Transform destination;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = destination.position;
        }
    }
}