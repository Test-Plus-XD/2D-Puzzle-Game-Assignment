using UnityEngine;

/// Handles the behaviour and visual explosion effect when a topping is removed.
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Topping : MonoBehaviour
{
    public ParticleSystem explosionParticlePrefab; // Prefab for explosion effect
    public float destroyDelay = 0.6f; // Time before topping object is destroyed
    public float scaleShrinkSpeed = 3f; // Speed of shrinking before destruction
    public AudioClip removeSound; // Optional sound to play on remove
    public float soundVolume = 0.6f; // Volume for sound playback

    private bool isRemoved = false; // Prevents duplicate remove calls
    private Rigidbody2D rigidBody; // Cached Rigidbody2D
    private Collider2D objectCollider; // Cached Collider2D
    private SpriteRenderer spriteRenderer; // Cached SpriteRenderer

    private void Awake()
    {
        // Cache components for performance
        rigidBody = GetComponent<Rigidbody2D>();
        objectCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Called externally by Logic.cs via SendMessage("Remove")
    public void Remove()
    {
        if(isRemoved) return; // Ignore repeated calls
        isRemoved = true;

        // Disable collider and gravity so it no longer interacts
        if(objectCollider != null) objectCollider.enabled = false;
        if(rigidBody != null)
        {
            rigidBody.simulated = false; // stop physics interaction
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
        }

        // Optional sound feedback
        if(removeSound != null)
        {
            AudioSource.PlayClipAtPoint(removeSound, transform.position, soundVolume);
        }

        // Spawn explosion particle (milk tea pop!)
        if(explosionParticlePrefab != null)
        {
            ParticleSystem explosion = Instantiate(explosionParticlePrefab, transform.position, Quaternion.identity);
            explosion.Play();
            Destroy(explosion.gameObject, explosion.main.duration + explosion.main.startLifetime.constantMax);
        }

        // Start coroutine to shrink and destroy object
        StartCoroutine(RemoveRoutine());
    }

    // Coroutine for shrinking and then destroying
    System.Collections.IEnumerator RemoveRoutine()
    {
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;

        while(elapsed < destroyDelay)
        {
            elapsed += Time.deltaTime;
            // Smoothly shrink over time
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed * scaleShrinkSpeed);
            // Optionally fade sprite out
            if(spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, elapsed / destroyDelay);
                spriteRenderer.color = c;
            }
            yield return null;
        }
        Destroy(gameObject); // Remove topping object from scene
    }
}