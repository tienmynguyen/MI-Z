using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetwork : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float lookSensitivity = 2.2f;

    [Header("Combat")]
    [SerializeField] private int damagePerShot = 20;
    [SerializeField] private float fireRate = 5f;
    [SerializeField] private float shootDistance = 120f;
    [SerializeField] private LayerMask shootMask = ~0;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private int startReserveAmmo = 120;
    [SerializeField] private float reloadDuration = 1.4f;

    [Header("Health / Armor")] 
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int maxArmor = 100;
    [SerializeField] private int startHealth = 100;
    [SerializeField] private int startArmor = 25;

    [Header("View")]
    [SerializeField] private Transform viewPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool lockCursorForLook = true;
    [SerializeField] private bool allow360VerticalLook = true;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Fly Skill")]
    [SerializeField] private float flyVerticalSpeed = 6f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.25f;
    [SerializeField] private float groundCheckSkin = 0.05f;

    [Header("Safety")]
    [SerializeField] private float fallResetY = -30f;
    [SerializeField] private Transform respawnPoint;

    [Header("Debug")]
    [SerializeField] private bool allowOfflineTesting = true;

    private readonly NetworkVariable<Vector3> networkPosition = new(
        writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Quaternion> networkRotation = new(
        writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> networkPitch = new(
        writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> health = new(
        writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> armor = new(
        writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isDead = new(
        writePerm: NetworkVariableWritePermission.Server);

    private CharacterController characterController;
    private float verticalVelocity;
    private float pitch;
    private float lastShootTime;
    private Vector3 lastSafeGroundedPosition;
    private float flyEndTime;
    private int currentAmmoInMagazine;
    private int currentReserveAmmo;
    private bool isReloading;
    private float reloadEndTime;

    public int CurrentHealth => health.Value;
    public int CurrentArmor => armor.Value;
    public bool IsDead => isDead.Value;
    public bool IsFlyActive => Time.time < flyEndTime;
    public int CurrentAmmoInMagazine => currentAmmoInMagazine;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public bool IsReloading => isReloading;
    public int DisplayHealth => health.Value > 0 ? health.Value : startHealth;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        lastSafeGroundedPosition = transform.position;
        currentAmmoInMagazine = Mathf.Max(1, magazineSize);
        currentReserveAmmo = Mathf.Max(0, startReserveAmmo);
    }

    private void Start()
    {
        if (lockCursorForLook)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        isDead.OnValueChanged += HandleDeadStateChanged;

        if (IsServer)
        {
            health.Value = Mathf.Clamp(startHealth, 1, maxHealth);
            armor.Value = Mathf.Clamp(startArmor, 0, maxArmor);
            isDead.Value = false;

            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
            networkPitch.Value = 0f;
        }

        if (!IsOwner && playerCamera != null)
        {
            playerCamera.enabled = false;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= HandleDeadStateChanged;
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            if (allowOfflineTesting)
            {
                HandleOwnerInput(false);
            }
            return;
        }

        if (IsOwner)
        {
            HandleOwnerInput(true);
        }
        else
        {
            InterpolateRemotePlayer();
        }
    }

    private void HandleOwnerInput(bool shouldSendNetworkRpc)
    {
        if (IsDead)
        {
            return;
        }

        if (isReloading && Time.time >= reloadEndTime)
        {
            FinishReload();
        }

        float mouseX = GetLookInput().x * lookSensitivity;
        float mouseY = GetLookInput().y * lookSensitivity;

        transform.Rotate(Vector3.up * mouseX);
        pitch -= mouseY;
        if (allow360VerticalLook)
        {
            pitch = Mathf.Repeat(pitch + 180f, 360f) - 180f;
        }
        else
        {
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        if (viewPivot != null)
        {
            viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector2 planarInput = GetMoveInput();
        Vector3 moveInput = (transform.right * planarInput.x) +
                            (transform.forward * planarInput.y);
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        bool isGrounded = IsGroundedReliable();
        if (IsFlyActive)
        {
            float flyInput = 0f;
            if (GetJumpHeld())
            {
                flyInput += 1f;
            }

            if (GetDescendHeld())
            {
                flyInput -= 1f;
            }

            verticalVelocity = flyInput * flyVerticalSpeed;
        }
        else if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (!IsFlyActive && GetJumpPressed() && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (!IsFlyActive)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 move = (moveInput * moveSpeed) + (Vector3.up * verticalVelocity);
        characterController.Move(move * Time.deltaTime);

        if (IsGroundedReliable())
        {
            lastSafeGroundedPosition = transform.position;
        }

        if (transform.position.y < fallResetY)
        {
            ResetToSafePosition(shouldSendNetworkRpc);
        }

        if (shouldSendNetworkRpc)
        {
            SubmitMovementServerRpc(transform.position, transform.rotation, pitch);
        }

        if (GetReloadPressed())
        {
            TryStartReload();
        }

        if (GetFireHeld() && Time.time >= lastShootTime + (1f / fireRate))
        {
            if (isReloading)
            {
                return;
            }

            if (!TryConsumeAmmoForShot())
            {
                TryStartReload();
                return;
            }

            lastShootTime = Time.time;

            Vector3 origin = playerCamera != null
                ? playerCamera.transform.position
                : transform.position + Vector3.up * 1.6f;
            Vector3 direction = playerCamera != null
                ? playerCamera.transform.forward
                : transform.forward;

            if (shouldSendNetworkRpc)
            {
                ShootServerRpc(origin, direction);
            }
        }
    }

    private Vector2 GetMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;

            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
#endif
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private Vector2 GetLookInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue() * 0.05f;
        }
#endif
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private bool GetJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetButtonDown("Jump");
#endif
    }

    private bool GetJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
        return Input.GetButton("Jump");
#endif
    }

    private bool GetDescendHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
#endif
    }

    private bool GetFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetButton("Fire1");
#endif
    }

    private bool GetReloadPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private bool IsGroundedReliable()
    {
        if (characterController.isGrounded)
        {
            return true;
        }

        float radius = Mathf.Max(0.05f, characterController.radius - groundCheckSkin);
        float castDistance = ((characterController.height * 0.5f) - characterController.radius) + groundCheckDistance;
        Vector3 castOrigin = transform.position + (Vector3.up * (characterController.radius + 0.05f));

        return Physics.SphereCast(
            castOrigin,
            radius,
            Vector3.down,
            out _,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);
    }

    private void ResetToSafePosition(bool shouldSendNetworkRpc)
    {
        Vector3 targetPosition = respawnPoint != null ? respawnPoint.position : lastSafeGroundedPosition;
        targetPosition.y += 0.1f;

        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;
        verticalVelocity = 0f;

        if (shouldSendNetworkRpc)
        {
            SubmitMovementServerRpc(transform.position, transform.rotation, pitch);
        }
    }

    private void InterpolateRemotePlayer()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            networkPosition.Value,
            12f * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            networkRotation.Value,
            12f * Time.deltaTime);

        if (viewPivot != null)
        {
            Quaternion remoteLook = Quaternion.Euler(networkPitch.Value, 0f, 0f);
            viewPivot.localRotation = Quaternion.Slerp(
                viewPivot.localRotation,
                remoteLook,
                16f * Time.deltaTime);
        }
    }

    [Rpc(SendTo.Server)]
    private void SubmitMovementServerRpc(Vector3 position, Quaternion rotation, float lookPitch)
    {
        networkPosition.Value = position;
        networkRotation.Value = rotation;
        networkPitch.Value = lookPitch;
    }

    [Rpc(SendTo.Server)]
    private void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        if (isDead.Value)
        {
            return;
        }

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, shootDistance, shootMask))
        {
            return;
        }

        ZombieBase zombie = hit.collider.GetComponentInParent<ZombieBase>();
        if (zombie != null && !zombie.IsDead)
        {
            zombie.TakeDamageFromServer(damagePerShot);
            return;
        }

        PlayerNetwork target = hit.collider.GetComponentInParent<PlayerNetwork>();
        if (target == null || target == this || target.IsDead)
        {
            return;
        }

        target.ApplyDamage(damagePerShot);
    }

    public void TakeDamageFromServer(int incomingDamage)
    {
        if (!IsServer)
        {
            return;
        }

        ApplyDamage(incomingDamage);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void HealServerRpc(int healAmount)
    {
        if (healAmount <= 0 || isDead.Value)
        {
            return;
        }

        health.Value = Mathf.Clamp(health.Value + healAmount, 0, maxHealth);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void AddArmorServerRpc(int armorAmount)
    {
        if (armorAmount <= 0 || isDead.Value)
        {
            return;
        }

        armor.Value = Mathf.Clamp(armor.Value + armorAmount, 0, maxArmor);
    }

    private void ApplyDamage(int incomingDamage)
    {
        if (!IsServer || incomingDamage <= 0 || isDead.Value)
        {
            return;
        }

        int remainingDamage = incomingDamage;

        if (armor.Value > 0)
        {
            int absorbed = Mathf.Min(armor.Value, remainingDamage);
            armor.Value -= absorbed;
            remainingDamage -= absorbed;
        }

        if (remainingDamage > 0)
        {
            health.Value = Mathf.Max(0, health.Value - remainingDamage);
        }

        if (health.Value <= 0)
        {
            isDead.Value = true;
        }
    }

    private void HandleDeadStateChanged(bool previousValue, bool newValue)
    {
        if (!previousValue && newValue && IsOwner)
        {
            Debug.Log("You died.");
        }
    }

    private bool TryConsumeAmmoForShot()
    {
        if (currentAmmoInMagazine <= 0)
        {
            return false;
        }

        currentAmmoInMagazine--;
        return true;
    }

    private void TryStartReload()
    {
        if (isReloading)
        {
            return;
        }

        if (currentAmmoInMagazine >= magazineSize || currentReserveAmmo <= 0)
        {
            return;
        }

        isReloading = true;
        reloadEndTime = Time.time + Mathf.Max(0.05f, reloadDuration);
    }

    private void FinishReload()
    {
        isReloading = false;

        int neededAmmo = Mathf.Max(0, magazineSize - currentAmmoInMagazine);
        if (neededAmmo <= 0 || currentReserveAmmo <= 0)
        {
            return;
        }

        int loaded = Mathf.Min(neededAmmo, currentReserveAmmo);
        currentAmmoInMagazine += loaded;
        currentReserveAmmo -= loaded;
    }

    public void AddWeaponDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        damagePerShot += amount;
    }

    public void AddReloadSpeed(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        reloadDuration = Mathf.Clamp(reloadDuration / (1f + amount), 0.25f, 5f);
    }

    public void AddMaxHealth(int amount, bool healToFull)
    {
        if (amount <= 0)
        {
            return;
        }

        maxHealth += amount;

        if (IsServer)
        {
            health.Value = healToFull
                ? maxHealth
                : Mathf.Clamp(health.Value + amount, 0, maxHealth);
        }
    }

    public void AddMoveSpeed(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        moveSpeed = Mathf.Clamp(moveSpeed + amount, 1f, 20f);
    }

    public void AddJumpHeight(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        jumpHeight = Mathf.Clamp(jumpHeight + amount, 0.5f, 10f);
    }

    public void ActivateFly(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        flyEndTime = Mathf.Max(flyEndTime, Time.time + durationSeconds);
    }
}
