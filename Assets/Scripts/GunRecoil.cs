using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GunRecoil : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The transform that will be affected by recoil (usually the hand bone or weapon)")]
    public Transform recoilTarget;

    [Tooltip("Optional: The weapon model for additional effects")]
    public Transform weaponModel;

    [Header("Rotation Recoil")]
    [Tooltip("Maximum rotation for recoil effect")]
    public Vector3 recoilRotation = new Vector3(-20f, 5f, 15f);

    [Tooltip("Number of parent bones to affect (if using bone chain mode)")]
    [Range(1, 5)]
    public int affectedBoneCount = 3;

    [Tooltip("How much each parent bone's recoil is reduced (0-1)")]
    [Range(0f, 1f)]
    public float boneInfluenceFalloff = 0.5f;

    [Header("Position Recoil")]
    [Tooltip("Enable positional recoil (gun kicks back)")]
    public bool enablePositionRecoil = true;

    [Tooltip("Position-based recoil movement")]
    public Vector3 recoilPosition = new Vector3(0, 0, -0.1f);

    [Header("Timing")]
    [Tooltip("How quickly the recoil reaches its peak")]
    public float recoilSpeed = 10f;

    [Tooltip("How quickly the recoil returns to normal")]
    public float returnSpeed = 5f;

    [Header("Visual Effects")]
    [Tooltip("Add random variation to each recoil (0-1)")]
    [Range(0f, 1f)]
    public float randomizationFactor = 0.2f;

    [Tooltip("Enable weapon kickback visual effect")]
    public bool enableWeaponKickback = true;

    [Tooltip("Weapon kickback amount")]
    public float weaponKickbackAmount = 0.05f;

    [Tooltip("Add random shake during recoil")]
    [Range(0f, 1f)]
    public float shakeStrength = 0.05f;

    [Header("Debug")]
    public bool debugMode = false;
    public Color[] debugColors = new Color[] { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan };

    // Store original transforms
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalWeaponPos;
    private Quaternion originalWeaponRot;

    // For bone chain mode
    private List<Transform> boneChain = new List<Transform>();
    private List<Quaternion> originalRotations = new List<Quaternion>();

    // Recoil state
    private bool isInRecoil = false;
    private float currentRecoil = 0f;
    private Vector3 currentRecoilRot = Vector3.zero;
    private Vector3 currentRecoilPos = Vector3.zero;
    private Vector3 targetRecoilRot;
    private Vector3 targetRecoilPos;

    // Public property to access recoil amount
    public float CurrentRecoilAmount { get { return currentRecoil; } }

    void Start()
    {
        // Check if recoil target is assigned
        if (recoilTarget == null)
        {
            // Try to use the firePoint from PlayerController
            PlayerController playerController = GetComponent<PlayerController>();
            if (playerController != null && playerController.firePoint != null)
            {
                recoilTarget = playerController.firePoint;
                if (debugMode) Debug.Log($"Auto-assigned recoil target to firePoint: {recoilTarget.name}");
            }
            else
            {
                // Try to find the hand/weapon transform
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name.Contains("Hand_R") || child.name.Contains("Hand_R_end") ||
                        child.name.Contains("Weapon") || child.name.Contains("Gun") ||
                        child.name.Contains("Rifle") || child.name.Contains("firePoint"))
                    {
                        recoilTarget = child;
                        if (debugMode) Debug.Log($"Auto-assigned recoil target to: {child.name}");
                        break;
                    }
                }
            }

            if (recoilTarget == null)
            {
                Debug.LogWarning("No recoil target assigned! Please assign a transform in the inspector.");
                recoilTarget = transform; // Fallback to player transform
            }
        }

        // Store original transforms
        if (recoilTarget != null)
        {
            originalLocalPos = recoilTarget.localPosition;
            originalLocalRot = recoilTarget.localRotation;
        }

        if (weaponModel != null)
        {
            originalWeaponPos = weaponModel.localPosition;
            originalWeaponRot = weaponModel.localRotation;
        }

        // Initialize bone chain if we have more than one affected bone
        if (affectedBoneCount > 1 && recoilTarget != null)
        {
            InitializeBoneChain();
        }
    }

    void InitializeBoneChain()
    {
        boneChain.Clear();
        originalRotations.Clear();

        // Start with the assigned target
        Transform currentBone = recoilTarget;

        // Add bones to the chain
        for (int i = 0; i < affectedBoneCount && currentBone != null; i++)
        {
            boneChain.Add(currentBone);
            originalRotations.Add(currentBone.localRotation);

            // Move to parent bone
            if (currentBone.parent != null && currentBone.parent != transform)
            {
                currentBone = currentBone.parent;
            }
            else
            {
                // Break if we reach the root or the player transform
                break;
            }
        }

        if (debugMode)
        {
            Debug.Log($"Initialized recoil bone chain with {boneChain.Count} bones.");
            for (int i = 0; i < boneChain.Count; i++)
            {
                Debug.Log($"Bone {i}: {boneChain[i].name}");
            }
        }
    }

    void Update()
    {
        // Example: Manually trigger recoil with R key (for testing)
        if (Input.GetKeyDown(KeyCode.R))
        {
            TriggerRecoil();
        }

        // Update recoil animation
        UpdateRecoilAnimation();
    }

    void UpdateRecoilAnimation()
    {
        if (recoilTarget == null) return;

        if (isInRecoil)
        {
            // Increase recoil amount
            currentRecoil = Mathf.Clamp01(currentRecoil + Time.deltaTime * recoilSpeed);

            // Apply recoil
            if (affectedBoneCount > 1 && boneChain.Count > 0)
            {
                // Bone chain mode
                ApplyRecoilToBoneChain();
            }
            else
            {
                // Single transform mode
                ApplyRecoilToTransform();
            }

            // Add shake
            if (shakeStrength > 0)
            {
                ApplyRecoilShake();
            }

            // Check if reached max recoil
            if (currentRecoil >= 1f)
            {
                isInRecoil = false;
            }
        }
        else if (currentRecoil > 0)
        {
            // Return to original position
            currentRecoil = Mathf.Clamp01(currentRecoil - Time.deltaTime * returnSpeed);

            // Apply interpolated recoil
            if (affectedBoneCount > 1 && boneChain.Count > 0)
            {
                ApplyRecoilToBoneChain();
            }
            else
            {
                ApplyRecoilToTransform();
            }
        }
        else if (currentRecoil <= 0)
        {
            // Ensure we're exactly at original position when done
            ResetToOriginal();
        }
    }

    void ApplyRecoilToBoneChain()
    {
        for (int i = 0; i < boneChain.Count; i++)
        {
            if (boneChain[i] == null) continue;

            // Calculate influence factor based on bone position in chain
            float influenceFactor = Mathf.Pow(1f - boneInfluenceFalloff, i);

            // Apply rotation recoil with falloff
            Vector3 boneRecoil = Vector3.Scale(targetRecoilRot, new Vector3(
                currentRecoil * influenceFactor,
                currentRecoil * influenceFactor,
                currentRecoil * influenceFactor
            ));

            boneChain[i].localRotation = originalRotations[i] * Quaternion.Euler(boneRecoil);

            // Apply position recoil only to the first bone (usually the hand/weapon)
            if (i == 0 && enablePositionRecoil)
            {
                Vector3 posRecoil = Vector3.Scale(targetRecoilPos, new Vector3(
                    currentRecoil,
                    currentRecoil,
                    currentRecoil
                ));

                boneChain[i].localPosition = originalLocalPos + boneChain[i].TransformVector(posRecoil);
            }
        }

        // Apply weapon kickback if enabled
        if (enableWeaponKickback && weaponModel != null)
        {
            float kickbackAmount = currentRecoil * weaponKickbackAmount;
            weaponModel.localPosition = originalWeaponPos - new Vector3(0, 0, kickbackAmount);
        }
    }

    void ApplyRecoilToTransform()
    {
        // Calculate current rotation based on recoil amount
        currentRecoilRot = Vector3.Scale(targetRecoilRot, new Vector3(currentRecoil, currentRecoil, currentRecoil));

        // Apply rotation
        recoilTarget.localRotation = originalLocalRot * Quaternion.Euler(currentRecoilRot);

        // Apply position recoil if enabled
        if (enablePositionRecoil)
        {
            currentRecoilPos = Vector3.Scale(targetRecoilPos, new Vector3(currentRecoil, currentRecoil, currentRecoil));
            recoilTarget.localPosition = originalLocalPos + recoilTarget.TransformVector(currentRecoilPos);
        }

        // Apply weapon kickback if enabled
        if (enableWeaponKickback && weaponModel != null)
        {
            float kickbackAmount = currentRecoil * weaponKickbackAmount;
            weaponModel.localPosition = originalWeaponPos - new Vector3(0, 0, kickbackAmount);
        }
    }

    void ApplyRecoilShake()
    {
        if (recoilTarget == null) return;

        // Add random shake
        Vector3 shake = new Vector3(
            Random.Range(-1f, 1f) * shakeStrength,
            Random.Range(-1f, 1f) * shakeStrength,
            Random.Range(-1f, 1f) * shakeStrength
        );

        // Apply shake to rotation
        recoilTarget.localRotation = recoilTarget.localRotation * Quaternion.Euler(shake);
    }

    void ResetToOriginal()
    {
        // Reset position and rotation to original values
        if (recoilTarget != null)
        {
            if (enablePositionRecoil)
            {
                recoilTarget.localPosition = originalLocalPos;
            }
            recoilTarget.localRotation = originalLocalRot;
        }

        if (weaponModel != null)
        {
            weaponModel.localPosition = originalWeaponPos;
            weaponModel.localRotation = originalWeaponRot;
        }

        // For bone chain mode, reset all bones
        if (affectedBoneCount > 1 && boneChain.Count > 0)
        {
            for (int i = 0; i < boneChain.Count; i++)
            {
                if (boneChain[i] != null)
                {
                    boneChain[i].localRotation = originalRotations[i];
                }
            }
        }
    }

    // Call this method when the gun fires
    public void TriggerRecoil()
    {
        if (recoilTarget == null) return;

        // Generate random variation in recoil
        targetRecoilRot = new Vector3(
            recoilRotation.x * (1f + Random.Range(-randomizationFactor, randomizationFactor)),
            recoilRotation.y * (1f + Random.Range(-randomizationFactor, randomizationFactor)),
            recoilRotation.z * (1f + Random.Range(-randomizationFactor, randomizationFactor))
        );

        // Generate position recoil
        targetRecoilPos = new Vector3(
            recoilPosition.x * (1f + Random.Range(-randomizationFactor, randomizationFactor)),
            recoilPosition.y * (1f + Random.Range(-randomizationFactor, randomizationFactor)),
            recoilPosition.z * (1f + Random.Range(-randomizationFactor, randomizationFactor))
        );

        // Start recoil
        isInRecoil = true;
        currentRecoil = 0f;

        if (debugMode)
        {
            Debug.Log($"Triggered recoil with rotation: {targetRecoilRot} and position: {targetRecoilPos}");
        }
    }

    // Refresh bone chain (call if bone hierarchy changes)
    public void RefreshBoneChain()
    {
        if (affectedBoneCount > 1)
        {
            InitializeBoneChain();
        }
    }

    // Visualize the bone chain in the editor
    private void OnDrawGizmos()
    {
        if (!debugMode || !Application.isPlaying) return;

        // Draw the recoil target
        if (recoilTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(recoilTarget.position, 0.03f);
            Gizmos.DrawRay(recoilTarget.position, recoilTarget.forward * 0.5f);
        }

        // Draw the bone chain if using it
        if (affectedBoneCount > 1 && boneChain != null && boneChain.Count > 0)
        {
            for (int i = 0; i < boneChain.Count; i++)
            {
                if (boneChain[i] == null) continue;

                // Draw a sphere at each bone position with different colors
                Gizmos.color = debugColors[i % debugColors.Length];
                Gizmos.DrawSphere(boneChain[i].position, 0.025f);

                // Draw lines connecting the bones
                if (i < boneChain.Count - 1 && boneChain[i + 1] != null)
                {
                    Gizmos.DrawLine(boneChain[i].position, boneChain[i + 1].position);
                }
            }
        }
    }
}