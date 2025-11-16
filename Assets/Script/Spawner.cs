using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawns topping prefabs with weights, supports initial spawn limit and manual spawn requests.
/// Plays the ladle animation and disables cup cap renderer at spawn.
public class Spawner : MonoBehaviour
{
    [Serializable]
    public class ToppingEntry
    {
        // Prefab to spawn for this topping entry.
        public GameObject toppingPrefab;
        // Relative weight for weighted random selection (non-negative).
        public float spawnWeight = 1f;
    }
    [Header("Topping Prefabs")]
    [Tooltip("List of topping prefabs and their relative spawn weights. Zero or negative weights are treated as 0.")]
    public ToppingEntry[] toppingPrefabs = new ToppingEntry[0];
    [Tooltip("Optional bomb topping prefab spawned as a combo reward.")]
    public GameObject bombToppingPrefab;

    [Header("Spawn Timing")]
    [Tooltip("How frequently SpawnMore/InitialSpawn spaces items (seconds between spawns).")]
    public float spawnInterval = 0.25f;
    [Tooltip("Number of items spawned in a single 'batch' call.")]
    public int spawnBatchCount = 1;

    [Header("Spawn Positioning")]
    [Tooltip("Parent transform under which spawned toppings are placed. If null, this Spawner's transform is used.")]
    public Transform parentContainer;
    [Tooltip("Minimum local X offset from the spawner's position for spawned toppings.")]
    public float horizontalMin = -2.5f;
    [Tooltip("Maximum local X offset from the spawner's position for spawned toppings.")]
    public float horizontalMax = 2.5f;
    [Tooltip("Minimum rotation (degrees) applied to spawned toppings.")]
    public float spawnRotationMin = 0f;
    [Tooltip("Maximum rotation (degrees) applied to spawned toppings.")]
    public float spawnRotationMax = 360f;

    [Header("Initial Population")]
    [Tooltip("Number of toppings to spawn at Start to create an initial board/population. Set to 0 to disable initial spawning.")]
    public int initialSpawnLimit = 20;
    [Tooltip("Spacing in seconds between spawns when calling SpawnMore or during the initial spawn coroutine.")]
    public float spawnMoreSpacing = 0.05f;

    [Header("Ladle Settings")]
    [Tooltip("Animator component attached to this ladle.")]
    public Animator ladleAnimator;
    [Tooltip("Animation trigger name for ladle throw.")]
    public string ladleTriggerName = "Throw";

    [Header("Cup Cap")]
    [Tooltip("Renderer component of the cup cap to hide when spawning.")]
    public Renderer cupCapRenderer;

    private float totalWeight;

    private void Awake()
    {
        // Calculate total weight
        totalWeight = 0f;
        foreach(var entry in toppingPrefabs) if(entry != null) totalWeight += Mathf.Max(0f, entry.spawnWeight);
        if(parentContainer == null) parentContainer = this.transform;
        // Ensure animator and cup cap references
        if(ladleAnimator == null) ladleAnimator = GetComponent<Animator>();
        if(cupCapRenderer != null) cupCapRenderer.enabled = true; // start visible
    }

    void Start()
    {
        if(initialSpawnLimit > 0)
            StartCoroutine(InitialSpawnCoroutine(initialSpawnLimit));
    }

    // Public method to spawn more toppings over time
    public void SpawnMore(int count)
    {
        if(count <= 0) return;
        StartCoroutine(SpawnMoreCoroutine(count));
    }

    public void Refill(int count, bool immediate = false)
    {
        if(count <= 0) return;
        if(immediate)
        {
            // Immediate refill in same frame; use with care for performance.
            for(int i = 0; i < count; i++) SpawnOne();
            return;
        }
        // Default behaviour: paced spawn using existing SpawnMore infrastructure.
        SpawnMore(count);
    }

    IEnumerator SpawnMoreCoroutine(int count)
    {
        int spawned = 0;
        while(spawned < count)
        {
            SpawnOneWithAnimation();
            spawned++;
            yield return new WaitForSeconds(spawnMoreSpacing);
        }
    }

    IEnumerator InitialSpawnCoroutine(int count)
    {
        int spawned = 0;
        while(spawned < count)
        {
            SpawnOneWithAnimation();
            spawned++;
            yield return new WaitForSeconds(spawnMoreSpacing);
        }
    }

    // Wrapper to handle animation, cup cap, and actual spawn
    private void SpawnOneWithAnimation()
    {
        // Play ladle animation
        if(ladleAnimator != null && !string.IsNullOrEmpty(ladleTriggerName))
            ladleAnimator.SetTrigger(ladleTriggerName);

        // Disable cup cap renderer temporarily
        if(cupCapRenderer != null)
            cupCapRenderer.enabled = false;

        // Spawn the topping
        SpawnOne();
    }

    // Spawn one normal topping using weighted random selection
    public GameObject SpawnOne()
    {
        if(toppingPrefabs == null || toppingPrefabs.Length == 0) return null;

        float pick = UnityEngine.Random.value * Mathf.Max(0.0001f, totalWeight);
        float running = 0f;
        GameObject chosenPrefab = null;
        foreach(var entry in toppingPrefabs)
        {
            if(entry == null) continue;
            running += Mathf.Max(0f, entry.spawnWeight);
            if(pick <= running)
            {
                chosenPrefab = entry.toppingPrefab;
                break;
            }
        }

        if(chosenPrefab == null)
        {
            int attempts = 0;
            while(chosenPrefab == null && attempts < toppingPrefabs.Length)
            {
                var candidate = toppingPrefabs[UnityEngine.Random.Range(0, toppingPrefabs.Length)];
                if(candidate != null) chosenPrefab = candidate.toppingPrefab;
                attempts++;
            }
            if(chosenPrefab == null) return null;
        }

        return SpawnPrefabAtRandomPosition(chosenPrefab);
    }

    public GameObject SpawnBombTopping()
    {
        if(bombToppingPrefab == null)
        {
            Debug.LogWarning("Spawner: bombToppingPrefab not assigned, cannot spawn bomb reward.", this);
            return null;
        }
        return SpawnPrefabAtRandomPosition(bombToppingPrefab);
    }

    GameObject SpawnPrefabAtRandomPosition(GameObject prefab)
    {
        if(prefab == null) return null;
        Vector3 spawnPosition = transform.position;
        spawnPosition.x += UnityEngine.Random.Range(horizontalMin, horizontalMax);
        Quaternion spawnRotation = Quaternion.AngleAxis(UnityEngine.Random.Range(spawnRotationMin, spawnRotationMax), Vector3.forward);
        GameObject spawned = Instantiate(prefab, spawnPosition, spawnRotation, parentContainer);
        return spawned;
    }
}