using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Core game logic for chaining and clearing toppings (connect 3+ to clear).
public class Logic : MonoBehaviour
{
    public LayerMask toppingLayerMask; // Layer where toppings live
    public float overlapPointRadius = 0.05f; // Radius for overlap point check
    public float holdToRegisterDelay = 0f; // Optional small delay before registering hold

    public Spawner spawner; // Reference to spawner to request more items
    public Controller controller; // Reference to controller to request UI

    readonly List<GameObject> storedToppings = new List<GameObject>(); // Stored chain objects
    private string storedToppingTag = null; // Tag we are matching during the current chain
    private bool inputActive = false; // Whether a press is in progress

    private void Awake()
    {
        // Cache references if not assigned
        if(controller == null) controller = FindFirstObjectByType<Controller>();
        if(spawner == null) spawner = FindFirstObjectByType<Spawner>();
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
    private void TryRegisterAtPosition(Vector3 worldPosition)
    {
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D hit = Physics2D.OverlapPoint(point, toppingLayerMask);
        if(hit == null) return;

        GameObject hitObject = hit.gameObject;

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
            RaycastHit2D[] hits = Physics2D.RaycastAll(start, direction, distance, toppingLayerMask);

            foreach(var h in hits)
            {
                if(h.collider == null) continue;
                GameObject obj = h.collider.gameObject;
                if(obj == lastTopping || obj == hitObject) continue;

                // If any other topping has a different tag, block the connection
                if(obj.tag != storedToppingTag)
                {
                    // Optional: play a small "blocked" sound or vibration feedback
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
    public void ResetChain()
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
        ResetChain(); // Reset early so player can start new chain while clear animates

        for(int i = 0; i < itemsToClear.Length; i++)
        {
            var item = itemsToClear[i];
            if(item == null) continue;

            // Call Remove as the lesson instructs (prefab's Explode will handle animation + destroy)
            item.SendMessage("Remove", SendMessageOptions.DontRequireReceiver);

            // Spacing between each clear
            yield return new WaitForSeconds(0.1f);
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
}