using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawns topping prefabs from the top of the screen using weights and a coroutine.
public class Spawner : MonoBehaviour
{
    [System.Serializable]
    public class ToppingEntry
    {
        public GameObject toppingPrefab; // Prefab for this topping type
        public float spawnWeight = 1f; // Relative weight for random selection
    }

    public ToppingEntry[] toppingPrefabs = new ToppingEntry[0]; // list of toppings and weights
    public float spawnInterval = 0.25f; // seconds between spawns
    public int spawnBatchCount = 1; // number of toppings per spawn
    public float horizontalMin = -2.5f; // left x offset relative to spawner
    public float horizontalMax = 2.5f; // right x offset relative to spawner
    public float spawnRotationMin = 0f; // min random rotation (deg)
    public float spawnRotationMax = 360f; // max random rotation (deg)
    public Transform parentContainer; // optional parent for spawned items

    private float totalWeight; // cached sum of weights

    void Awake()
    {
        // Calculate total weight for weighted random selection
        totalWeight = 0f;
        foreach(var entry in toppingPrefabs) if(entry != null) totalWeight += Mathf.Max(0f, entry.spawnWeight);
        if(parentContainer == null) parentContainer = this.transform;
    }

    void Start()
    {
        // Start the regular spawn coroutine
        StartCoroutine(SpawnCoroutine());
    }

    IEnumerator SpawnCoroutine()
    {
        while(true)
        {
            for(int i = 0; i < spawnBatchCount; i++)
            {
                SpawnOne();
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // Spawn a single topping using weighted random selection
    public GameObject SpawnOne()
    {
        if(toppingPrefabs == null || toppingPrefabs.Length == 0) return null;

        float pick = Random.value * totalWeight;
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
        Vector3 spawnPosition = transform.position;
        spawnPosition.x += Random.Range(horizontalMin, horizontalMax);
        Quaternion spawnRotation = Quaternion.AngleAxis(Random.Range(spawnRotationMin, spawnRotationMax), Vector3.forward);

        GameObject spawned = Instantiate(chosenPrefab, spawnPosition, spawnRotation, parentContainer);
        return spawned;
    }

    // Regenerate totals if you change weights at runtime
    public void RecalculateWeights()
    {
        totalWeight = 0f;
        foreach(var entry in toppingPrefabs) if(entry != null) totalWeight += Mathf.Max(0f, entry.spawnWeight);
    }

    // Return all tags defined by current topping prefab list
    public string[] GetAllToppingTags()
    {
        List<string> tags = new List<string>();
        foreach(var entry in toppingPrefabs)
        {
            if(entry == null || entry.toppingPrefab == null) continue;
            var tag = entry.toppingPrefab.tag;
            if(!string.IsNullOrEmpty(tag) && !tags.Contains(tag)) tags.Add(tag);
        }
        return tags.ToArray();
    }
}