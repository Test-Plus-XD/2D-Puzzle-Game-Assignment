using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawns topping prefabs with weights, supports initial spawn limit and manual spawn requests.
public class Spawner : MonoBehaviour
{
    [System.Serializable]
    public class ToppingEntry
    {
        public GameObject toppingPrefab; // Prefab for this topping type
        public float spawnWeight = 1f; // Relative weight for selection
    }

    public ToppingEntry[] toppingPrefabs = new ToppingEntry[0]; // toppings list
    public float spawnInterval = 0.25f; // time between automatic spawns
    public int spawnBatchCount = 1; // auto-spawn items per interval
    public Transform parentContainer; // parent for spawned objects
    public float horizontalMin = -2.5f; // spawn area left (local X)
    public float horizontalMax = 2.5f; // spawn area right (local X)
    public float spawnRotationMin = 0f; // random rotation min
    public float spawnRotationMax = 360f; // random rotation max

    public int initialSpawnLimit = 20; // number of toppings to create at scene start
    public float spawnMoreSpacing = 0.05f; // small delay when spawning multiple via SpawnMore

    private float totalWeight; // cached weight sum

    void Awake()
    {
        // Calculate total weight
        totalWeight = 0f;
        foreach(var entry in toppingPrefabs) if(entry != null) totalWeight += Mathf.Max(0f, entry.spawnWeight);
        if(parentContainer == null) parentContainer = this.transform;
    }

    void Start()
    {
        // Create initial pool up to the limit
        if(initialSpawnLimit > 0)
        {
            StartCoroutine(InitialSpawnCoroutine(initialSpawnLimit));
        }
    }

    // Automatic spawn loop removed: now waits for external calls to spawn more
    IEnumerator InitialSpawnCoroutine(int count)
    {
        int spawned = 0;
        while(spawned < count)
        {
            SpawnOne();
            spawned++;
            yield return new WaitForSeconds(spawnMoreSpacing);
        }
    }

    // Public method logic can call to spawn 'count' more toppings (spaced)
    public void SpawnMore(int count)
    {
        if(count <= 0) return;
        StartCoroutine(SpawnMoreCoroutine(count));
    }

    IEnumerator SpawnMoreCoroutine(int count)
    {
        int spawned = 0;
        while(spawned < count)
        {
            SpawnOne();
            spawned++;
            yield return new WaitForSeconds(spawnMoreSpacing);
        }
    }

    // Spawn one using weighted random selection
    public GameObject SpawnOne()
    {
        if(toppingPrefabs == null || toppingPrefabs.Length == 0) return null;

        // Weighted selection
        float pick = Random.value * Mathf.Max(0.0001f, totalWeight);
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
        if(chosenPrefab == null) chosenPrefab = toppingPrefabs[Random.Range(0, toppingPrefabs.Length)].toppingPrefab;

        // Random position within horizontal bounds (local to spawner)
        Vector3 spawnPosition = transform.position;
        spawnPosition.x += Random.Range(horizontalMin, horizontalMax);
        Quaternion spawnRotation = Quaternion.AngleAxis(Random.Range(spawnRotationMin, spawnRotationMax), Vector3.forward);

        GameObject spawned = Instantiate(chosenPrefab, spawnPosition, spawnRotation, parentContainer);
        return spawned;
    }

    // Recalculate weights at runtime if needed
    public void RecalculateWeights()
    {
        totalWeight = 0f;
        foreach(var entry in toppingPrefabs) if(entry != null) totalWeight += Mathf.Max(0f, entry.spawnWeight);
    }

    // Return array of tags from topping prefabs
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
}