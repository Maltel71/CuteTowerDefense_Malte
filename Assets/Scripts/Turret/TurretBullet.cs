using UnityEngine;

public class TurretBullet : MonoBehaviour
{
    public float speed = 20f;
    public int damage = 2;
    public ParticleSystem impactEffect;
    public AudioClip impactSound;
    public float lifetime = 3f;

    private Rigidbody rb;
    private AudioSource audioSource;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Set initial velocity
            rb.linearVelocity = transform.forward * speed;
        }

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.playOnAwake = false;
        }

        // Destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter(Collider hitInfo)
    {
        // Check if we hit wildlife
        Wildlife wildlife = hitInfo.GetComponent<Wildlife>();
        if (wildlife != null)
        {
            // Apply damage with bullet position for knockback
            wildlife.TakeDamage(damage, transform.position);

            // Skip collision physics
            Physics.IgnoreCollision(GetComponent<Collider>(), hitInfo);

            // Play effects
            PlayImpactEffect(hitInfo.transform.position);
            PlayImpactSound();

            // Destroy bullet
            Destroy(gameObject);
        }
    }

    void PlayImpactEffect(Vector3 position)
    {
        if (impactEffect != null)
        {
            // Instantiate at the contact point and play immediately
            ParticleSystem effect = Instantiate(impactEffect, position, Quaternion.identity);

            // Ensure it's playing
            if (!effect.isPlaying)
            {
                effect.Play();
            }

            // Get the duration based on particle settings
            float duration = effect.main.duration;
            if (effect.main.loop)
            {
                duration = 2.0f; // Default duration for looping particles
            }

            // Add the max start lifetime to ensure all particles are shown
            float totalLifetime = duration + effect.main.startLifetime.constantMax;

            // Clean up effect after it finishes
            Destroy(effect.gameObject, totalLifetime);

            Debug.Log($"Bullet impact effect playing at {position}, duration: {totalLifetime}s");
        }
        else
        {
            Debug.LogWarning("No impact effect assigned to bullet");
        }
    }

    void PlayImpactSound()
    {
        if (impactSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(impactSound);
            Debug.Log("Playing bullet impact sound");
        }
        else
        {
            Debug.LogWarning("No impact sound assigned to bullet or audio source missing");
        }
    }
}