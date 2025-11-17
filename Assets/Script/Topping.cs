using UnityEngine;
using System.Collections;

/// Handles the behaviour and visual explosion effect when a topping is removed.
/// Also contains sticky behaviour: toppings stick to the ladle collider while it is enabled
/// and are released when the ladle collider is disabled (or when ReleaseAll is called).
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

    [Header("Sticky Behaviour")]
    [Tooltip("Whether this topping is allowed to stick to the ladle collider.")]
    public bool allowStick = true;
    [Tooltip("Linear velocity applied to the topping when it un-sticks (so it falls away).")]
    public Vector2 unstickLinearVelocity = new Vector2(0f, -1.5f);

    // Internal state
    private bool isRemoved = false; // Prevents duplicate remove calls
    private Rigidbody2D rigidBody; // Cached Rigidbody2D
    private Collider2D objectCollider; // Cached Collider2D
    private SpriteRenderer spriteRenderer; // Cached SpriteRenderer

    // Sticky internal state
    private Transform originalParent; // Parent to restore when un-sticking
    private bool isStuck = false; // Whether this topping is currently stuck to the ladle

    private void Awake()
    {
        // Cache components for performance
        rigidBody = GetComponent<Rigidbody2D>();
        objectCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Store original parent to restore when un-sticking
        originalParent = transform.parent;
    }

    // Called externally by Logic.cs via SendMessage("Remove")
    public void Remove()
    {
        if(isRemoved) return; // Ignore repeated calls
        isRemoved = true;

        // If stuck, mark un-stuck logically; physical removal flow will still perform visual removal
        if(isStuck)
        {
            // Restore parent before removal so explosion spawns at correct world position
            transform.SetParent(originalParent, worldPositionStays: true);
            isStuck = false;
        }

        // Disable collider and physics
        if(objectCollider != null) objectCollider.enabled = false;
        if(rigidBody != null)
        {
            // Stop physics interaction
            rigidBody.simulated = false;
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
        // Try to infer lifespan from ParticleSystem, Animator, or default fallback
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

    // Collision handler to implement sticky behaviour with the ladle collider
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Guard against disabled behaviour or already stuck
        if(!allowStick || isStuck) return;

        // Ensure Spawner instance exists and provides the ladle collider reference
        if(Spawner.Instance == null) return;

        Collider2D ladleCollider = Spawner.Instance.ladleEdgeCollider;
        if(ladleCollider == null) return;

        // Stick only when colliding with the specific ladle collider while it is enabled
        if(collision.collider == ladleCollider && ladleCollider.enabled)
        {
            StickToLadle();
        }
    }

    // Make the topping "stick" to the ladle: disable physics simulation and parent to ladle transform
    public void StickToLadle()
    {
        if(isStuck || !allowStick) return;

        if(rigidBody != null)
        {
            // Stop physics simulation so the topping follows the ladle cleanly
            rigidBody.simulated = false;
            // Clear any velocity to avoid visual jitter
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
        }

        // Parent to ladle transform so topping moves with ladle animation
        if(Spawner.Instance != null)
        {
            transform.SetParent(Spawner.Instance.transform, worldPositionStays: true);
        }

        isStuck = true;
    }

    // Restore the topping to normal physics and unparent from the ladle
    public void Unstick()
    {
        if(!isStuck) return;

        // Restore parent
        transform.SetParent(originalParent, worldPositionStays: true);

        // Re-enable physics
        if(rigidBody != null)
        {
            rigidBody.simulated = true;
            // Give small downward velocity so the topping falls away smoothly
            rigidBody.linearVelocity = unstickLinearVelocity;
        }

        isStuck = false;
    }

    // Static helper to find all Topping instances in the scene and unstick them
    public static void ReleaseAll()
    {
        Topping[] all = FindObjectsOfType<Topping>();
        for(int i = 0; i < all.Length; i++)
        {
            if(all[i] != null)
            {
                all[i].Unstick();
            }
        }
    }
}