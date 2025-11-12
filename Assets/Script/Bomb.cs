using System.Collections;
using UnityEngine;

/// Special topping that explodes when connected, clearing all toppings within radius.
/// Inherits from Topping to maintain base behaviour.
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Bomb : MonoBehaviour
{
    [Header("Explosion Settings")]
    [Tooltip("Radius in world units for explosion effect.")]
    public float explosionRadius = 2f;
    [Tooltip("Layer mask for toppings that can be cleared by explosion.")]
    public LayerMask toppingLayerMask;
    [Tooltip("Bonus points awarded per topping cleared by explosion.")]
    public int bonusPointsPerTopping = 5;
    [Header("Visual Effects")]
    [Tooltip("Particle system prefab for explosion visual.")]
    public ParticleSystem explosionParticlePrefab;
    [Tooltip("Time before bomb object is destroyed after explosion.")]
    public float destroyDelay = 0.8f;
    [Tooltip("Speed of scale shrinking animation.")]
    public float scaleShrinkSpeed = 3f;
    [Header("Audio")]
    [Tooltip("Sound played when bomb explodes.")]
    public AudioClip explosionSound;
    [Tooltip("Volume for explosion sound.")]
    public float explosionVolume = 0.8f;
    [Header("Warning Animation")]
    [Tooltip("Enable pulsing animation to warn player this is a bomb.")]
    public bool enableWarningPulse = true;
    [Tooltip("Speed of warning pulse animation.")]
    public float pulseSpeed = 2f;
    [Tooltip("Scale multiplier for pulse animation.")]
    public float pulseScale = 1.2f;
    private bool isExploded = false;
    private Rigidbody2D rigidBody;
    private Collider2D objectCollider;
    private SpriteRenderer spriteRenderer;
    private Vector3 initialScale;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        objectCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialScale = transform.localScale;
    }

    private void Start()
    {
        // Start warning pulse animation if enabled
        if (enableWarningPulse)
        {
            StartCoroutine(WarningPulseCoroutine());
        }
    }

    /// Called by Logic.cs when this topping is included in a chain
    public void Remove()
    {
        if (isExploded) return;
        isExploded = true;
        // Trigger explosion
        StartCoroutine(ExplodeCoroutine());
    }

    // Pulsing animation to indicate bomb status
    private IEnumerator WarningPulseCoroutine()
    {
        while (!isExploded)
        {
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f);
            transform.localScale = initialScale * scale;
            yield return null;
        }
    }

    // Explosion coroutine: find nearby toppings, clear them, award bonus points
    private IEnumerator ExplodeCoroutine()
    {
        // Disable physics interaction immediately
        if (objectCollider != null) objectCollider.enabled = false;
        if (rigidBody != null)
        {
            rigidBody.simulated = false;
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
        }
        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }
        // Spawn explosion particle effect
        if (explosionParticlePrefab != null)
        {
            ParticleSystem explosion = Instantiate(explosionParticlePrefab, transform.position, Quaternion.identity);
            explosion.transform.localScale = Vector3.one * (explosionRadius / 1f);
            explosion.Play();
            Destroy(explosion.gameObject, explosion.main.duration + explosion.main.startLifetime.constantMax);
        }
        // Find all toppings within explosion radius
        Vector2 origin = transform.position;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(origin, explosionRadius, toppingLayerMask);
        int toppingsCleared = 0;
        foreach (Collider2D hitCollider in hitColliders)
        {
            if (hitCollider == null || hitCollider.gameObject == this.gameObject) continue;
            // Send Remove message to trigger their removal animation
            hitCollider.gameObject.SendMessage("Remove", SendMessageOptions.DontRequireReceiver);
            toppingsCleared++;
            // Small delay between each removal for visual effect
            yield return new WaitForSeconds(0.05f);
        }
        // Award bonus points for explosion clears
        if (ScoreManager.Instance != null && toppingsCleared > 0)
        {
            int bonusPoints = toppingsCleared * bonusPointsPerTopping;
            ScoreManager.Instance.AddBonusPoints(bonusPoints);
        }
        // Shrink and destroy bomb object
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        while (elapsed < destroyDelay)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed * scaleShrinkSpeed);
            // Fade sprite
            if (spriteRenderer != null)
            {
                Color colour = spriteRenderer.color;
                colour.a = Mathf.Lerp(1f, 0f, elapsed / destroyDelay);
                spriteRenderer.color = colour;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // Visualize explosion radius in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}