using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;

    private Rigidbody2D body;
    private Vector2 moveInput;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        bool canMove = GameManager.Instance == null ||
                       GameManager.Instance.CanPlayerMove;

        if (!canMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            horizontal -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            horizontal += 1f;

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            vertical -= 1f;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            vertical += 1f;

        moveInput = new Vector2(horizontal, vertical).normalized;
    }

    private void FixedUpdate()
    {
        body.linearVelocity = moveInput * moveSpeed;
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}