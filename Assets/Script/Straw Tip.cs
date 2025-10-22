using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// When a topping enters the straw tip trigger, apply a radial + lateral impulse,
/// swap its visual depth (scale + sorting order) to simulate moving to the other side,
/// and provide a callable wind/gust function that enables an effector GameObject then applies a final gust.
[RequireComponent(typeof(Collider2D))]
public class StrawTipPush : MonoBehaviour
{
    [Tooltip("Main outward push strength applied as an impulse when a topping touches the tip.")]
    public float radialImpulse = 120f;

    [Tooltip("Sideways push strength applied as an impulse to move object across the straw.")]
    public float lateralImpulse = 60f;

    [Tooltip("Torque impulse applied for a little rotational toss on entry.")]
    public float torqueImpulse = 30f;

    [Tooltip("Scale factor applied to an object when it is considered 'behind' the straw.")]
    public float behindScale = 0.92f;

    [Tooltip("Sorting order to set when the object is in front of the straw.")]
    public int frontSortingOrder = 5;

    [Tooltip("Sorting order to set when the object is behind the straw.")]
    public int behindSortingOrder = -5;

    [Tooltip("Which layers will be affected by the straw tip (set in Inspector to your toppings layer).")]
    public LayerMask affectedLayers;

    [Tooltip("Toggle visual depth change on enter/exit (scale + sorting order).")]
    public bool toggleDepthOnEnter = true;

    // Wind / effector support
    [Tooltip("Optional wind effect GameObject that contains an Effector2D (AreaEffector2D or PointEffector2D) and a Collider2D used by the effector. " +
             "The object will be enabled while wind is active then restored.")]
    public GameObject windEffectObject;

    [Tooltip("Default time in seconds to enable the wind effect if TriggerWindForSeconds is called without argument.")]
    public float defaultWindActiveDuration = 2f;

    [Tooltip("Radius in world units for the final one-shot gust impulse applied at the end of the wind active duration.")]
    public float gustRadius = 1.5f;

    [Tooltip("Strength of the final one-shot gust impulse applied at the end of the wind active duration.")]
    public float gustImpulseStrength = 200f;

    [Tooltip("Small upward bias applied to final gust impulses so objects lift slightly (0 = none).")]
    public float gustUpwardBias = 0.1f;

    [Tooltip("Optional multiplier applied to gust impulse based on object's Rigidbody2D.mass (impulseScale = gustImpulseStrength / mass).")]
    public float gustMassScale = 1f;

    // Internal helper to remember original visuals for each renderer
    private class VisualBackup
    {
        public Vector3 scale;
        public int sortingOrder;
    }

    // Map Renderer -> backup info
    private readonly Dictionary<Renderer, VisualBackup> rendererBackups = new Dictionary<Renderer, VisualBackup>();

    // Cached wind active state so we restore previous state after wind ends
    private bool cachedWindActiveState = false;

    private void Awake()
    {
        // Ensure the wind object starts disabled
        windEffectObject.SetActive(false);
    }

    void Reset()
    {
        // Ensure the collider is a trigger so OnTrigger callbacks fire as expected
        Collider2D colliderComponent = GetComponent<Collider2D>();
        if(colliderComponent != null) colliderComponent.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D otherCollider)
    {
        // Ignore objects that are not on the affected layers
        if((affectedLayers.value & (1 << otherCollider.gameObject.layer)) == 0) return;

        Rigidbody2D rigidbody = otherCollider.attachedRigidbody;
        if(rigidbody == null) return;

        // Compute direction from straw tip to the object (world space)
        Vector2 directionFromTipToObject = (otherCollider.transform.position - transform.position).normalized;
        if(directionFromTipToObject.sqrMagnitude < 1e-6f) directionFromTipToObject = Vector2.up; // Fallback

        // Lateral direction perpendicular to radial gives a side-push
        Vector2 lateralDirection = new Vector2(-directionFromTipToObject.y, directionFromTipToObject.x);

        // Decide lateral sign to push the object across the straw side
        // Use dot with straw right vector to determine which side the object is on
        Vector2 strawRightVector = transform.right;
        float sideDot = Vector2.Dot(directionFromTipToObject, strawRightVector);
        float lateralSign = sideDot >= 0f ? 1f : -1f;

        // Build final impulse: radial outward + lateral sideways
        Vector2 finalImpulse = directionFromTipToObject * radialImpulse + lateralDirection * (lateralImpulse * lateralSign);

        // Apply impulse at object's centre; use ForceMode2D.Impulse for immediate shove
        rigidbody.AddForce(finalImpulse, ForceMode2D.Impulse);

        // Give a little spin so it feels tossed
        rigidbody.AddTorque(torqueImpulse * lateralSign, ForceMode2D.Impulse);

        // Visually send object behind straw (simulate depth)
        if(toggleDepthOnEnter)
        {
            Renderer rendererComponent = otherCollider.GetComponent<Renderer>();
            if(rendererComponent != null && !rendererBackups.ContainsKey(rendererComponent))
            {
                VisualBackup backup = new VisualBackup
                {
                    scale = otherCollider.transform.localScale,
                    sortingOrder = rendererComponent.sortingOrder
                };
                rendererBackups.Add(rendererComponent, backup);

                // Scale down slightly
                otherCollider.transform.localScale = otherCollider.transform.localScale * behindScale;
                // Change sorting order to go behind the straw
                rendererComponent.sortingOrder = behindSortingOrder;
            }
        }
    }

    void OnTriggerExit2D(Collider2D otherCollider)
    {
        // Restore visuals when object leaves tip area (optional)
        if((affectedLayers.value & (1 << otherCollider.gameObject.layer)) == 0) return;

        Renderer rendererComponent = otherCollider.GetComponent<Renderer>();
        if(rendererComponent != null && rendererBackups.TryGetValue(rendererComponent, out VisualBackup backup))
        {
            otherCollider.transform.localScale = backup.scale;
            rendererComponent.sortingOrder = backup.sortingOrder;
            rendererBackups.Remove(rendererComponent);
        }
    }

    // Public: Trigger wind for the default duration (defaultWindActiveDuration).
    public void TriggerWind()
    {
        TriggerWindForSeconds(defaultWindActiveDuration);
    }

    // Public: Trigger wind for a given number of seconds. Safe to call multiple times; each call starts its own routine.
    public void TriggerWindForSeconds(float seconds)
    {
        StartCoroutine(WindCoroutine(seconds));
    }

    // Coroutine: Enable windEffectObject, wait, apply final gust impulse, restore previous active state
    private IEnumerator WindCoroutine(float seconds)
    {
        if(windEffectObject == null)
        {
            Debug.LogWarning("StrawTipPush: windEffectObject not assigned — cannot enable effector.", this);
            yield break;
        }

        // Cache previous active state and enable wind object
        cachedWindActiveState = windEffectObject.activeSelf;
        windEffectObject.SetActive(true);

        // Wait for the requested duration so the effector can apply continuous force
        yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));

        // Apply one-off radial gust impulse to nearby toppings
        ApplyFinalGustImpulse();

        // Restore wind object's original active state
        windEffectObject.SetActive(cachedWindActiveState);
    }

    // One-shot radial gust: find rigidbodies within gustRadius and apply impulse away from tip
    private void ApplyFinalGustImpulse()
    {
        Vector2 origin = (Vector2)transform.position;

        // Overlap circle to find all colliders in radius that are on affectedLayers
        Collider2D[] overlapColliders = Physics2D.OverlapCircleAll(origin, gustRadius, affectedLayers);

        for(int index = 0; index < overlapColliders.Length; index++)
        {
            Collider2D overlapCollider = overlapColliders[index];
            if(overlapCollider == null) continue;

            Rigidbody2D rigidbody = overlapCollider.attachedRigidbody;
            if(rigidbody == null) continue;

            // Compute direction away from origin
            Vector2 directionAwayFromOrigin = (rigidbody.worldCenterOfMass - origin).normalized;
            if(directionAwayFromOrigin.sqrMagnitude < 1e-6f)
            {
                // If exactly at origin, push up
                directionAwayFromOrigin = Vector2.up;
            }

            // Apply small upward bias
            Vector2 biasedDirection = directionAwayFromOrigin + Vector2.up * gustUpwardBias;
            biasedDirection.Normalize();

            // Scale impulse by mass if desired
            float massScale = (rigidbody.mass > 0f) ? (1f / Mathf.Max(0.0001f, rigidbody.mass)) * gustMassScale : 1f;
            float impulseMagnitude = gustImpulseStrength * massScale;

            Vector2 finalImpulse = biasedDirection * impulseMagnitude;

            // Apply impulse at centre as an immediate shove
            rigidbody.AddForce(finalImpulse, ForceMode2D.Impulse);
        }
    }

    // Optional: visualize the gust radius in the editor for easier tuning
    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, gustRadius);
    }
}