using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawns toppings at world position before gameplay, then above ladle during gameplay.
/// Refactor: added direct spawn helpers so special prefabs (bombs) can be spawned immediately.
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
    [Tooltip("Maximum time to hide cap when fast refill hide time.")]
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

    [Header("Scene Minimum Safeguard")]
    [Tooltip("If the scene ever has fewer than this many toppings, spawner will automatically create extras.")]
    public int minimumSceneToppings = 25;

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
    private bool originalAnimatorEnabled = false;

    // Public singleton reference for other components (StickyTopping) to find ladle collider
    public static Spawner Instance { get; private set; }

    private void Awake()
    {
        // Set singleton instance
        Instance = this;
        // Calculate weight and default parent
        CalculateTotalWeight();
        if (parentContainer == null) parentContainer = this.transform;
        // Cache animator and save original transform
        if (ladleAnimator == null) ladleAnimator = GetComponent<Animator>();
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        if (ladleAnimator != null) originalAnimatorEnabled = ladleAnimator.enabled;
    }

    private void OnDestroy()
    {
        // Clear singleton reference on destroy to avoid stale pointer in editor
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Keep ladle visuals and animator disabled at start; animation timeline should enable visuals.
        if (ladleSpriteRenderer != null) ladleSpriteRenderer.enabled = false;
        if (ladleEdgeCollider != null) ladleEdgeCollider.enabled = false;
        if (cupCapRenderer != null) cupCapRenderer.enabled = true;
        if (ladleAnimator != null) ladleAnimator.enabled = false;
        // Spawn initial toppings at fixed world position within radius (each at random position in radius).
        if (initialSpawnCount > 0)
        {
            Debug.Log($"Spawner: Spawning {initialSpawnCount} initial toppings at {initialSpawnWorldPosition} (radius {initialSpawnRadius})");
            for (int index = 0; index < initialSpawnCount; index++) SpawnOneAtInitialPosition();
        }
        // Ensure there are at least minimumSceneToppings present after initial spawn.
        EnsureMinimumToppings();
    }

    // Enable gameplay mode - call after countdown finishes
    public void EnableGameplay()
    {
        // Set gameplay active flag
        isGameplayActive = true;
        Debug.Log("Spawner: Gameplay enabled. Ladle visuals remain controlled by animation.");
        // Ensure the scene has the minimum required toppings when gameplay starts.
        EnsureMinimumToppings();
    }

    // Main spawn method called by Logic.cs
    public void SpawnMore(int count)
    {
        if (count <= 0) return;
        Debug.Log($"Spawner: Received SpawnMore request for {count} toppings. Gameplay active: {isGameplayActive}, Currently animating: {isAnimating}, Pending count: {pendingSpawnCount}");
        pendingSpawnCount += count;
        if (!isAnimating) StartCoroutine(SpawnAndAnimateCoroutine());
    }

    // Refill method for Logic.cs compatibility
    public void Refill(int count, bool immediate = false)
    {
        if (count <= 0) return;
        Debug.Log($"Spawner: Received Refill request for {count} toppings. Immediate: {immediate}, Gameplay active: {isGameplayActive}");
        if (immediate || !isGameplayActive) { for (int i = 0; i < count; i++) { if (isGameplayActive) SpawnOneAboveLadle(); else SpawnOneAtInitialPosition(); } return; }
        if (isAnimating) { pendingSpawnCount += count; return; }
        StartCoroutine(FastSpawnAndAnimateCoroutine(count));
    }

    // Spawn bomb topping immediately (returns instantiated GameObject)
    public GameObject SpawnBombTopping()
    {
        if (bombToppingPrefab == null)
        {
            Debug.LogWarning("Spawner: bombToppingPrefab not assigned.", this);
            return null;
        }
        Debug.Log("Spawner: Spawning bomb topping directly as reward.");
        // If gameplay active, spawn above ladle; otherwise spawn at initial area
        if (isGameplayActive) return SpawnPrefabAboveLadle(bombToppingPrefab);
        return SpawnPrefabAtInitialPosition(bombToppingPrefab);
    }

    // Ensure the scene has at least minCount toppings; spawn immediately if deficit exists.
    // Uses Topping component count when available, otherwise parentContainer child count as fallback.
    public void EnsureMinimumToppings(int minCount = -1)
    {
        if (minCount <= 0) minCount = minimumSceneToppings;
        int existing = CountActiveToppings();
        if (existing >= minCount) return;
        int deficit = minCount - existing;
        Debug.Log($"Spawner: Scene has {existing} toppings, spawning {deficit} to reach minimum {minCount}.");
        // Use immediate refill to place toppings without waiting for animation queue.
        Refill(deficit, true);
    }

    // Count active toppings in scene using Topping component as primary signal.
    private int CountActiveToppings()
    {
        // Prefer components of type Topping when available (most robust).
        Topping[] toppings = Object.FindObjectsOfType<Topping>();
        if (toppings != null && toppings.Length > 0) return toppings.Length;
        // Fallback: count children under parent container (if used consistently).
        if (parentContainer != null) return parentContainer.childCount;
        return 0;
    }

    // Regular spawn-and-animate coroutine used by SpawnMore (slower paced)
    private IEnumerator SpawnAndAnimateCoroutine()
    {
        isAnimating = true;
        currentBatchCount = 0;
        Debug.Log($"Spawner: Starting spawn batch. Pending count: {pendingSpawnCount}");
        if (ladleEdgeCollider != null) ladleEdgeCollider.enabled = true;
        while (pendingSpawnCount > 0)
        {
            SpawnOneAboveLadle();
            pendingSpawnCount--;
            currentBatchCount++;
            yield return new WaitForSeconds(rapidSpawnInterval);
        }
        Debug.Log($"Spawner: Finished spawning {currentBatchCount} toppings");
        if (currentBatchCount >= minimumToppingsToAnimate && isGameplayActive) yield return StartCoroutine(PerformLadleAnimationSequence(currentBatchCount));
        else Debug.Log($"Spawner: Not enough toppings to animate (batch: {currentBatchCount}, minimum: {minimumToppingsToAnimate})");
        isAnimating = false;
        // After finishing a spawn batch, ensure minimum scene toppings are present.
        EnsureMinimumToppings();
        if (pendingSpawnCount > 0) StartCoroutine(SpawnAndAnimateCoroutine());
    }

    // Fast spawn coroutine for refills during gameplay
    private IEnumerator FastSpawnAndAnimateCoroutine(int count)
    {
        isAnimating = true;
        currentBatchCount = 0;
        Debug.Log($"Spawner: Fast-refill spawning {count} toppings within {fastRefillHideCapMax} seconds");
        if (ladleEdgeCollider != null) ladleEdgeCollider.enabled = true;
        float totalAllowed = Mathf.Max(0f, fastRefillHideCapMax);
        float delay = (count <= 1) ? 0f : totalAllowed / (count - 1);
        for (int i = 0; i < count; i++)
        {
            SpawnOneAboveLadle();
            currentBatchCount++;
            yield return new WaitForSeconds(delay);
        }
        Debug.Log($"Spawner: Fast-refill finished spawning {currentBatchCount} toppings");
        if (currentBatchCount >= minimumToppingsToAnimate && isGameplayActive) yield return StartCoroutine(PerformLadleAnimationSequence(currentBatchCount));
        else Debug.Log($"Spawner: Fast-refill did not meet animation threshold (spawned: {currentBatchCount})");
        isAnimating = false;
        // After fast-refill, ensure the scene minimum is still met.
        EnsureMinimumToppings();
        if (pendingSpawnCount > 0) StartCoroutine(SpawnAndAnimateCoroutine());
    }

    // Centralised ladle animation sequence
    private IEnumerator PerformLadleAnimationSequence(int batchCount)
    {
        Debug.Log($"Spawner: Performing ladle animation for batch of {batchCount}");
        if (cupCapRenderer != null) { cupCapRenderer.enabled = false; Debug.Log("Spawner: Cup cap hidden for animation"); }
        bool released = false;
        EdgeCollider2D colliderRef = ladleEdgeCollider;
        bool initialColliderEnabled = (colliderRef != null) ? colliderRef.enabled : false;
        float elapsed = 0f;
        float midPoint = animationDuration * 0.5f;
        if (ladleAnimator != null)
        {
            ladleAnimator.enabled = false;
            yield return null;
            ladleAnimator.enabled = true;
            if (animationClip != null) ladleAnimator.Play(animationClip.name, 0, 0f);
            else if (!string.IsNullOrEmpty(animationTriggerName)) ladleAnimator.SetTrigger(animationTriggerName);
            else Debug.LogWarning("Spawner: No animation clip or trigger name assigned!");
        }
        else { Debug.LogWarning("Spawner: Animator is not assigned; skipping animation."); }
        float pollInterval = 0.05f;
        while (elapsed < animationDuration)
        {
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
            if (colliderRef != null)
            {
                bool nowEnabled = colliderRef.enabled;
                if (initialColliderEnabled && !nowEnabled && !released) { Topping.ReleaseAll(); released = true; Debug.Log("Spawner: Detected ladle collider disabled by animation; released stuck toppings immediately."); }
                initialColliderEnabled = nowEnabled;
            }
            if (!released && elapsed >= midPoint) { Topping.ReleaseAll(); released = true; Debug.Log("Spawner: Midpoint reached; released stuck toppings as fallback."); }
        }
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
        Debug.Log("Spawner: Ladle transform reset to original values after animation.");
        if (ladleAnimator != null) ladleAnimator.enabled = false;
        if (cupCapRenderer != null) { cupCapRenderer.enabled = true; Debug.Log("Spawner: Cup cap restored after animation."); }
        yield break;
    }

    // Spawn at initial world position within radius (before gameplay)
    private GameObject SpawnOneAtInitialPosition() { return SpawnPrefabAtInitialPosition(SelectWeightedRandomTopping()); }

    // Spawn above ladle within smaller radius (during gameplay)
    private GameObject SpawnOneAboveLadle() { return SpawnPrefabAboveLadle(SelectWeightedRandomTopping()); }

    // Helper: spawn a specific prefab above ladle
    private GameObject SpawnPrefabAboveLadle(GameObject prefab)
    {
        if (prefab == null) return null;
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * gameplaySpawnRadius;
        Vector3 spawnPosition = transform.position + gameplaySpawnOffset + new Vector3(randomCircle.x, randomCircle.y, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(rotationRange.x, rotationRange.y));
        GameObject spawned = Instantiate(prefab, spawnPosition, rotation, parentContainer);
        if (addInitialVelocity)
        {
            var rb = spawned.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, initialDownwardVelocity);
        }
        return spawned;
    }

    // Helper: spawn a specific prefab at the initial spawn area
    private GameObject SpawnPrefabAtInitialPosition(GameObject prefab)
    {
        if (prefab == null) return null;
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * initialSpawnRadius;
        Vector3 spawnPosition = initialSpawnWorldPosition + new Vector3(randomCircle.x, randomCircle.y, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(rotationRange.x, rotationRange.y));
        GameObject spawned = Instantiate(prefab, spawnPosition, rotation, parentContainer);
        if (addInitialVelocity)
        {
            var rb = spawned.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, initialDownwardVelocity);
        }
        return spawned;
    }

    // Weighted random selection
    private GameObject SelectWeightedRandomTopping()
    {
        if (toppingPrefabs == null || toppingPrefabs.Length == 0) return null;
        if (totalSpawnWeight <= 0f)
        {
            ToppingEntry random = toppingPrefabs[UnityEngine.Random.Range(0, toppingPrefabs.Length)];
            return random != null ? random.toppingPrefab : null;
        }
        float pick = UnityEngine.Random.value * totalSpawnWeight;
        float cumulative = 0f;
        foreach (var entry in toppingPrefabs)
        {
            if (entry == null || entry.toppingPrefab == null) continue;
            cumulative += Mathf.Max(0f, entry.spawnWeight);
            if (pick <= cumulative) return entry.toppingPrefab;
        }
        ToppingEntry fallback = toppingPrefabs[UnityEngine.Random.Range(0, toppingPrefabs.Length)];
        return fallback != null ? fallback.toppingPrefab : null;
    }

    private void CalculateTotalWeight()
    {
        totalSpawnWeight = 0f;
        if (toppingPrefabs == null) return;
        foreach (var entry in toppingPrefabs) if (entry != null) totalSpawnWeight += Mathf.Max(0f, entry.spawnWeight);
    }

    public void RecalculateWeights() { CalculateTotalWeight(); }

    public string[] GetAllToppingTags()
    {
        List<string> tags = new List<string>();
        foreach (var entry in toppingPrefabs)
        {
            if (entry == null || entry.toppingPrefab == null) continue;
            string tag = entry.toppingPrefab.tag;
            if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag)) tags.Add(tag);
        }
        return tags.ToArray();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(initialSpawnWorldPosition, initialSpawnRadius);
        Gizmos.DrawWireSphere(initialSpawnWorldPosition, 0.1f);
        Gizmos.color = Color.cyan;
        Vector3 gameplaySpawnCentre = transform.position + gameplaySpawnOffset;
        Gizmos.DrawWireSphere(gameplaySpawnCentre, gameplaySpawnRadius);
        Gizmos.DrawWireSphere(gameplaySpawnCentre, 0.05f);
    }
}