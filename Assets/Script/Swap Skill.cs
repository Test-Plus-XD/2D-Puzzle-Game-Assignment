using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// Skill button that allows player to swap a topping with the next type clicked.
/// Player clicks button to activate, then clicks two toppings to swap their types.
public class ToppingSwapSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    [Tooltip("Number of times player can use this skill per game.")]
    public int skillCharges = 3;
    [Tooltip("Camera reference for screen-to-world conversion.")]
    public Camera mainCamera;
    [Tooltip("Layer mask for detecting toppings.")]
    public LayerMask toppingLayerMask;
    [Header("UI References")]
    [Tooltip("Button component for activating skill.")]
    public Button skillButton;
    [Tooltip("Text showing remaining charges.")]
    public Text chargesText;
    [Tooltip("Image component for visual feedback (optional).")]
    public Image skillButtonImage;
    [Tooltip("Colour when skill is available.")]
    public Color availableColour = Color.white;
    [Tooltip("Colour when skill is active (waiting for selection).")]
    public Color activeColour = Color.yellow;
    [Tooltip("Colour when skill has no charges.")]
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
    private GameObject selectedTopping = null;
    private AudioSource audioSource;
    private Controller controller;

    private void Awake()
    {
        remainingCharges = skillCharges;
        if (mainCamera == null) mainCamera = Camera.main;
        controller = Object.FindFirstObjectByType<Controller>();
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        // Setup button listener
        if (skillButton != null)
        {
            skillButton.onClick.AddListener(ActivateSkill);
        }
        UpdateUI();
    }

    private void Update()
    {
        // Handle topping selection when skill is active
        if (skillActive)
        {
            // Check for input (mouse or touch)
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                Vector3 inputPosition = Input.mousePosition;
                if (Input.touchCount > 0) inputPosition = Input.GetTouch(0).position;
                Vector3 worldPosition = mainCamera.ScreenToWorldPoint(inputPosition);
                worldPosition.z = 0f;
                HandleToppingSelection(worldPosition);
            }
        }
    }

    // Activate skill when button is clicked
    public void ActivateSkill()
    {
        if (remainingCharges <= 0 || skillActive) return;
        skillActive = true;
        selectedTopping = null;
        // Disable player controller temporarily to prevent interference
        if (controller != null) controller.enabled = false;
        // Play activation sound
        if (activationSound != null && audioSource != null)
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
        if (hitCollider == null) return;
        GameObject hitTopping = hitCollider.gameObject;
        // First selection stores the topping
        if (selectedTopping == null)
        {
            selectedTopping = hitTopping;
            // Visual feedback: highlight selected topping
            StartCoroutine(HighlightToppingCoroutine(selectedTopping));
            return;
        }
        // Second selection: swap tags between two toppings
        if (hitTopping != selectedTopping)
        {
            SwapToppingTags(selectedTopping, hitTopping);
            // Deduct charge
            remainingCharges--;
            // Deactivate skill
            skillActive = false;
            selectedTopping = null;
            // Re-enable controller
            if (controller != null) controller.enabled = true;
            UpdateUI();
        }
    }

    // Swap the tags of two toppings
    private void SwapToppingTags(GameObject toppingA, GameObject toppingB)
    {
        // Store original tags
        string tagA = toppingA.tag;
        string tagB = toppingB.tag;
        // Swap tags
        toppingA.tag = tagB;
        toppingB.tag = tagA;
        // Swap sprites to match new tags (if you have SpriteRenderer)
        SpriteRenderer spriteA = toppingA.GetComponent<SpriteRenderer>();
        SpriteRenderer spriteB = toppingB.GetComponent<SpriteRenderer>();
        if (spriteA != null && spriteB != null)
        {
            Sprite tempSprite = spriteA.sprite;
            spriteA.sprite = spriteB.sprite;
            spriteB.sprite = tempSprite;
        }
        // Play swap sound
        if (swapSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(swapSound, soundVolume);
        }
        // Spawn particle effects on both toppings
        if (swapParticlePrefab != null)
        {
            SpawnSwapParticle(toppingA.transform.position);
            SpawnSwapParticle(toppingB.transform.position);
        }
    }

    // Spawn swap particle effect at position
    private void SpawnSwapParticle(Vector3 position)
    {
        if (swapParticlePrefab == null) return;
        ParticleSystem particle = Instantiate(swapParticlePrefab, position, Quaternion.identity);
        particle.Play();
        Destroy(particle.gameObject, particle.main.duration + particle.main.startLifetime.constantMax);
    }

    // Highlight topping briefly to show it's selected
    private IEnumerator HighlightToppingCoroutine(GameObject topping)
    {
        SpriteRenderer spriteRenderer = topping.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;
        Color originalColour = spriteRenderer.color;
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration && skillActive)
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
        if (chargesText != null)
        {
            chargesText.text = remainingCharges.ToString();
        }
        // Update button visual state
        if (skillButtonImage != null)
        {
            if (remainingCharges <= 0)
            {
                skillButtonImage.color = depletedColour;
            }
            else if (skillActive)
            {
                skillButtonImage.color = activeColour;
            }
            else
            {
                skillButtonImage.color = availableColour;
            }
        }
        // Disable button if no charges remain
        if (skillButton != null)
        {
            skillButton.interactable = (remainingCharges > 0 && !skillActive);
        }
    }

    // Reset skill charges (for new game)
    public void ResetCharges()
    {
        remainingCharges = skillCharges;
        skillActive = false;
        selectedTopping = null;
        UpdateUI();
    }
}