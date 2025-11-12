using System;
using System.Collections;
using UnityEngine;

/// Controls a single world-space popup GameObject (assigned in the inspector) for pop-up sprite display.
/// The script will enable the popup, set its SpriteRenderer.sprite, play its Animator (if present),
/// then restore its original world position and disable the object when the animation completes.
public class PopUp : MonoBehaviour
{
    [Tooltip("The popup GameObject that already exists in world space. The script will enable/disable this object rather than instantiate prefabs.")]
    public GameObject popupGameObject;

    [Tooltip("Optional scene anchor that specifies a default world location for popups (used when no world position is supplied).")]
    public Transform popupSceneAnchor;

    [Tooltip("Sprite used for the 'Ready' popup.")]
    public Sprite spriteReady;

    [Tooltip("Sprite used for the countdown '3' popup.")]
    public Sprite spriteThree;

    [Tooltip("Sprite used for the countdown '2' popup.")]
    public Sprite spriteTwo;

    [Tooltip("Sprite used for the countdown '1' popup.")]
    public Sprite spriteOne;

    [Tooltip("Sprite used for the combo 'Great' popup.")]
    public Sprite spriteGreat;

    [Tooltip("Sprite used for the super combo 'Excellent' popup.")]
    public Sprite spriteExcellent;

    [Tooltip("Sprite used for the 'Time's Up' popup.")]
    public Sprite spriteTimesUp;

    [Tooltip("Fixed animation duration in seconds for each popup (assignment requires 1 second).")]
    public float animationDuration = 1f;

    [Tooltip("Optional offset in world space to nudge the popup position (x,y,z).")]
    public Vector3 popupWorldOffset = Vector3.zero;

    [Tooltip("Default fallback world position used when no anchor or world position is available.")]
    public Vector3 fallbackWorldPosition = Vector3.zero;

    // Singleton instance for convenient calls
    public static PopUp Instance { get; private set; }

    // Cached components and state
    private SpriteRenderer cachedPopupSpriteRenderer; // <-- Changed: was Image in UI version
    private Animator cachedAnimator;
    private Transform cachedPopupTransform;
    private Vector3 savedPopupWorldPosition; // <-- Changed: saved world position instead of anchored canvas position

    private void Awake()
    {
        // Enforce singleton instance
        if(Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        // Validate popupGameObject and cache its components
        if(popupGameObject == null)
        {
            Debug.LogWarning("PopUp: popupGameObject is not assigned. Please assign the popup GameObject in the inspector.", this);
            return;
        }

        cachedPopupTransform = popupGameObject.transform;

        // Save starting world position so we can restore after moving for world-location popups
        savedPopupWorldPosition = cachedPopupTransform.position;

        // Try to find a SpriteRenderer on the root first, then any child (fallback)
        cachedPopupSpriteRenderer = popupGameObject.GetComponent<SpriteRenderer>();
        if(cachedPopupSpriteRenderer == null)
        {
            cachedPopupSpriteRenderer = popupGameObject.GetComponentInChildren<SpriteRenderer>();
        }

        if(cachedPopupSpriteRenderer == null)
        {
            Debug.LogWarning("PopUp: No SpriteRenderer found on popupGameObject or its children. Please add a SpriteRenderer component.", popupGameObject);
        }

        // Cache Animator (optional)
        cachedAnimator = popupGameObject.GetComponent<Animator>();

        // Ensure the popup object starts disabled so it does not show before requested
        popupGameObject.SetActive(false);
    }

    // Show 'Ready' popup at the preset world location (or scene anchor if present).
    public void ShowReady(Action callbackFunction = null)
    {
        StartCoroutine(ShowSpriteCoroutine(spriteReady, null, callbackFunction));
    }

    // Show countdown 3 -> 2 -> 1 at the preset location (or scene anchor if present).
    public void ShowCountdown(Action callbackFunction = null)
    {
        Sprite[] countdownSprites = new Sprite[] { spriteReady, spriteThree, spriteTwo, spriteOne };
        StartCoroutine(ShowSequentialSpritesCoroutine(countdownSprites, null, callbackFunction));
    }

    // Show combo 'Great' popup. Optional worldPosition allows showing at a world location (finger or topping).
    public void ShowComboGreat(Vector3? optionalWorldPosition = null, Action callbackFunction = null)
    {
        StartCoroutine(ShowSpriteCoroutine(spriteGreat, optionalWorldPosition, callbackFunction));
    }

    // Show super combo 'Excellent' popup. Optional worldPosition allows showing at a world location (finger or topping).
    public void ShowSuperComboExcellent(Vector3? optionalWorldPosition = null, Action callbackFunction = null)
    {
        StartCoroutine(ShowSpriteCoroutine(spriteExcellent, optionalWorldPosition, callbackFunction));
    }

    // Show 'Time's Up' popup at the preset world location (or scene anchor if present).
    public void ShowTimesUp(Action callbackFunction = null)
    {
        StartCoroutine(ShowSpriteCoroutine(spriteTimesUp, null, callbackFunction));
    }

    // Internal: show a single sprite on the assigned popupGameObject, moving it to the proper world position if requested.
    private IEnumerator ShowSpriteCoroutine(Sprite spriteToShow, Vector3? optionalWorldPosition, Action callbackFunction)
    {
        // Validate required components
        if(popupGameObject == null || cachedPopupTransform == null)
        {
            callbackFunction?.Invoke();
            yield break;
        }

        // Determine world position to place popup
        Vector3 worldPosition = savedPopupWorldPosition;

        if(optionalWorldPosition.HasValue)
        {
            worldPosition = optionalWorldPosition.Value;
        } else if(popupSceneAnchor != null)
        {
            worldPosition = popupSceneAnchor.position;
        }
        // Else: use savedPopupWorldPosition (preset)

        // Apply optional world offset
        worldPosition += popupWorldOffset;

        // Set sprite if we have a SpriteRenderer component
        if(cachedPopupSpriteRenderer != null)
        {
            cachedPopupSpriteRenderer.sprite = spriteToShow;
            // Optionally reset colour alpha to 1 in case previous fade modified it
            Color resetColour = cachedPopupSpriteRenderer.color;
            resetColour.a = 1f;
            cachedPopupSpriteRenderer.color = resetColour;
        }

        // Place popup at world position
        cachedPopupTransform.position = worldPosition;

        // Enable popup object
        popupGameObject.SetActive(true);

        // Play Animator from start if present (Animator can animate transform and SpriteRenderer properties)
        if(cachedAnimator != null)
        {
            cachedAnimator.Play(0, -1, 0f);
            cachedAnimator.Update(0f); // force immediate update
        } else
        {
            // If no animator, do a simple spin + fade by code over animationDuration as fallback
            StartCoroutine(SpinAndFadeSpriteCoroutine(cachedPopupTransform, cachedPopupSpriteRenderer, animationDuration));
        }

        // Wait for the fixed animation duration
        yield return new WaitForSeconds(Mathf.Max(0.01f, animationDuration));

        // Disable popup and restore its original world position
        popupGameObject.SetActive(false);
        cachedPopupTransform.position = savedPopupWorldPosition;

        // Optionally clear the sprite so it doesn't linger in editor
        if(cachedPopupSpriteRenderer != null)
        {
            cachedPopupSpriteRenderer.sprite = null;
        }

        // Invoke callback if supplied
        callbackFunction?.Invoke();
    }

    // Internal: show multiple sprites in sequence, each using the same optional world position
    private IEnumerator ShowSequentialSpritesCoroutine(Sprite[] spritesToShow, Vector3? optionalWorldPosition, Action callbackFunction)
    {
        if(spritesToShow == null || spritesToShow.Length == 0)
        {
            callbackFunction?.Invoke();
            yield break;
        }

        for(int index = 0; index < spritesToShow.Length; index++)
        {
            Sprite spriteToShow = spritesToShow[index];
            yield return StartCoroutine(ShowSpriteCoroutine(spriteToShow, optionalWorldPosition, null));
        }

        callbackFunction?.Invoke();
    }

    // Fallback spin + fade coroutine for SpriteRenderer when no Animator is present
    private IEnumerator SpinAndFadeSpriteCoroutine(Transform spriteTransform, SpriteRenderer spriteRenderer, float duration)
    {
        if(spriteTransform == null || spriteRenderer == null || duration <= 0f) yield break;

        float elapsed = 0f;
        // Cache original rotation and colour
        Quaternion originalRotation = spriteTransform.rotation;
        Color originalColour = spriteRenderer.color;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Spin: rotate around Z (world) — 360 degrees over duration
            float zAngle = Mathf.Lerp(0f, 360f, t);
            spriteTransform.rotation = Quaternion.Euler(0f, 0f, zAngle);

            // Fade out: keep nearly full for a moment then fade
            float alpha = Mathf.Clamp01(1f - (t * 1.05f)); // slight overshoot for natural fade
            Color colour = originalColour;
            colour.a = alpha;
            spriteRenderer.color = colour;

            yield return null;
        }

        // Restore transform rotation (keep original rotation)
        spriteTransform.rotation = originalRotation;
        // Restore sprite colour alpha to original (useful if popup reused immediately)
        spriteRenderer.color = originalColour;
    }
}