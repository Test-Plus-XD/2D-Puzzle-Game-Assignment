using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // For legacy text popups

/// Core game logic for chaining and clearing toppings (connect 3+ to clear).
/// Keeps both the new animated image popups (PopUp) and the legacy text popups.
public class Logic : MonoBehaviour
{
    public LayerMask toppingLayerMask; // Layer where toppings live
    public float overlapPointRadius = 0.05f; // Radius for overlap point check
    public float holdToRegisterDelay = 0f; // Optional small delay before registering hold

    public Spawner spawner; // Reference to spawner to request more items

    // UI popups
    public GameObject comboPopupPrefab; // Prefab (UI Text) for >= 6 clear (legacy)
    public GameObject superComboPopupPrefab; // Prefab (UI Text) for >= 9 clear (legacy)
    public Transform uiCanvasTransform; // Parent Canvas transform for legacy popups

    // Audio clips (played when combo thresholds are reached)
    public AudioClip removalSound; // Sound to play per topping removal
    public float removalSoundVolume = 0.6f; // Volume for removal sound
    public AudioClip comboAudioClip; // Sound to play when combo >= 6
    public float comboAudioVolume = 0.8f; // Volume for combo audio
    public AudioClip superComboAudioClip; // Sound to play when combo >= 9
    public float superComboAudioVolume = 1.0f; // Volume for super combo audio

    private AudioSource audioSource; // Cached audio source
    private readonly List<GameObject> storedToppings = new List<GameObject>(); // Stored chain objects
    private string storedToppingTag = null; // Tag we are matching during the current chain
    private bool inputActive = false; // Whether a press is in progress
    private Controller controller; // Cached reference to Controller

    void Awake()
    {
        // Cache Controller reference using Unity 6 API
        controller = Object.FindFirstObjectByType<Controller>();

        // Ensure audio source exists for removal and combo sounds
        audioSource = gameObject.GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // Called by Controller when touch begins
    public void OnPress(Vector3 worldPosition)
    {
        inputActive = true;
        ResetChain(); // Start fresh on each press
        TryRegisterAtPosition(worldPosition);
    }

    // Called by Controller while touch held / moved
    public void OnHold(Vector3 worldPosition)
    {
        if(!inputActive) return;
        TryRegisterAtPosition(worldPosition);
    }

    // Called by Controller when touch ends
    public void OnRelease(Vector3 worldPosition)
    {
        inputActive = false;
        // If 3 or more are stored, clear them
        if(storedToppings.Count >= 3) StartCoroutine(ClearStoredCoroutine());
        else ResetChain(); // Not enough toppings, play fail sound or reset chain
    }

    // Try to see if a topping is under the given world position and register it
    void TryRegisterAtPosition(Vector3 worldPosition)
    {
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D overlapCollider = Physics2D.OverlapPoint(point, toppingLayerMask);
        if(overlapCollider == null) return;

        GameObject hitObject = overlapCollider.gameObject;

        // If first item, set the stored tag
        if(storedToppings.Count == 0) storedToppingTag = hitObject.tag;

        // Only accept if tag matches stored tag
        if(storedToppingTag != hitObject.tag) return;

        // Prevent repeats
        if(storedToppings.Contains(hitObject)) return;

        // If this is not the first topping, check if any other type blocks the line
        if(storedToppings.Count > 0)
        {
            GameObject lastTopping = storedToppings[storedToppings.Count - 1];
            Vector2 start = lastTopping.transform.position;
            Vector2 end = hitObject.transform.position;
            Vector2 direction = (end - start).normalized;
            float distance = Vector2.Distance(start, end);

            // Raycast between last and new topping to detect blockers
            RaycastHit2D[] raycastHits = Physics2D.RaycastAll(start, direction, distance, toppingLayerMask);

            foreach(var raycastHit in raycastHits)
            {
                if(raycastHit.collider == null) continue;
                GameObject raycastHitObject = raycastHit.collider.gameObject;
                if(raycastHitObject == lastTopping || raycastHitObject == hitObject) continue;

                // If any other topping has a different tag, block the connection
                if(raycastHitObject.tag != storedToppingTag)
                {
                    // Optional: provide feedback (short vibration or sound) here
                    return; // Abort connection
                }
            }
        }

        // Passed the obstruction test — add to list
        storedToppings.Add(hitObject);

        // Notify Controller for visuals (so it can show line & dots)
        if(controller != null)
        {
            // Create a dot for the specific topping (Controller will manage dot instances)
            controller.CreateDotForTopping();

            List<Transform> transforms = new List<Transform>();
            foreach(var storedObject in storedToppings) transforms.Add(storedObject.transform);
            controller.SetConnectedToppings(transforms);
            controller.PlayChainIncrementSound();
        }
    }

    // Reset chain and remove dot visuals
    void ResetChain()
    {
        storedToppings.Clear();
        storedToppingTag = null;
        if(controller != null) controller.SetConnectedToppings(new List<Transform>());
    }

    // Coroutine that removes stored items one by one with a short spacing
    IEnumerator ClearStoredCoroutine()
    {
        // Copy and prepare
        GameObject[] itemsToClear = storedToppings.ToArray();
        int clearedCount = itemsToClear.Length;

        // Compute centroid of cleared items for popup placement (world space)
        Vector3 popupWorldPosition = Vector3.zero;
        int validPositionCount = 0;
        foreach(var itemObject in itemsToClear)
        {
            if(itemObject == null) continue;
            popupWorldPosition += itemObject.transform.position;
            validPositionCount++;
        }
        if(validPositionCount > 0) popupWorldPosition /= Mathf.Max(1, validPositionCount);
        else popupWorldPosition = Vector3.zero;

        ResetChain(); // Reset early so player can start new chain while clear animates

        for(int index = 0; index < itemsToClear.Length; index++)
        {
            var itemObject = itemsToClear[index];
            if(itemObject == null) continue;

            // Play removal sound (global)
            if(removalSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(removalSound, removalSoundVolume);
            }

            // Call Remove as the lesson instructs (prefab's Explode will handle animation + destroy)
            itemObject.SendMessage("Remove", SendMessageOptions.DontRequireReceiver);

            // Spacing between each clear
            yield return new WaitForSeconds(0.1f);
        }

        // Pop-up and combo audio handling after clears
        if(clearedCount >= 9)
        {
            // Play super combo audio
            if(superComboAudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(superComboAudioClip, superComboAudioVolume);
            }

            // Show animated image popup via PopUp (at centroid)
            if(PopUp.Instance != null)
            {
                PopUp.Instance.ShowSuperComboExcellent(popupWorldPosition);
            }

            // Also show legacy text popup (keeps both systems visible)
            if(superComboPopupPrefab != null && uiCanvasTransform != null)
            {
                StartCoroutine(ShowPopupCoroutine(superComboPopupPrefab, "SUPER COMBO! x" + clearedCount));
            }
        } else if(clearedCount >= 6)
        {
            // Play combo audio
            if(comboAudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(comboAudioClip, comboAudioVolume);
            }

            // Show animated image popup via PopUp (at centroid)
            if(PopUp.Instance != null)
            {
                PopUp.Instance.ShowComboGreat(popupWorldPosition);
            }

            // Also show legacy text popup (keeps both systems visible)
            if(comboPopupPrefab != null && uiCanvasTransform != null)
            {
                StartCoroutine(ShowPopupCoroutine(comboPopupPrefab, "COMBO! x" + clearedCount));
            }
        }

        // Ask spawner to refill to balance lost items
        if(spawner != null) spawner.StartCoroutine(RefillCoroutine(itemsToClear.Length));
    }

    // Example refill coroutine that asks spawner to spawn N items with a slight delay
    IEnumerator RefillCoroutine(int count)
    {
        int spawned = 0;
        while(spawned < count)
        {
            spawner.SpawnOne();
            spawned++;
            yield return new WaitForSeconds(0.05f);
        }
    }

    // Small coroutine to display a UI text popup, animate it, then destroy (legacy fallback)
    IEnumerator ShowPopupCoroutine(GameObject popupPrefab, string text)
    {
        if(popupPrefab == null || uiCanvasTransform == null) yield break;

        // Instantiate the popup under the provided canvas
        GameObject popupInstance = Instantiate(popupPrefab, uiCanvasTransform, false);
        // Try to get a Text component
        Text popupText = popupInstance.GetComponentInChildren<Text>();
        if(popupText != null) popupText.text = text;

        // Animate scale and fade for a short time
        float duration = 1.1f;
        float elapsed = 0f;
        Vector3 initialScale = popupInstance.transform.localScale;
        CanvasGroup canvasGroup = popupInstance.GetComponent<CanvasGroup>();
        if(canvasGroup == null)
        {
            canvasGroup = popupInstance.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
        }

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Scale up slightly then settle
            popupInstance.transform.localScale = Vector3.Lerp(initialScale * 0.7f, initialScale * 1.15f, Mathf.Sin(t * Mathf.PI));
            // Fade out at the end
            canvasGroup.alpha = Mathf.Clamp01(1f - (t * 1.2f));
            yield return null;
        }
        Destroy(popupInstance);
    }
}