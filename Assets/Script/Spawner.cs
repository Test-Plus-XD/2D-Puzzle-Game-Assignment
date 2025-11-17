using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawns toppings at world position before gameplay, then above ladle during gameplay.
/// Hides components before game starts, shows them during spawning with animation.
/// Behaviour additions:
/// - Save original ladle transform (local pos/rot/scale) and reset it after each animation completes.
/// - Trigger animation reliably each time by toggling the Animator component (disable -> next frame -> enabl
/// - Ladle sprite and animator remain disabled by default; the animation controls visuals.
/// - Ladle edge collider is re-enabled immediately before spawning a batch so toppings can interact.
/// - Cup cap is hidden only while the ladle animation runs (animationDuration or fast refill hide time).
public class Spawner : MonoBehaviour
{
    [Serializable]
    public class ToppingEntry
    {
        // Prefab to spawn for this topping type.
        public GameObject toppingPrefab;
        // Relative probability weight for this topping.
        public float spawnWeight = 1f;
    }

    [Header("Topping Configuration")]
    [Tooltip("Array of topping prefabs with spawn weights for random selection.")]
    public ToppingEntry[] toppingPrefabs = new ToppingEntry[0];
    [Tooltip("Special bomb topping prefab spawned as combo reward (not part of normal random pool).")]
    public GameObject bombToppingPrefab;

    [Header("Pre-Gameplay Spawn (Fixed World Position)")]
    [Tooltip("World position for initial spawn before gameplay starts.")]
    public Vector3 initialSpawnWorldPosition = new Vector3(0f, 8f, 0f);
    [Tooltip("Radius for random placement of initial toppings.")]
    public float initialSpawnRadius = 2f;
    [Tooltip("Number of toppings spawned before gameplay.")]
    public int initialSpawnCount = 20;

    [Header("Gameplay Spawn (Above Ladle)")]
    [Tooltip("Local offset from ladle position for gameplay spawning.")]
    public Vector3 gameplaySpawnOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("Radius for random placement during gameplay (smaller).")]
    public float gameplaySpawnRadius = 0.5f;

    [Header("Spawn Timing")]
    [Tooltip("Minimum toppings needed before triggering animation.")]
    public int minimumToppingsToAnimate = 3;
    [Tooltip("Time between rapid spawns.")]
    public float rapidSpawnInterval = 0.15f;

    [Header("Animation")]
    [Tooltip("Animator for ladle animation.")]
    public Animator ladleAnimator;
    [Tooltip("Animation clip to play (optional, uses trigger if null).")]
    public AnimationClip animationClip;
    [Tooltip("Trigger name if using trigger instead of clip.")]
    public string animationTriggerName = "Ladle";
    [Tooltip("Duration animation plays (cup cap hidden for this duration).")]
    public float animationDuration = 1.5f;
    [Tooltip("Maximum time to hide cap when fast-refilling (seconds).")]
    public float fastRefillHideCapMax = 0.5f;

    [Header("Visual Components")]
    [Tooltip("Ladle sprite renderer to hide before gameplay.")]
    public SpriteRenderer ladleSpriteRenderer;
    [Tooltip("Ladle edge collider to disable before gameplay.")]
    public EdgeCollider2D ladleEdgeCollider;
    [Tooltip("Cup cap renderer to hide during animation.")]
    public Renderer cupCapRenderer;

    [Header("Gameplay Container")]
    [Tooltip("Parent for spawned toppings.")]
    public Transform parentContainer;
    [Tooltip("Rotation range for spawned toppings.")]
    public Vector2 rotationRange = new Vector2(0f, 360f);

    [Tooltip("Add initial downward velocity to spawned toppings to help them interact with ladle faster.")]
    public bool addInitialVelocity = false;
    [Tooltip("Downward velocity magnitude if addInitialVelocity is enabled.")]
    public float initialDownwardVelocity = -0.5f;

    // Internal state
    private float totalSpawnWeight;
    private int pendingSpawnCount = 0;
    private bool isGameplayActive = false;
    private bool isAnimating = false;
    private int currentBatchCount = 0;

    // Saved original transform of the ladle (local space)
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    // Save animator enabled original state (for reference)
    private bool originalAnimatorEnabled = false;

    // Public singleton reference for other components (StickyTopping) to find ladle collider
    public static Spawner Instance { get; private set; }

    private void Awake()
    {
        // Set singleton instance
        Instance = this;

        // Calculate weight and default parent
        CalculateTotalWeight();
        if(parentContainer == null) parentContainer = this.transform;

        // Cache animator and save original transform
        if(ladleAnimator == null) ladleAnimator = GetComponent<Animator>();
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        if(ladleAnimator != null) originalAnimatorEnabled = ladleAnimator.enabled;
    }

    private void OnDestroy()
    {
        // Clear singleton reference on destroy to avoid stale pointer in editor
        if(Instance == this) Instance = null;
    }

    private void Start()
    {
        // Keep ladle visuals and animator disabled at start; animation timeline should enable visuals.
        if(ladleSpriteRenderer != null) ladleSpriteRenderer.enabled = false;
        if(ladleEdgeCollider != null) ladleEdgeCollider.enabled = false;
        if(cupCapRenderer != null) cupCapRenderer.enabled = true; // Cap visible by default; only hide during animation.
        if(ladleAnimator != null) ladleAnimator.enabled = false;

        // Spawn initial toppings at fixed world position within radius (each at random position in radius).
        if(initialSpawnCount > 0)
        {
            Debug.Log($"Spawner: Spawning {initialSpawnCount} initial toppings at {initialSpawnWorldPosition} (radius {initialSpawnRadius})");
            for(int index = 0; index < initialSpawnCount; index++)
            {
                SpawnOneAtInitialPosition();
            }
        }
    }

    // Enable gameplay mode - call after countdown finishes
    public void EnableGameplay()
    {
        isGameplayActive = true;
        // Keep ladle renderer and animator disabled here; animation should control visuals.
        // Re-enable collider is done just before spawning to ensure correct timing (see coroutines).
        Debug.Log("Spawner: Gameplay enabled. Ladle visuals remain controlled by animation.");
    }

    // Main spawn method called by Logic.cs
    public void SpawnMore(int count)
    {
        if(count <= 0) return;
        Debug.Log($"Spawner: Received SpawnMore request for {count} toppings. Gameplay active: {isGameplayActive}, Currently animating: {isAnimating}, Pending count: {pendingSpawnCount}");
        pendingSpawnCount += count;
        // Start spawning if not already running
        if(!isAnimating)
        {
            StartCoroutine(SpawnAndAnimateCoroutine());
        }
    }

    // Refill method for Logic.cs compatibility
    public void Refill(int count, bool immediate = false)
    {
        if(count <= 0) return;
        Debug.Log($"Spawner: Received Refill request for {count} toppings. Immediate: {immediate}, Gameplay active: {isGameplayActive}");
        // If immediate or gameplay not active, spawn directly (initial behaviour)
        if(immediate || !isGameplayActive)
        {
            for(int i = 0; i < count; i++)
            {
                if(isGameplayActive) SpawnOneAboveLadle(); else SpawnOneAtInitialPosition();
            }
            return;
        }
        // Gameplay active and not immediate: if already animating, queue; otherwise fast-spawn the refill
        if(isAnimating)
        {
            pendingSpawnCount += count;
            return;
        }
        StartCoroutine(FastSpawnAndAnimateCoroutine(count));
    }

    // Spawn bomb topping
    public GameObject SpawnBombTopping()
    {
        if(bombToppingPrefab == null)
        {
            Debug.LogWarning("Spawner: bombToppingPrefab not assigned.", this);
            return null;
        }
        Debug.Log("Spawner: Spawning bomb topping");
        pendingSpawnCount++;
        if(!isAnimating) StartCoroutine(SpawnAndAnimateCoroutine());
        return null;
    }

    // Regular spawn-and-animate coroutine used by SpawnMore (slower paced)
    private IEnumerator SpawnAndAnimateCoroutine()
    {
        isAnimating = true;
        currentBatchCount = 0;
        Debug.Log($"Spawner: Starting spawn batch. Pending count: {pendingSpawnCount}");

        // Re-enable edge collider so spawned toppings can interact with ladle during batch
        if(ladleEdgeCollider != null) ladleEdgeCollider.enabled = true;

        // Rapid spawn phase - spawn all pending toppings (paced by rapidSpawnInterval)
        while(pendingSpawnCount > 0)
        {
            SpawnOneAboveLadle();
            pendingSpawnCount--;
            currentBatchCount++;
            yield return new WaitForSeconds(rapidSpawnInterval);
        }
        Debug.Log($"Spawner: Finished spawning {currentBatchCount} toppings");

        // Trigger animation if conditions met
        if(currentBatchCount >= minimumToppingsToAnimate && isGameplayActive)
        {
            yield return StartCoroutine(PerformLadleAnimationSequence(currentBatchCount));
        } else
        {
            Debug.Log($"Spawner: Not enough toppings to animate (batch: {currentBatchCount}, minimum: {minimumToppingsToAnimate})");
        }

        isAnimating = false;

        // If more pending arrived during animation, continue processing
        if(pendingSpawnCount > 0)
        {
            Debug.Log($"Spawner: More toppings arrived during animation ({pendingSpawnCount}), continuing");
            StartCoroutine(SpawnAndAnimateCoroutine());
        }
    }

    // Fast spawn coroutine for refills during gameplay: spawn all items within fastRefillHideCapMax seconds
    private IEnumerator FastSpawnAndAnimateCoroutine(int count)
    {
        isAnimating = true;
        currentBatchCount = 0;
        Debug.Log($"Spawner: Fast-refill spawning {count} toppings within {fastRefillHideCapMax} seconds");

        // Re-enable edge collider so toppings can interact
        if(ladleEdgeCollider != null) ladleEdgeCollider.enabled = true;

        float totalAllowed = Mathf.Max(0f, fastRefillHideCapMax);
        float delay = (count <= 1) ? 0f : totalAllowed / (count - 1);

        for(int i = 0; i < count; i++)
        {
            SpawnOneAboveLadle();
            currentBatchCount++;
            yield return new WaitForSeconds(delay);
        }
        Debug.Log($"Spawner: Fast-refill finished spawning {currentBatchCount} toppings");

        // Trigger animation if conditions met
        if(currentBatchCount >= minimumToppingsToAnimate && isGameplayActive)
        {
            yield return StartCoroutine(PerformLadleAnimationSequence(currentBatchCount));
        } else
        {
            Debug.Log($"Spawner: Fast-refill did not meet animation threshold (spawned: {currentBatchCount})");
        }

        isAnimating = false;

        // If more pending arrived during fast-refill, process them
        if(pendingSpawnCount > 0) StartCoroutine(SpawnAndAnimateCoroutine());
    }

    // Centralised ladle animation sequence (used by both coroutines)
    // Replaces the previous implementation; it monitors the ladle edge collider and
    // releases stuck toppings either when the animation disables the collider or at the mid-point.
    private IEnumerator PerformLadleAnimationSequence(int batchCount)
    {
        // Play log for debugging
        Debug.Log($"Spawner: Performing ladle animation for batch of {batchCount}");

        // Hide cup cap only while animation runs
        if(cupCapRenderer != null)
        {
            cupCapRenderer.enabled = false;
            Debug.Log("Spawner: Cup cap hidden for animation");
        }

        // Prepare to monitor collider state changes
        bool released = false; // Whether ReleaseAll() has run
        EdgeCollider2D colliderRef = ladleEdgeCollider;
        bool initialColliderEnabled = (colliderRef != null) ? colliderRef.enabled : false;
        float elapsed = 0f;
        float midPoint = animationDuration * 0.5f;

        // Toggle animator off -> next frame -> on so animation reliably restarts
        if(ladleAnimator != null)
        {
            ladleAnimator.enabled = false;
            yield return null; // ensure toggle takes effect
            ladleAnimator.enabled = true;

            // Play via clip or trigger
            if(animationClip != null)
            {
                ladleAnimator.Play(animationClip.name, 0, 0f);
            } else if(!string.IsNullOrEmpty(animationTriggerName))
            {
                ladleAnimator.SetTrigger(animationTriggerName);
            } else
            {
                Debug.LogWarning("Spawner: No animation clip or trigger name assigned!");
            }
        } else
        {
            Debug.LogWarning("Spawner: Animator is not assigned; skipping animation.");
        }

        // Monitor loop while animation plays
        // Check collider.enabled each small interval; if it turns false, release stuck toppings immediately.
        // Also, as a fallback, release at midpoint if collider never toggles.
        float pollInterval = 0.05f; // Poll frequently but not every frame
        while(elapsed < animationDuration)
        {
            // Wait a short interval
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;

            // If collider exists, detect transition from enabled -> disabled
            if(colliderRef != null)
            {
                bool nowEnabled = colliderRef.enabled;
                if(initialColliderEnabled && !nowEnabled && !released)
                {
                    // Animator turned the collider off; release stuck toppings now
                    Topping.ReleaseAll();
                    released = true;
                    Debug.Log("Spawner: Detected ladle collider disabled by animation; released stuck toppings immediately.");
                }
                // Update initialColliderEnabled in case animation re-enables it later (not common)
                initialColliderEnabled = nowEnabled;
            }

            // Fallback: if we reach midpoint and haven't released yet, release now
            if(!released && elapsed >= midPoint)
            {
                Topping.ReleaseAll();
                released = true;
                Debug.Log("Spawner: Midpoint reached; released stuck toppings as fallback.");
            }
        }

        // End of animation: reset ladle transform so repeated animations start from the same pose
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
        Debug.Log("Spawner: Ladle transform reset to original values after animation.");

        // Disable animator again so ladle visuals remain controlled by the animation timeline if desired
        if(ladleAnimator != null) ladleAnimator.enabled = false;

        // IMPORTANT: Do NOT forcibly change the collider enabled state here.
        // Animation is responsible for toggling the collider. We only ensured stuck toppings are released
        // either when the animation turned the collider off or at the midpoint fallback.

        // Restore cup cap after animation
        if(cupCapRenderer != null)
        {
            cupCapRenderer.enabled = true;
            Debug.Log("Spawner: Cup cap restored after animation.");
        }
        yield break;
    }

    // Spawn at initial world position within radius (before gameplay)
    private GameObject SpawnOneAtInitialPosition()
    {
        GameObject prefab = SelectWeightedRandomTopping();
        if(prefab == null) return null;
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * initialSpawnRadius;
        Vector3 spawnPosition = initialSpawnWorldPosition + new Vector3(randomCircle.x, randomCircle.y, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(rotationRange.x, rotationRange.y));
        GameObject spawned = Instantiate(prefab, spawnPosition, rotation, parentContainer);
        if(addInitialVelocity)
        {
            var rb = spawned.GetComponent<Rigidbody2D>();
            if(rb != null) rb.linearVelocity = new Vector2(0f, initialDownwardVelocity);
        }
        return spawned;
    }

    // Spawn above ladle within smaller radius (during gameplay)
    private GameObject SpawnOneAboveLadle()
    {
        GameObject prefab = SelectWeightedRandomTopping();
        if(prefab == null) return null;
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * gameplaySpawnRadius;
        Vector3 spawnPosition = transform.position + gameplaySpawnOffset + new Vector3(randomCircle.x, randomCircle.y, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(rotationRange.x, rotationRange.y));
        GameObject spawned = Instantiate(prefab, spawnPosition, rotation, parentContainer);
        if(addInitialVelocity)
        {
            var rb = spawned.GetComponent<Rigidbody2D>();
            if(rb != null) rb.linearVelocity = new Vector2(0f, initialDownwardVelocity);
        }
        return spawned;
    }

    // Weighted random selection
    private GameObject SelectWeightedRandomTopping()
    {
        if(toppingPrefabs == null || toppingPrefabs.Length == 0) return null;
        if(totalSpawnWeight <= 0f)
        {
            ToppingEntry random = toppingPrefabs[UnityEngine.Random.Range(0, toppingPrefabs.Length)];
            return random != null ? random.toppingPrefab : null;
        }
        float pick = UnityEngine.Random.value * totalSpawnWeight;
        float cumulative = 0f;
        foreach(var entry in toppingPrefabs)
        {
            if(entry == null || entry.toppingPrefab == null) continue;
            cumulative += Mathf.Max(0f, entry.spawnWeight);
            if(pick <= cumulative) return entry.toppingPrefab;
        }
        ToppingEntry fallback = toppingPrefabs[UnityEngine.Random.Range(0, toppingPrefabs.Length)];
        return fallback != null ? fallback.toppingPrefab : null;
    }

    private void CalculateTotalWeight()
    {
        totalSpawnWeight = 0f;
        if(toppingPrefabs == null) return;
        foreach(var entry in toppingPrefabs)
        {
            if(entry != null) totalSpawnWeight += Mathf.Max(0f, entry.spawnWeight);
        }
    }

    public void RecalculateWeights() { CalculateTotalWeight(); }

    public string[] GetAllToppingTags()
    {
        List<string> tags = new List<string>();
        foreach(var entry in toppingPrefabs)
        {
            if(entry == null || entry.toppingPrefab == null) continue;
            string tag = entry.toppingPrefab.tag;
            if(!string.IsNullOrEmpty(tag) && !tags.Contains(tag)) tags.Add(tag);
        }
        return tags.ToArray();
    }

    // Gizmos for spawn area visualisation
    private void OnDrawGizmosSelected()
    {
        // Initial spawn area (before gameplay)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(initialSpawnWorldPosition, initialSpawnRadius);
        Gizmos.DrawWireSphere(initialSpawnWorldPosition, 0.1f);
        // Gameplay spawn area (above ladle)
        Gizmos.color = Color.cyan;
        Vector3 gameplaySpawnCentre = transform.position + gameplaySpawnOffset;
        Gizmos.DrawWireSphere(gameplaySpawnCentre, gameplaySpawnRadius);
        Gizmos.DrawWireSphere(gameplaySpawnCentre, 0.05f);
    }
}