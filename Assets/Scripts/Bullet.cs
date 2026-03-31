using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 20f;
    public int damage = 1;
    public ParticleSystem impactEffect;
    public AudioClip impactSound;
    private Rigidbody rb;
    private AudioSource audioSource;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Configure the rigidbody for better collision handling
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Better collision detection
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Smoother movement
        }

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false; // Ensure the AudioSource does not play on start

        // Ignore collisions with all turrets
        GameObject[] turrets = GameObject.FindGameObjectsWithTag("Turret");
        Collider bulletCollider = GetComponent<Collider>();

        foreach (GameObject turret in turrets)
        {
            Collider[] turretColliders = turret.GetComponentsInChildren<Collider>();
            foreach (Collider turretCollider in turretColliders)
            {
                Physics.IgnoreCollision(bulletCollider, turretCollider);
            }
        }
    }

    void OnTriggerEnter(Collider hitInfo)
    {
        // Check if we hit wildlife
        Wildlife wildlife = hitInfo.GetComponent<Wildlife>();
        if (wildlife != null)
        {
            // Deal damage to the wildlife WITH bullet position for knockback
            wildlife.TakeDamage(damage, transform.position);
            Physics.IgnoreCollision(GetComponent<Collider>(), hitInfo); // Ignore collision forces
            SpawnImpactEffect(hitInfo);
            PlayImpactSound();
        }
        // Check if we hit the tower
        else if (hitInfo.CompareTag("Tower"))
        {
            // Just destroy the bullet without damaging the tower
            SpawnImpactEffect(hitInfo);
            PlayImpactSound();
        }
        // Check if we hit anything else
        else
        {
            // For any other collision, just spawn effects
            SpawnImpactEffect(hitInfo);
            PlayImpactSound();
        }

        // Destroy the bullet regardless of what it hit
        Destroy(gameObject);
    }

    void SpawnImpactEffect(Collider hitInfo)
    {
        if (impactEffect != null)
        {
            ParticleSystem effect = Instantiate(impactEffect, hitInfo.transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration);
        }
    }

    void PlayImpactSound()
    {
        if (impactSound != null && audioSource != null)
        {
            Debug.Log("Playing impact sound");
            audioSource.PlayOneShot(impactSound);
        }
        else
        {
            Debug.Log("Impact sound or audio source is null");
        }
    }
}