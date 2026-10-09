using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum MoveMode { Idle, Sneak, Walk, Run, Exhausted }

    [Header("Referencias")]
    [SerializeField] private Transform cameraTransform;

    [Header("Velocidades (m/s)")]
    [SerializeField] private float sneakSpeed = 1.5f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 5.5f;

    [Header("Cámara")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float standingCameraHeight = 0.7f;
    [SerializeField] private float sneakCameraHeight = 0.35f;
    [SerializeField] private float cameraHeightSmoothing = 8f;

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float sneakDrainPerSecond = 5f;
    [SerializeField] private float runDrainPerSecond = 20f;
    [SerializeField] private float walkRegenPerSecond = 8f;
    [SerializeField] private float idleRegenPerSecond = 15f;
    [SerializeField] private float recoverThreshold = 25f;
    [SerializeField] private float exhaustedDuration = 1f;

    [Header("Ruido (radio en metros)")]
    [SerializeField] private float idleNoise = 0f;
    [SerializeField] private float sneakNoise = 2f;
    [SerializeField] private float walkNoise = 6f;
    [SerializeField] private float runNoise = 15f;
    [SerializeField] private float exhaustedNoise = 6f;

    private const float Gravity = -9.81f;

    private CharacterController controller;
    private float yaw;
    private float pitch;
    private float verticalVelocity;
    private float stamina;
    private float exhaustedTimer;
    private bool needsRecovery;

    public MoveMode CurrentMode { get; private set; } = MoveMode.Idle;
    public float NoiseRadius { get; private set; }
    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
    public bool IsExhausted => CurrentMode == MoveMode.Exhausted;
    public bool InputEnabled { get; set; } = true;
    public float RegenMultiplier { get; set; } = 1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        stamina = maxStamina;
    }

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        LockCursor(true);
    }

    private void Update()
    {
        HandleCursor();

        if (!InputEnabled)
        {
            CurrentMode = MoveMode.Idle;
            NoiseRadius = idleNoise;
            return;
        }

        HandleLook();
        HandleMovement();
    }

    private void HandleCursor()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            LockCursor(false);
        }
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor(true);
        }
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || cameraTransform == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * mouseSensitivity;
        pitch -= delta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 input = ReadMoveInput(keyboard);
        bool moving = input.sqrMagnitude > 0.01f;
        bool wantsRun = keyboard != null && keyboard.leftShiftKey.isPressed;
        bool wantsSneak = keyboard != null && keyboard.leftCtrlKey.isPressed;

        // 1) Decidir el modo de movimiento
        if (exhaustedTimer > 0f)
        {
            exhaustedTimer -= Time.deltaTime;
            CurrentMode = MoveMode.Exhausted;
        }
        else
        {
            if (needsRecovery && stamina >= recoverThreshold)
            {
                needsRecovery = false;
            }

            bool canUseSpecial = !needsRecovery && stamina > 0f;

            if (!moving)
            {
                CurrentMode = MoveMode.Idle;
            }
            else if (canUseSpecial && wantsSneak)
            {
                CurrentMode = MoveMode.Sneak;
            }
            else if (canUseSpecial && wantsRun)
            {
                CurrentMode = MoveMode.Run;
            }
            else
            {
                CurrentMode = MoveMode.Walk;
            }
        }

        // 2) Actualizar la estamina
        UpdateStamina();

        // 3) Ruido que escuchará el enemigo
        NoiseRadius = GetNoiseForMode(CurrentMode);

        // 4) Mover al jugador
        float speed = GetSpeedForMode(CurrentMode);
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += Gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        UpdateCameraHeight();
    }

    private Vector2 ReadMoveInput(Keyboard keyboard)
    {
        if (keyboard == null || CurrentMode == MoveMode.Exhausted)
        {
            return Vector2.zero;
        }

        float x = 0f;
        float y = 0f;

        if (keyboard.wKey.isPressed) y += 1f;
        if (keyboard.sKey.isPressed) y -= 1f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.aKey.isPressed) x -= 1f;

        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private void UpdateStamina()
    {
        switch (CurrentMode)
        {
            case MoveMode.Run:
                stamina -= runDrainPerSecond * Time.deltaTime;
                break;
            case MoveMode.Sneak:
                stamina -= sneakDrainPerSecond * Time.deltaTime;
                break;
            case MoveMode.Walk:
                stamina += walkRegenPerSecond * RegenMultiplier * Time.deltaTime;
                break;
            case MoveMode.Idle:
                stamina += idleRegenPerSecond * RegenMultiplier * Time.deltaTime;
                break;
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        if (stamina <= 0f && CurrentMode != MoveMode.Exhausted)
        {
            exhaustedTimer = exhaustedDuration;
            needsRecovery = true;
            CurrentMode = MoveMode.Exhausted;
        }
    }

    private float GetSpeedForMode(MoveMode mode)
    {
        switch (mode)
        {
            case MoveMode.Sneak: return sneakSpeed;
            case MoveMode.Run: return runSpeed;
            case MoveMode.Walk: return walkSpeed;
            default: return 0f;
        }
    }

    private float GetNoiseForMode(MoveMode mode)
    {
        switch (mode)
        {
            case MoveMode.Sneak: return sneakNoise;
            case MoveMode.Walk: return walkNoise;
            case MoveMode.Run: return runNoise;
            case MoveMode.Exhausted: return exhaustedNoise;
            default: return idleNoise;
        }
    }

    private void UpdateCameraHeight()
    {
        if (cameraTransform == null)
        {
            return;
        }

        float targetHeight = CurrentMode == MoveMode.Sneak ? sneakCameraHeight : standingCameraHeight;
        Vector3 localPosition = cameraTransform.localPosition;
        localPosition.y = Mathf.Lerp(localPosition.y, targetHeight, cameraHeightSmoothing * Time.deltaTime);
        cameraTransform.localPosition = localPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, NoiseRadius);
    }
}