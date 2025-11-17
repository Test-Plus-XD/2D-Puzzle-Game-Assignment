using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Core game logic for chaining and clearing toppings (connect 3+ to clear).
/// Integrated with ScoreManager for score tracking and special topping handling.
/// Keeps both the animated image popups (PopUp) and the legacy text popups.
public class Logic : MonoBehaviour
{
    // Gameplay settings
    // Layer where toppings live
    public LayerMask toppingLayerMask;
    // Radius for overlap point check
    public float overlapPointRadius = 0.05f;
    // Optional small delay before registering hold
    public float holdToRegisterDelay = 0f;
    // Reference to spawner to request more items
    public Spawner spawner;
    // UI popups
    // Prefab (UI Text) for >= 6 clear (legacy)
    public GameObject comboPopupPrefab;
    // Prefab (UI Text) for >= 9 clear (legacy)
    public GameObject superComboPopupPrefab;
    // Parent canvas transform for legacy popups
    public Transform uiCanvasTransform;
    // Audio clips
    // Sound to play per topping removal
    public AudioClip removalSound;
    // Volume for removal sound
    public float removalSoundVolume = 0.6f;
    // Sound to play when combo >= 6
    public AudioClip comboAudioClip;
    // Volume for combo audio
    public float comboAudioVolume = 0.8f;
    // Sound to play when combo >= 9
    public AudioClip superComboAudioClip;
    // Volume for super combo audio
    public float superComboAudioVolume = 1.0f;
    [Header("Special Feature Settings")]
    [Tooltip("Reference to GameManager for time extension feature.")]
    public GameManager gameManager;
    [Tooltip("Chain length required to trigger time extension.")]
    public int timeExtensionChainThreshold = 9;
    [Tooltip("Seconds added when time extension is triggered.")]
    public float timeExtensionAmount = 5f;
    [Header("Special Topping Settings")]
    [Tooltip("Combo threshold to spawn a bomb topping as reward.")]
    public int bombSpawnComboThreshold = 6;
    [Tooltip("Optional GameObject (scene object or prefab) that contains a Bomb component. Assign in Inspector to avoid runtime search.")]
    public GameObject bombGameObjectReference;
    [Tooltip("Optional GameObject (scene object or prefab) that contains a DualConnect component. Assign in Inspector to avoid runtime search.")]
    public GameObject dualConnectGameObjectReference;

    // Private state
    // Cached audio source for playing removal and combo sounds
    private AudioSource audioSource;
    // Stored chain objects
    private readonly List<GameObject> storedToppings = new List<GameObject>();
    // Tag we are matching during the current chain (base type)
    private string storedToppingTag = null;
    // Whether a press is in progress
    private bool inputActive = false;
    // Cached reference to Controller for visuals and audio cues
    private Controller controller;
    // Track whether a bomb has been spawned as a reward during the current combo
    private bool hasSpawnedBombThisCombo = false;
    // Cached special-component references resolved from inspector fields or runtime search
    private Bomb bombComponent;
    private DualConnect dualConnectComponent;
    // Number of extra toppings cleared by bombs while a chain clear is in progress.
    private int pendingExtraRefillCount = 0;
    // Whether a chain-clear coroutine is currently running (helps reasoning / debugging).
    private bool isClearingChain = false;

    void Awake()
    {
        // Cache Controller reference using Unity API
        controller = Object.FindFirstObjectByType<Controller>();
        // Ensure audio source exists for removal and combo sounds
        audioSource = gameObject.GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        // Resolve Bomb reference from Inspector or find in scene as fallback
        if(bombGameObjectReference != null)
        {
            bombComponent = bombGameObjectReference.GetComponent<Bomb>();
            if(bombComponent == null)
            {
                Debug.LogWarning("Logic: bombGameObjectReference assigned but no Bomb component found on that GameObject.", this);
            }
        } else
        {
            bombComponent = Object.FindFirstObjectByType<Bomb>();
            if(bombComponent != null) bombGameObjectReference = bombComponent.gameObject;
        }
        // Resolve DualConnect reference from Inspector or find in scene as fallback
        if(dualConnectGameObjectReference != null)
        {
            dualConnectComponent = dualConnectGameObjectReference.GetComponent<DualConnect>();
            if(dualConnectComponent == null)
            {
                Debug.LogWarning("Logic: dualConnectGameObjectReference assigned but no DualConnect component found on that GameObject.", this);
            }
        } else
        {
            dualConnectComponent = Object.FindFirstObjectByType<DualConnect>();
            if(dualConnectComponent != null) dualConnectGameObjectReference = dualConnectComponent.gameObject;
        }
        // Informative warnings if neither inspector nor scene contains those components
        if(bombComponent == null)
        {
            Debug.Log("Logic: No Bomb component found via Inspector or in-scene search. Special bomb behaviour will still work if Bomb components exist on toppings.", this);
        }
        if(dualConnectComponent == null)
        {
            Debug.Log("Logic: No DualConnect component found via Inspector or in-scene search. Dual-connect behaviour will still work if DualConnect components exist on toppings.", this);
        }
    }

    // Subscribe to Bomb events on enable, unsubscribe on disable.
    private void OnEnable()
    {
        Bomb.Exploded += OnBombExploded;
    }

    private void OnDisable()
    {
        Bomb.Exploded -= OnBombExploded;
    }

    // Event handler invoked by Bomb when it clears toppings.
    // This simply accumulates the extra count so we can refill once later.
    private void OnBombExploded(int clearedByBomb)
    {
        if(clearedByBomb <= 0) return;
        pendingExtraRefillCount += clearedByBomb;
        Debug.Log($"Logic: Bomb reported {clearedByBomb} cleared, pendingExtraRefillCount now {pendingExtraRefillCount}", this);
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
        // If 3 or more are stored, clear them; otherwise reset chain
        if(storedToppings.Count >= 3) StartCoroutine(ClearStoredCoroutine());
        else ResetChain();
    }

    // Try to see if a topping is under the given world position and register it
    void TryRegisterAtPosition(Vector3 worldPosition)
    {
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D overlapCollider = Physics2D.OverlapPoint(point, toppingLayerMask);
        if(overlapCollider == null) return;
        GameObject hitObject = overlapCollider.gameObject;
        // Prevent adding the same topping twice
        if(storedToppings.Contains(hitObject)) return;
        // Check if this topping can be added to the current chain
        bool canAddToChain = CanAddToppingToChain(hitObject);
        if(!canAddToChain) return;
        // If not first topping, check for blocking toppings between last and new
        if(storedToppings.Count > 0)
        {
            GameObject lastTopping = storedToppings[storedToppings.Count - 1];
            if(HasBlockingToppingBetween(lastTopping, hitObject)) return;
        }
        // Successfully add topping to chain
        storedToppings.Add(hitObject);
        // Update stored tag only when a normal (non-special) topping becomes the first base
        if(storedToppings.Count == 1 && !IsSpecialTopping(hitObject))
        {
            storedToppingTag = hitObject.tag;
        }
        // Notify Controller for visual feedback
        if(controller != null)
        {
            controller.CreateDotForTopping();
            List<Transform> transforms = new List<Transform>();
            foreach(var storedObject in storedToppings) transforms.Add(storedObject.transform);
            controller.SetConnectedToppings(transforms);
            controller.PlayChainIncrementSound();
        }
    }

    // Determine if a topping can be added to the current chain based on matching rules
    bool CanAddToppingToChain(GameObject topping)
    {
        if(topping == null) return false;
        bool toppingIsDual = IsDualConnect(topping);
        bool toppingIsBomb = IsBomb(topping);
        // First topping rules: Bomb and Dual cannot start a chain
        if(storedToppings.Count == 0)
        {
            if(toppingIsBomb) return false; // Bomb may not start the chain
            if(toppingIsDual) return false; // Dual may not start the chain
            return true; // Any normal topping may start
        }
        // Detect if chain already contains any DualConnect
        bool chainHasDual = storedToppings.Exists(IsDualConnect);
        // Dual-specific rule: dual can only continue an existing chain with a base type
        if(toppingIsDual)
        {
            DualConnect dual = topping.GetComponent<DualConnect>();
            if(dual != null)
            {
                return !string.IsNullOrEmpty(storedToppingTag);
            }
            return false;
        }
        // Bomb-specific rules
        if(toppingIsBomb)
        {
            if(chainHasDual) return false;
            return true;
        }
        // Normal topping rules
        if(chainHasDual)
        {
            if(string.IsNullOrEmpty(storedToppingTag)) return false;
            return topping.tag == storedToppingTag;
        }
        if(string.IsNullOrEmpty(storedToppingTag))
        {
            return true;
        }
        return topping.tag == storedToppingTag;
    }

    // Get the effective tag for chain matching (returns the tag or identifies special types)
    string GetEffectiveToppingTag(GameObject topping)
    {
        if(topping == null) return null;
        return topping.tag;
    }

    // Check if a topping is a special type (Bomb or DualConnect) using component detection
    bool IsSpecialTopping(GameObject topping)
    {
        if(topping == null) return false;
        return topping.GetComponent<Bomb>() != null || topping.GetComponent<DualConnect>() != null;
    }

    // Check if a topping is a DualConnect by component detection
    bool IsDualConnect(GameObject topping)
    {
        if(topping == null) return false;
        return topping.GetComponent<DualConnect>() != null;
    }

    // Check if a topping is a Bomb by component detection
    bool IsBomb(GameObject topping)
    {
        if(topping == null) return false;
        return topping.GetComponent<Bomb>() != null;
    }

    // Check if there are any blocking toppings between two positions
    bool HasBlockingToppingBetween(GameObject fromTopping, GameObject toTopping)
    {
        Vector2 start = fromTopping.transform.position;
        Vector2 end = toTopping.transform.position;
        Vector2 direction = (end - start).normalized;
        float distance = Vector2.Distance(start, end);
        // Raycast between the two toppings to detect any others in the path
        RaycastHit2D[] raycastHits = Physics2D.RaycastAll(start, direction, distance, toppingLayerMask);
        foreach(var raycastHit in raycastHits)
        {
            if(raycastHit.collider == null) continue;
            GameObject raycastHitObject = raycastHit.collider.gameObject;
            // Ignore the start and end toppings themselves
            if(raycastHitObject == fromTopping || raycastHitObject == toTopping) continue;
            // Special toppings don't block the connection
            if(IsSpecialTopping(raycastHitObject)) continue;
            // Normal blocker check: if it doesn't match our chain type, it blocks
            if(!string.IsNullOrEmpty(storedToppingTag) && raycastHitObject.tag != storedToppingTag) return true;
        }
        return false;
    }

    // Reset chain and remove dot visuals
    void ResetChain()
    {
        storedToppings.Clear();
        storedToppingTag = null;
        // Reset the bomb spawn flag when starting a new potential combo sequence
        hasSpawnedBombThisCombo = false;
        if(controller != null) controller.SetConnectedToppings(new List<Transform>());
    }

    // Coroutine that removes stored items one by one with a short spacing
    IEnumerator ClearStoredCoroutine()
    {
        // Mark that a chain clear is running.
        isClearingChain = true;
        // Capture the items to clear now (array snapshot)
        GameObject[] itemsToClear = storedToppings.ToArray();
        int clearedCount = itemsToClear.Length;
        // Compute centroid for popup placement (world space)
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
        // Check for time extension
        bool timeExtended = false;
        if(clearedCount >= timeExtensionChainThreshold && gameManager != null)
        {
            gameManager.ExtendTime(timeExtensionAmount);
            timeExtended = true;
        }
        // Check if we should spawn a bomb topping as reward for combo
        bool shouldSpawnBomb = (clearedCount >= bombSpawnComboThreshold && !hasSpawnedBombThisCombo);
        if(shouldSpawnBomb) hasSpawnedBombThisCombo = true;
        // Reset chain state early to allow player input while the clear animates
        ResetChain();
        // Clear toppings with animation (this will invoke Bomb.Remove() which raises Bomb.Exploded event)
        for(int index = 0; index < itemsToClear.Length; index++)
        {
            var itemObject = itemsToClear[index];
            if(itemObject == null) continue;
            if(removalSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(removalSound, removalSoundVolume);
            }
            itemObject.SendMessage("Remove", SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(0.1f);
        }
        // CRITICAL: Wait for bomb explosions to complete
        // Bombs take time to explode and clear additional toppings
        // We need to wait long enough for all bomb coroutines to finish and fire their events
        // The bomb's destroyDelay is 0.8s, plus 0.05s per topping cleared, so we wait conservatively
        yield return new WaitForSeconds(1.0f);
        // Now pendingExtraRefillCount should have all bomb-cleared toppings accumulated
        // Calculate total physical distance between connected toppings (for scoring)
        float totalPhysicalLength = CalculateChainPhysicalLength(itemsToClear);
        // Add score via ScoreManager (uses both count and physical length)
        if(ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScoreForChain(clearedCount, totalPhysicalLength);
            if(timeExtended)
            {
                ScoreManager.Instance.AddTimeExtensionBonus();
            }
        }
        // Combo popups and audio (keep both animated and legacy text popups)
        if(clearedCount >= 9)
        {
            if(superComboAudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(superComboAudioClip, superComboAudioVolume);
            }
            if(PopUp.Instance != null)
            {
                PopUp.Instance.ShowSuperComboExcellent(popupWorldPosition);
            }
            if(superComboPopupPrefab != null && uiCanvasTransform != null)
            {
                StartCoroutine(ShowPopupCoroutine(superComboPopupPrefab, "SUPER COMBO! x" + clearedCount));
            }
        } else if(clearedCount >= 6)
        {
            if(comboAudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(comboAudioClip, comboAudioVolume);
            }
            if(PopUp.Instance != null)
            {
                PopUp.Instance.ShowComboGreat(popupWorldPosition);
            }
            if(comboPopupPrefab != null && uiCanvasTransform != null)
            {
                StartCoroutine(ShowPopupCoroutine(comboPopupPrefab, "COMBO! x" + clearedCount));
            }
        }
        // Spawn bomb topping as reward if threshold met
        if(shouldSpawnBomb && spawner != null)
        {
            GameObject spawnedBomb = spawner.SpawnBombTopping();
            if(spawnedBomb != null) Debug.Log("Logic: Bomb reward spawned by Spawner.", this);
            else Debug.LogWarning("Logic: Spawner did not return a bomb instance (check bombToppingPrefab assignment).", this);
        }
        // Cmpute combined refill count and call spawner just once
        int totalRefillCount = clearedCount + pendingExtraRefillCount;
        if(totalRefillCount > 0 && spawner != null)
        {
            try
            {
                spawner.SpawnMore(totalRefillCount);
            }
            catch(System.Exception)
            {
                System.Console.Error.WriteLine("Logic: Exception occurred while calling spawner.SpawnMore().");
            }
        }
        // Reset pending extras after refill is scheduled
        pendingExtraRefillCount = 0;
        // Done clearing chain
        isClearingChain = false;
    }

    // Small coroutine to display a UI text popup, animate it, then destroy (legacy fallback)
    IEnumerator ShowPopupCoroutine(GameObject popupPrefab, string text)
    {
        if(popupPrefab == null || uiCanvasTransform == null) yield break;
        GameObject popupInstance = Instantiate(popupPrefab, uiCanvasTransform, false);
        Text popupText = popupInstance.GetComponentInChildren<Text>();
        if(popupText != null) popupText.text = text;
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
            popupInstance.transform.localScale = Vector3.Lerp(initialScale * 0.7f, initialScale * 1.15f, Mathf.Sin(t * Mathf.PI));
            canvasGroup.alpha = Mathf.Clamp01(1f - (t * 1.2f));
            yield return null;
        }
        Destroy(popupInstance);
    }

    // Calculate the sum of distances between consecutive toppings in the chain
    float CalculateChainPhysicalLength(GameObject[] toppings)
    {
        if(toppings == null || toppings.Length < 2) return 0f;
        float totalDistance = 0f;
        // Sum up distance between each consecutive pair
        for(int index = 0; index < toppings.Length - 1; index++)
        {
            GameObject currentTopping = toppings[index];
            GameObject nextTopping = toppings[index + 1];
            // Skip null objects (in case any were destroyed)
            if(currentTopping == null || nextTopping == null) continue;
            // Calculate distance between the two positions
            float distance = Vector3.Distance(currentTopping.transform.position, nextTopping.transform.position);
            totalDistance += distance;
        }
        return totalDistance;
    }

    // Reset the bomb spawn flag when starting a new potential combo sequence
    public void ResetBombSpawnFlag()
    {
        hasSpawnedBombThisCombo = false;
    }
}