using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Core game logic for chaining and clearing toppings (connect 3+ to clear).
public class Logic : MonoBehaviour
{
    public LayerMask toppingLayerMask; // layer where toppings live
    public float overlapPointRadius = 0.05f; // radius for overlap point check
    public float holdToRegisterDelay = 0f; // optional small delay before registering hold

    public GameObject dotPrefab; // small dot prefab to show on connected toppings
    public Transform dotContainer; // parent for created dots

    public Spawner spawnerReference; // reference to Spawner for regeneration calls

    readonly List<GameObject> storedToppings = new List<GameObject>(); // stored chain objects
    readonly List<GameObject> createdDots = new List<GameObject>(); // dot visuals

    private string storedToppingTag = null; // tag we are matching during current chain
    private bool inputActive = false; // whether a press is in progress

    void Awake()
    {
        if(dotContainer == null) dotContainer = this.transform;
    }

    // Called by Controller when touch begins
    public void OnPress(Vector3 worldPosition)
    {
        inputActive = true;
        ResetChain(); // start fresh on each press
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
            // not enough, play fail sound or reset chain
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

        // if first item, set the stored tag
        if(storedToppings.Count == 0)
        {
            storedToppingTag = hitObject.tag;
        }

        // only accept if tag matches stored tag
        if(storedToppingTag != hitObject.tag) return;

        // prevent repeats
        if(storedToppings.Contains(hitObject)) return;

        // add to list and create dot visual
        storedToppings.Add(hitObject);
        CreateDotForTopping(hitObject);

        // notify Controller for visuals (so it can show line & last pointer)
        var controller = FindFirstObjectByType<Controller>();
        if(controller != null)
        {
            List<Transform> transforms = new List<Transform>();
            foreach(var go in storedToppings) transforms.Add(go.transform);
            controller.SetConnectedToppings(transforms);
        }

        // optional: play hit sound, increase pitch, etc. per lesson.
    }

    // Create small dot at topping position and store it for later removal
    void CreateDotForTopping(GameObject topping)
    {
        if(dotPrefab == null) return;
        var dot = Instantiate(dotPrefab, topping.transform.position, Quaternion.identity, dotContainer);
        createdDots.Add(dot);
    }

    // Reset chain and remove dot visuals
    void ResetChain()
    {
        storedToppings.Clear();
        storedToppingTag = null;
        RemoveDots();
        var controller = FindFirstObjectByType<Controller>();
        if(controller != null) controller.SetConnectedToppings(new List<Transform>());
    }

    void RemoveDots()
    {
        foreach(var d in createdDots) if(d != null) Destroy(d);
        createdDots.Clear();
    }

    // Coroutine that removes stored items one by one with a short spacing (lesson style)
    IEnumerator ClearStoredCoroutine()
    {
        // copy and prepare
        GameObject[] itemsToClear = storedToppings.ToArray();
        ResetChain(); // reset early so player can start new chain while clear animates

        for(int i = 0; i < itemsToClear.Length; i++)
        {
            var item = itemsToClear[i];
            if(item == null) continue;

            // disable touching by moving to IgnoreTouch layer (lesson suggests layer 31)
            item.layer = LayerMask.NameToLayer("IgnoreTouch");

            // call Remove as the lesson instructs (prefab's Explode will handle animation + destroy)
            item.SendMessage("Remove", SendMessageOptions.DontRequireReceiver);

            // spacing between each clear
            yield return new WaitForSeconds(0.1f);
        }

        // ask spawner to refill to balance lost items (lesson suggests calling spawner regenerate)
        if(spawnerReference != null) spawnerReference.StartCoroutine(RefillCoroutine(itemsToClear.Length));
    }

    // Example refill coroutine that asks spawner to spawn N items with a slight delay
    IEnumerator RefillCoroutine(int count)
    {
        int spawned = 0;
        while(spawned < count)
        {
            spawnerReference.SpawnOne();
            spawned++;
            yield return new WaitForSeconds(0.05f);
        }
    }
}