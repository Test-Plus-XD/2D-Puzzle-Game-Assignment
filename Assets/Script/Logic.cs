using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // For popup Text

/// Core game logic for chaining and clearing toppings (connect 3+ to clear).
public class Logic : MonoBehaviour
{
    public LayerMask toppingLayerMask; // Layer where toppings live
    public float overlapPointRadius = 0.05f; // Radius for overlap point check
    public float holdToRegisterDelay = 0f; // Optional small delay before registering hold

    public Spawner spawner; // Reference to spawner to request more items

    // Removal audio and UI popups
    public AudioClip removalSound; // Sound to play per topping removal
    public float removalSoundVolume = 0.7f; // Volume for removal sound
    public GameObject comboPopupPrefab; // Prefab (UI Text) for >= 6 clear
    public GameObject superComboPopupPrefab; // Prefab (UI Text) for >= 9 clear
    public Transform UICanvasTransform; // Parent Canvas transform for UI popups

    private AudioSource audioSource; // Cached audio source
    readonly List<GameObject> storedToppings = new List<GameObject>(); // Stored chain objects
    private string storedToppingTag = null; // Tag we are matching during the current chain
    private bool inputActive = false; // Whether a press is in progress
    private Controller controller; // Cached reference to Controller

    void Awake()
    {
        // Cache Controller reference
        controller = Object.FindFirstObjectByType<Controller>();

        // Ensure audio source exists for removal sounds
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
        if(storedToppings.Count >= 3)
        {
            StartCoroutine(ClearStoredCoroutine());
        } else
        {
            // Not enough toppings, play fail sound or reset chain
            ResetChain();
        }
    }

    // Try to see if a topping is under the given world position and register it
    void TryRegisterAtPosition(Vector3 worldPosition)
    {
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D hit = Physics2D.OverlapPoint(point, toppingLayerMask);
        if(hit == null) return;

        GameObject hitObject = hit.gameObject;

        // If first item, set the stored tag
        if(storedToppings.Count == 0)
        {
            storedToppingTag = hitObject.tag;
        }

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
            RaycastHit2D[] hits = Physics2D.RaycastAll(start, direction, distance, toppingLayerMask);

            foreach(var h in hits)
            {
                if(h.collider == null) continue;
                GameObject obj = h.collider.gameObject;
                if(obj == lastTopping || obj == hitObject) continue;

                // If any other topping has a different tag, block the connection
                if(obj.tag != storedToppingTag)
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
            controller.CreateDotForTopping();
            List<Transform> transforms = new List<Transform>();
            foreach(var go in storedToppings) transforms.Add(go.transform);
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

        ResetChain(); // Reset early so player can start new chain while clear animates

        for(int i = 0; i < itemsToClear.Length; i++)
        {
            var item = itemsToClear[i];
            if(item == null) continue;

            // Play removal sound (global)
            if(removalSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(removalSound, removalSoundVolume);
            }

            // Call Remove as the lesson instructs (prefab's Explode will handle animation + destroy)
            item.SendMessage("Remove", SendMessageOptions.DontRequireReceiver);

            // Spacing between each clear
            yield return new WaitForSeconds(0.1f);
        }

        // Pop-up handling after clears
        if(clearedCount >= 9)
        {
            // Show super combo popup for large clears (>=9)
            if(superComboPopupPrefab != null && UICanvasTransform != null) StartCoroutine(ShowPopupCoroutine(superComboPopupPrefab, "SUPER COMBO! x" + clearedCount));
        } else if(clearedCount >= 6){
            // Show combo popup for medium clears (>=6)
            if(comboPopupPrefab != null && UICanvasTransform != null) StartCoroutine(ShowPopupCoroutine(comboPopupPrefab, "COMBO! x" + clearedCount));
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

    // Small coroutine to display a UI text popup, animate it, then destroy
    IEnumerator ShowPopupCoroutine(GameObject popupPrefab, string text)
    {
        // Instantiate the popup under the provided canvas
        GameObject popupInstance = Instantiate(popupPrefab, UICanvasTransform, false);
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