using UnityEngine;
using System.Collections;

/// Handles the behaviour and visual explosion effect when a topping is removed.
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Topping : MonoBehaviour
{
    [Header("Explosion Settings")]
    [Tooltip("Prefab that will be instantiated at the topping's position when removed. Can be any GameObject with animation or particle effects.")]
    public GameObject explosionPrefab;

    [Tooltip("Time before topping object is destroyed after being removed.")]
    public float destroyDelay = 0.6f;

    [Tooltip("Speed at which the topping shrinks before destruction.")]
    public float scaleShrinkSpeed = 3f;

    [Header("Audio Settings")]
    [Tooltip("Optional sound effect to play when the topping is removed.")]
    public AudioClip removeSound;

    [Tooltip("Volume of the removal sound effect.")]
    [Range(0f, 1f)]
    public float soundVolume = 0.6f;

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

        // Disable collider and physics
        if(objectCollider != null) objectCollider.enabled = false;
        if(rigidBody != null)
        {
            rigidBody.simulated = false; // Stop physics interaction
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
        }

        // Optional sound feedback
        if(removeSound != null)
            AudioSource.PlayClipAtPoint(removeSound, transform.position, soundVolume);

        // Spawn explosion prefab if assigned
        if(explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            // Automatically destroy the explosion prefab after its duration if it has one
            float lifetime = GetPrefabLifetime(explosion);
            Destroy(explosion, lifetime);
        }

        // Start coroutine to shrink and destroy this object
        StartCoroutine(RemoveRoutine());
    }

    // Determines a reasonable lifetime for explosion prefabs
    private float GetPrefabLifetime(GameObject explosion)
    {
        // Try to infer lifespan from Animator, ParticleSystem, or default fallback
        float lifetime = 1.0f;

        ParticleSystem particle = explosion.GetComponent<ParticleSystem>();
        if(particle != null)
        {
            var main = particle.main;
            lifetime = main.duration + main.startLifetime.constantMax;
        } else
        {
            Animator anim = explosion.GetComponent<Animator>();
            if(anim != null && anim.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
                if(clips.Length > 0) lifetime = clips[0].length;
            }
        }

        return Mathf.Max(0.1f, lifetime);
    }

    // Coroutine for shrinking and then destroying
    private IEnumerator RemoveRoutine()
    {
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;

        while(elapsed < destroyDelay)
        {
            elapsed += Time.deltaTime;

            // Smoothly shrink over time
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed * scaleShrinkSpeed);

            // Gradually fade sprite transparency
            if(spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, elapsed / destroyDelay);
                spriteRenderer.color = c;
            }

            yield return null;
        }

        // Destroy topping object from scene
        Destroy(gameObject);
    }
}