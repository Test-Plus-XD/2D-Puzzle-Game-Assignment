using System.Collections;
using UnityEngine;

/// Special topping that enables dual-type connection for the next chain.
/// When connected, allows player to connect two different topping types together.
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class DualConnect : MonoBehaviour
{
    [Header("Visual Feedback")]
    [Tooltip("Particle effect prefab for removal animation.")]
    public ParticleSystem activationParticlePrefab;
    [Tooltip("Time before topping object is destroyed.")]
    public float destroyDelay = 0.6f;
    [Tooltip("Speed of scale shrinking animation.")]
    public float scaleShrinkSpeed = 3f;
    [Header("Audio")]
    [Tooltip("Sound played when dual connect activates.")]
    public AudioClip activationSound;
    [Tooltip("Volume for activation sound.")]
    public float activationVolume = 0.7f;
    [Header("Indicator Animation")]
    [Tooltip("Enable rainbow colour cycling to indicate special ability.")]
    public bool enableColourCycle = true;
    [Tooltip("Speed of colour cycling animation.")]
    public float colourCycleSpeed = 1.5f;
    private bool isActivated = false;
    private Rigidbody2D rigidBody;
    private Collider2D objectCollider;
    private SpriteRenderer spriteRenderer;
    private Color originalColour;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        objectCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColour = spriteRenderer.color;
    }

    private void Start()
    {
        // Start colour cycling animation if enabled
        if (enableColourCycle && spriteRenderer != null)
        {
            StartCoroutine(ColourCycleCoroutine());
        }
    }

    /// Called by Logic.cs when this topping is connected in a chain
    public void Remove()
    {
        if (isActivated) return;
        isActivated = true;
        // Activate dual connect ability
        StartCoroutine(ActivateCoroutine());
    }

    // Colour cycling animation to indicate special topping
    private IEnumerator ColourCycleCoroutine()
    {
        while (!isActivated)
        {
            float hue = (Time.time * colourCycleSpeed) % 1f;
            Color rainbowColour = Color.HSVToRGB(hue, 0.7f, 1f);
            // Blend with original colour
            spriteRenderer.color = Color.Lerp(originalColour, rainbowColour, 0.5f);
            yield return null;
        }
    }

    // Activation coroutine: play effects and notify Logic
    private IEnumerator ActivateCoroutine()
    {
        // Disable physics
        if (objectCollider != null) objectCollider.enabled = false;
        if (rigidBody != null)
        {
            rigidBody.simulated = false;
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
        }
        // Play activation sound
        if (activationSound != null)
        {
            AudioSource.PlayClipAtPoint(activationSound, transform.position, activationVolume);
        }
        // Spawn activation particle effect
        if (activationParticlePrefab != null)
        {
            ParticleSystem activation = Instantiate(activationParticlePrefab, transform.position, Quaternion.identity);
            activation.Play();
            Destroy(activation.gameObject, activation.main.duration + activation.main.startLifetime.constantMax);
        }
        // Show popup notification to player
        if (PopUp.Instance != null)
        {
            // You could create a custom sprite for "DUAL CONNECT!" message
            PopUp.Instance.ShowComboGreat(transform.position);
        }
        // Shrink and destroy
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;
        while (elapsed < destroyDelay)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed * scaleShrinkSpeed);
            // Flash colour brightly then fade
            if (spriteRenderer != null)
            {
                Color colour = Color.white;
                colour.a = Mathf.Lerp(1f, 0f, elapsed / destroyDelay);
                spriteRenderer.color = colour;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}