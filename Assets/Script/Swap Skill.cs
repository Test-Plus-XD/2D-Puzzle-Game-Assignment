using System.Collections;
using UnityEngine;
using UnityEngine.UI;
//using static System.Net.Mime.MediaTypeNames;

/// Skill button that allows player to swap a topping with the next type clicked.
/// Features a cooldown timer with a circular slider indicator that fills clockwise.
public class SwapSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    [Tooltip("Number of times player can use this skill per game.")]
    public int skillCharges = 3;
    [Tooltip("Cooldown duration in seconds before skill can be used again.")]
    public float cooldownDuration = 10f;
    [Tooltip("Camera reference for screen-to-world conversion.")]
    public Camera mainCamera;
    [Tooltip("Layer mask for detecting toppings.")]
    public LayerMask toppingLayerMask;
    [Header("UI References")]
    [Tooltip("Button component for activating skill.")]
    public Button skillButton;
    [Tooltip("Text showing remaining charges.")]
    public Text chargesText;
    [Tooltip("Image component for the cooldown slider (circular fill).")]
    public Image cooldownSlider;
    [Tooltip("Image component for visual feedback (optional).")]
    public Image skillButtonImage;
    [Tooltip("Colour when skill is available.")]
    public Color availableColour = Color.white;
    [Tooltip("Colour when skill is active (waiting for selection).")]
    public Color activeColour = Color.yellow;
    [Tooltip("Colour when skill is on cooldown or depleted.")]
    public Color depletedColour = Color.grey;
    [Header("Audio")]
    [Tooltip("Sound played when skill is activated.")]
    public AudioClip activationSound;
    [Tooltip("Sound played when swap completes.")]
    public AudioClip swapSound;
    [Tooltip("Volume for skill sounds.")]
    public float soundVolume = 0.7f;
    [Header("Visual Feedback")]
    [Tooltip("Particle effect spawned on swapped toppings.")]
    public ParticleSystem swapParticlePrefab;
    private int remainingCharges;
    private bool skillActive = false;
    private bool onCooldown = false;
    private float cooldownTimeRemaining = 0f;
    private GameObject selectedTopping = null;
    private AudioSource audioSource;
    private Controller controller;

    private void Awake()
    {
        remainingCharges = skillCharges;
        if(mainCamera == null) mainCamera = Camera.main;
        controller = Object.FindFirstObjectByType<Controller>();
        audioSource = gameObject.GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        // Setup button listener
        if(skillButton != null)
        {
            skillButton.onClick.AddListener(ActivateSkill);
        }
        // Initialise cooldown slider to full (no cooldown at start)
        if(cooldownSlider != null)
        {
            cooldownSlider.fillAmount = 1f;
            cooldownSlider.type = Image.Type.Filled;
            cooldownSlider.fillMethod = Image.FillMethod.Radial360;
            cooldownSlider.fillOrigin = (int)Image.Origin360.Top;
            cooldownSlider.fillClockwise = true;
        }
        UpdateUI();
    }

    private void Update()
    {
        // Update cooldown timer if active
        if(onCooldown)
        {
            cooldownTimeRemaining -= Time.deltaTime;
            // Update slider fill amount (1 = full, 0 = empty)
            if(cooldownSlider != null)
            {
                float fillRatio = Mathf.Clamp01(cooldownTimeRemaining / cooldownDuration);
                // Invert so it fills clockwise from empty to full
                cooldownSlider.fillAmount = 1f - fillRatio;
            }
            // Check if cooldown finished
            if(cooldownTimeRemaining <= 0f)
            {
                onCooldown = false;
                cooldownTimeRemaining = 0f;
                if(cooldownSlider != null) cooldownSlider.fillAmount = 1f;
                UpdateUI();
            }
        }
        // Handle topping selection when skill is active
        if(skillActive)
        {
            // Check for input (mouse or touch)
            if(Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                Vector3 inputPosition = Input.mousePosition;
                if(Input.touchCount > 0) inputPosition = Input.GetTouch(0).position;
                Vector3 worldPosition = mainCamera.ScreenToWorldPoint(inputPosition);
                worldPosition.z = 0f;
                HandleToppingSelection(worldPosition);
            }
        }
    }

    // Activate skill when button is clicked
    public void ActivateSkill()
    {
        if(remainingCharges <= 0 || skillActive || onCooldown) return;
        skillActive = true;
        selectedTopping = null;
        // Disable player controller temporarily to prevent interference
        if(controller != null) controller.enabled = false;
        // Play activation sound
        if(activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound, soundVolume);
        }
        UpdateUI();
    }

    // Handle topping selection during skill activation
    private void HandleToppingSelection(Vector3 worldPosition)
    {
        // Detect topping at world position
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D hitCollider = Physics2D.OverlapPoint(point, toppingLayerMask);
        if(hitCollider == null) return;
        GameObject hitTopping = hitCollider.gameObject;
        // First selection stores the topping
        if(selectedTopping == null)
        {
            selectedTopping = hitTopping;
            // Visual feedback: highlight selected topping
            StartCoroutine(HighlightToppingCoroutine(selectedTopping));
            return;
        }
        // Second selection: swap all instances of the second topping's type with the selected first topping's type
        if(hitTopping != selectedTopping)
        {
            SwapAllInstancesWithSelected(selectedTopping, hitTopping);
            // Deduct charge
            remainingCharges--;
            // Start cooldown timer
            onCooldown = true;
            cooldownTimeRemaining = cooldownDuration;
            if(cooldownSlider != null) cooldownSlider.fillAmount = 0f;
            // Deactivate skill
            skillActive = false;
            selectedTopping = null;
            // Re-enable controller
            if(controller != null) controller.enabled = true;
            UpdateUI();
        }
    }

    // Swap all instances of toppingB's tag to toppingA's tag, then turn the single selected toppingA into toppingB.
    // After operation: former-B objects have tagA and A-sprite, selected A object has tagB and B-sprite.
    private void SwapAllInstancesWithSelected(GameObject toppingA, GameObject toppingB)
    {
        if(toppingA == null || toppingB == null) return;
        string tagA = toppingA.tag;
        string tagB = toppingB.tag;
        // If tags are identical no-op
        if(tagA == tagB) return;
        // Cache sprites for visual swap
        SpriteRenderer spriteRendererA = toppingA.GetComponent<SpriteRenderer>();
        SpriteRenderer spriteRendererB = toppingB.GetComponent<SpriteRenderer>();
        Sprite spriteA = spriteRendererA != null ? spriteRendererA.sprite : null;
        Sprite spriteB = spriteRendererB != null ? spriteRendererB.sprite : null;
        // Find all current B objects (capture list before modifying tags)
        GameObject[] allBObjects = GameObject.FindGameObjectsWithTag(tagB);
        // Change every B -> A (tag and sprite)
        foreach(GameObject bObject in allBObjects)
        {
            if(bObject == null) continue;
            // Skip if this object somehow is the selected A (rare) �X but normally it won't be.
            if(bObject == toppingA) continue;
            // Set tag to A type
            bObject.tag = tagA;
            // Update sprite to A's sprite if possible
            SpriteRenderer sr = bObject.GetComponent<SpriteRenderer>();
            if(sr != null && spriteA != null) sr.sprite = spriteA;
            // Spawn particle for visual feedback
            if(swapParticlePrefab != null) SpawnSwapParticle(bObject.transform.position);
            // If the topping has a component that stores its own type, try to update it
            // (This is defensive: many topping prefabs store type info; update if present)
            bObject.SendMessage("OnTypeChanged", tagA, SendMessageOptions.DontRequireReceiver);
        }
        // Now convert the originally selected A into B (single object)
        toppingA.tag = tagB;
        if(spriteRendererA != null && spriteB != null) spriteRendererA.sprite = spriteB;
        if(swapParticlePrefab != null) SpawnSwapParticle(toppingA.transform.position);
        toppingA.SendMessage("OnTypeChanged", tagB, SendMessageOptions.DontRequireReceiver);
        // Play swap sound once
        if(swapSound != null && audioSource != null) audioSource.PlayOneShot(swapSound, soundVolume);
    }

    // Spawn swap particle effect at position
    private void SpawnSwapParticle(Vector3 position)
    {
        if(swapParticlePrefab == null) return;
        ParticleSystem particle = Instantiate(swapParticlePrefab, position, Quaternion.identity);
        particle.Play();
        Destroy(particle.gameObject, particle.main.duration + particle.main.startLifetime.constantMax);
    }

    // Highlight topping briefly to show it's selected
    private IEnumerator HighlightToppingCoroutine(GameObject topping)
    {
        SpriteRenderer spriteRenderer = topping.GetComponent<SpriteRenderer>();
        if(spriteRenderer == null) yield break;
        Color originalColour = spriteRenderer.color;
        float duration = 0.5f;
        float elapsed = 0f;
        while(elapsed < duration && skillActive)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 4f, 1f);
            spriteRenderer.color = Color.Lerp(originalColour, Color.yellow, t * 0.5f);
            yield return null;
        }
        spriteRenderer.color = originalColour;
    }

    // Update UI elements to reflect current state
    private void UpdateUI()
    {
        // Update charges text
        if(chargesText != null)
        {
            chargesText.text = remainingCharges.ToString();
        }
        // Update button visual state
        if(skillButtonImage != null)
        {
            if(remainingCharges <= 0 || onCooldown)
            {
                skillButtonImage.color = depletedColour;
            } else if(skillActive)
            {
                skillButtonImage.color = activeColour;
            } else
            {
                skillButtonImage.color = availableColour;
            }
        }
        // Disable button if no charges remain or on cooldown
        if(skillButton != null)
        {
            skillButton.interactable = (remainingCharges > 0 && !skillActive && !onCooldown);
        }
    }

    // Reset skill charges and cooldown (for new game)
    public void ResetCharges()
    {
        remainingCharges = skillCharges;
        skillActive = false;
        selectedTopping = null;
        onCooldown = false;
        cooldownTimeRemaining = 0f;
        if(cooldownSlider != null) cooldownSlider.fillAmount = 1f;
        UpdateUI();
    }
}