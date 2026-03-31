using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 15f;  // Increased for more responsive rotation
    public bool useCharacterController = true;

    [Header("Shooting Settings")]
    public float fireRate = 0.5f;
    public GameObject bulletPrefab;
    public Transform firePoint;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] gunshotSounds;
    public AudioClip[] footstepSounds;
    public float footstepInterval = 0.4f;
    public float pitchVariation = 0.1f;

    [Header("Visual Effects")]
    public ParticleSystem muzzleFlash;

    [Header("Animation")]
    public Animator animator;
    public string speedParameter = "Speed";
    public string isWalkingParameter = "IsWalking";
    public string isIdleParameter = "IsIdle";
    public string shootTriggerParameter = "Shoot";

    // New animation parameters
    public string isWalkingFwdParameter = "IsWalkingFwd";
    public string isWalkingBwdParameter = "IsWalkingBwd";
    public string isWalkingLeftParameter = "IsWalkingLeft";
    public string isWalkingRightParameter = "IsWalkingRight";

    // Private variables
    private float nextFireTime = 0f;
    private float lastFootstepTime = 0f;
    private CharacterController characterController;
    private Rigidbody rb;
    private TurretManager turretManager;
    private bool isGamePaused = false;

    // Reference to the recoil system
    private GunRecoil gunRecoil;

    void Start()
    {
        // Find the TurretManager in the scene
        turretManager = FindObjectOfType<TurretManager>();
        if (turretManager == null)
        {
            Debug.LogWarning("TurretManager not found. Player shooting won't coordinate with turret placement.");
        }

        // Get components based on movement type
        if (useCharacterController)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                Debug.Log("Added CharacterController component to player");
            }
        }
        else
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;  // Prevent unwanted rotation
                Debug.Log("Added Rigidbody component to player");
            }
        }

        // Try to get animator if not already assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("Animator component not found on player or its children!");
            }
            else
            {
                Debug.Log("Found Animator component on child object: " + animator.gameObject.name);
            }
        }

        // Get reference to GunRecoil component
        gunRecoil = GetComponent<GunRecoil>();
        if (gunRecoil == null)
        {
            gunRecoil = gameObject.AddComponent<GunRecoil>();
            Debug.Log("Added GunRecoil component to player");
        }
    }

    void Update()
    {
        // Check if game is paused by checking timeScale
        isGamePaused = (Time.timeScale == 0f);

        // Only process input and movement if game is not paused
        if (!isGamePaused)
        {
            // Handle rotation - do this first to ensure proper aim
            RotateTowardsMouse();

            // Handle movement input and apply movement
            HandleMovement();

            // Handle shooting
            HandleShooting();
        }
    }

    void HandleMovement()
    {
        // Get input axes
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Create movement vector (in world space)
        Vector3 moveDir = new Vector3(horizontal, 0f, vertical).normalized;

        // Apply movement
        if (useCharacterController && characterController != null)
        {
            // Add gravity for CharacterController
            if (!characterController.isGrounded)
            {
                moveDir.y = -9.81f * Time.deltaTime;
            }
            else
            {
                moveDir.y = -0.5f;  // Small downward force to keep grounded
            }

            // Apply the movement
            characterController.Move(moveDir * moveSpeed * Time.deltaTime);
        }
        else if (rb != null)
        {
            // For rigidbody, apply velocity directly
            Vector3 targetVelocity = moveDir * moveSpeed;
            targetVelocity.y = rb.linearVelocity.y;  // Preserve vertical velocity
            rb.linearVelocity = targetVelocity;
        }

        // Play footstep sounds if moving
        if ((horizontal != 0f || vertical != 0f) && Time.time > lastFootstepTime + footstepInterval)
        {
            PlayFootstepSound();
            lastFootstepTime = Time.time;
        }

        // Update animator with movement values
        if (animator != null)
        {
            // Check if player is moving
            bool isMoving = (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f);

            // Basic animation state
            animator.SetBool(isWalkingParameter, isMoving);
            animator.SetBool(isIdleParameter, !isMoving);
            animator.SetFloat(speedParameter, moveDir.magnitude);

            // Directional animation states - with input relative to player's forward
            Vector3 localMoveDir = transform.InverseTransformDirection(moveDir);

            // Set boolean parameters for specific direction animations
            animator.SetBool(isWalkingFwdParameter, localMoveDir.z > 0.7f);
            animator.SetBool(isWalkingBwdParameter, localMoveDir.z < -0.7f);
            animator.SetBool(isWalkingLeftParameter, localMoveDir.x < -0.7f);
            animator.SetBool(isWalkingRightParameter, localMoveDir.x > 0.7f);
        }
    }

    void RotateTowardsMouse()
    {
        // Skip if paused
        if (isGamePaused) return;

        // Get mouse position in screen space
        Vector3 mousePos = Input.mousePosition;

        // Create a ray from the camera through the mouse position
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // Create a plane at the player's height
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        // Try to find where the ray intersects the plane
        if (groundPlane.Raycast(ray, out float hitDistance))
        {
            // Get the point on the plane that the ray hits
            Vector3 targetPoint = ray.GetPoint(hitDistance);

            // Debug visualization
            Debug.DrawLine(transform.position, targetPoint, Color.red);

            // Get direction to look at (ignoring Y axis)
            Vector3 lookDirection = targetPoint - transform.position;
            lookDirection.y = 0f;

            // Only rotate if we have a valid direction
            if (lookDirection != Vector3.zero)
            {
                // Create rotation to look at target point
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

                // Apply rotation with smooth damping
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
            }
        }
    }

    void HandleShooting()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            // Check conditions before shooting
            bool mouseOverUI = EventSystem.current.IsPointerOverGameObject();
            bool turretInPlacementMode = turretManager != null && turretManager.IsPlacingTurret();

            if (!mouseOverUI && !turretInPlacementMode)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    void Shoot()
    {
        // Skip shooting if game is paused
        if (isGamePaused)
            return;

        // Instantiate bullet
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // Set bullet velocity
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = firePoint.forward * 20f;
        }

        // Try to get TurretBullet component (from your custom scripts)
        TurretBullet turretBullet = bullet.GetComponent<TurretBullet>();
        if (turretBullet != null)
        {
            // Your TurretBullet script seems to set velocity in Start()
            // No need to do anything else
        }
        else
        {
            // Try to get regular Bullet component
            Bullet bulletComponent = bullet.GetComponent<Bullet>();
            if (bulletComponent != null)
            {
                // Your Bullet script also sets velocity in Start()
                // No need to do anything else
            }
        }

        // Trigger recoil effect
        if (gunRecoil != null)
        {
            gunRecoil.TriggerRecoil();
        }

        // Play gunshot sound
        PlayGunshotSound();

        // Play muzzle flash
        PlayMuzzleFlash();

        // Trigger shoot animation if animator exists
        if (animator != null)
        {
            animator.SetTrigger(shootTriggerParameter);
        }
    }

    void PlayGunshotSound()
    {
        if (gunshotSounds != null && gunshotSounds.Length > 0 && audioSource != null)
        {
            int index = Random.Range(0, gunshotSounds.Length);
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(gunshotSounds[index]);
        }
    }

    void PlayFootstepSound()
    {
        if (footstepSounds != null && footstepSounds.Length > 0 && audioSource != null)
        {
            int index = Random.Range(0, footstepSounds.Length);

            // Play at lower volume to avoid overwhelming gunshots
            audioSource.pitch = 1f + Random.Range(-pitchVariation / 2, pitchVariation / 2);
            audioSource.PlayOneShot(footstepSounds[index], 0.5f);
        }
    }

    void PlayMuzzleFlash()
    {
        if (muzzleFlash != null)
        {
            ParticleSystem flash = Instantiate(muzzleFlash, firePoint.position, firePoint.rotation);
            flash.Play();
            Destroy(flash.gameObject, flash.main.duration);
        }
    }
}