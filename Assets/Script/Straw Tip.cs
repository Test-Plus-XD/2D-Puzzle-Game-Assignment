using UnityEngine;

/// When a topping enters the straw tip trigger, apply a radial + lateral impulse
/// and swap its visual depth (scale + sorting order) to simulate moving to the other side.
[RequireComponent(typeof(Collider2D))]
public class StrawTipPush : MonoBehaviour
{
    public float radialImpulse = 120f; // main outward push strength (impulse)
    public float lateralImpulse = 60f; // sideways spin/push strength (impulse)
    public float torqueImpulse = 30f; // small torque to add rotation (optional)
    public float behindScale = 0.92f; // scale factor when object is "behind" the straw
    public int frontSortingOrder = 5; // sorting order when in front
    public int behindSortingOrder = -5; // sorting order to send behind
    public LayerMask affectedLayers; // which layers count as toppings
    public bool toggleDepthOnEnter = true; // change visuals on enter/exit

    // Simple container to remember original visuals per object
    class VisualBackup { public Vector3 scale; public int sorting; }
    private readonly System.Collections.Generic.Dictionary<Renderer, VisualBackup> backups = new();

    void Reset()
    {
        // Ensure the collider is a trigger for this component to work smoothly
        Collider2D c = GetComponent<Collider2D>();
        if(c != null) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore non-affected layers
        if((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody2D rb = other.attachedRigidbody;
        if(rb == null) return;

        // Compute direction from tip to object (world space)
        Vector2 dir = (other.transform.position - transform.position).normalized;
        if(dir.sqrMagnitude < 1e-6f) dir = Vector2.up; // fallback

        // Lateral direction perpendicular to radial (gives side-push)
        Vector2 lateral = new Vector2(-dir.y, dir.x);

        // Decide lateral sign to push the object across the straw side
        // Use dot with straw right vector to determine which side it's on
        Vector2 strawRight = transform.right;
        float sideDot = Vector2.Dot(dir, strawRight);
        float lateralSign = sideDot >= 0f ? 1f : -1f;

        // Build final impulse: radial outward + lateral sideways
        Vector2 impulse = dir * radialImpulse + lateral * (lateralImpulse * lateralSign);

        // Apply impulse at object's centre; use ForceMode2D.Impulse for immediate shove
        rb.AddForce(impulse, ForceMode2D.Impulse);

        // Give a little spin so it feels tossed
        rb.AddTorque(torqueImpulse * lateralSign, ForceMode2D.Impulse);

        // Visually send object behind straw (simulate depth)
        if(toggleDepthOnEnter)
        {
            Renderer rend = other.GetComponent<Renderer>();
            if(rend != null && !backups.ContainsKey(rend))
            {
                backups[rend] = new VisualBackup { scale = other.transform.localScale, sorting = rend.sortingOrder };
                // scale down slightly
                other.transform.localScale = other.transform.localScale * behindScale;
                // change sorting order to go behind the straw
                rend.sortingOrder = behindSortingOrder;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        //Restore visuals when object leaves tip area (optional)
        if((affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;

        Renderer rend = other.GetComponent<Renderer>();
        if(rend != null && backups.TryGetValue(rend, out VisualBackup b))
        {
            other.transform.localScale = b.scale;
            rend.sortingOrder = b.sorting;
            backups.Remove(rend);
        }
    }
}
