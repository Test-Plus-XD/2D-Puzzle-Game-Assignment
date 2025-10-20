using System.Collections.Generic;
using UnityEngine;

/// Handles player touch/mouse input and renders the connecting line and finger pointer.
[RequireComponent(typeof(LineRenderer))]
public class Controller : MonoBehaviour
{
    public Camera mainCamera; // Assign main camera (defaults to Camera.main)
    public AudioSource audioSource; // Audio source to play the sound
    public LayerMask interactableLayerMask; // Layer mask for toppings
    public float followSmoothing = 0.03f; // Smoothing for finger pointer movement

    public GameObject pointerPrefab; // Pointer sprite following the finger
    public GameObject dotPrefab; // Small dot prefab to show on connected toppings
    public Transform dotContainer; // Parent for created dots

    public AudioClip chainSound; // Sound played when a new topping is added to the chain
    public float basePitch = 1f; // Base pitch for the sound
    public float pitchIncrease = 0.1f; // Pitch increase per new chain increment

    private LineRenderer lineRenderer; // For chain rendering
    private GameObject pointerInstance; // Finger pointer instance
    private List<Transform> connectedToppingTransforms = new List<Transform>(); // Transforms for line points
    readonly List<GameObject> createdDots = new List<GameObject>(); // Created dots
    private Vector3 smoothedWorldPosition = Vector3.zero; // Smoothed finger position
    private bool isTouching = false; // Whether input is active

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if(mainCamera == null) mainCamera = Camera.main;
        // Ensure LineRenderer settings for tiled sprite and camera-facing alignment
        lineRenderer.positionCount = 0;

        audioSource = gameObject.AddComponent<AudioSource>(); // Add audio source component if not present
    }

    void Start()
    {
        // Create finger pointer instance if prefab assigned
        if(pointerPrefab != null)
        {
            pointerInstance = Instantiate(pointerPrefab, transform);
            pointerInstance.SetActive(false);
        }
    }

    void Update()
    {
        // Read input (mouse or first touch)
        Vector3 rawWorldPosition;
        bool inputDown = false;
        bool inputHeld = false;
        bool inputUp = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        if(Input.GetMouseButtonDown(0)) { inputDown = true; isTouching = true; }
        if(Input.GetMouseButton(0)) { inputHeld = true; isTouching = true; }
        if(Input.GetMouseButtonUp(0)) { inputUp = true; isTouching = false; }
        rawWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
#else
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) { inputDown = true; isTouching = true; }
            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) { inputHeld = true; isTouching = true; }
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) { inputUp = true; isTouching = false; }
            rawWorldPosition = mainCamera.ScreenToWorldPoint(touch.position);
        }
        else
        {
            rawWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        }
#endif

        rawWorldPosition.z = 0f;

        // Smooth finger pointer movement
        smoothedWorldPosition = Vector3.Lerp(smoothedWorldPosition, rawWorldPosition, Mathf.Clamp01(Time.deltaTime / Mathf.Max(1e-6f, followSmoothing)));

        // Show/hide finger pointer
        if(pointerInstance != null)
        {
            pointerInstance.SetActive(inputHeld);
            pointerInstance.transform.position = smoothedWorldPosition;
        }

        // Rotate finger pointer to face from last connected topping to the finger
        if(pointerInstance != null && connectedToppingTransforms.Count > 0)
        {
            Transform lastTransform = connectedToppingTransforms[connectedToppingTransforms.Count - 1];
            Vector3 fromLastToFinger = (smoothedWorldPosition - lastTransform.position).normalized;
            if(fromLastToFinger.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(fromLastToFinger.y, fromLastToFinger.x) * Mathf.Rad2Deg;
                angle -= 90f; // Apply the -90 degree adjustment
                pointerInstance.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        } else if(pointerInstance != null)
        {
            // If no last topping, pointer faces upward by default
            pointerInstance.transform.rotation = Quaternion.identity;
        }

        // Update line visuals
        UpdateLineVisual(inputHeld, smoothedWorldPosition);

        // Update dot positions
        UpdateDots();

        // Notify Logic using Unity6 API
        if(inputDown) TryNotifyLogicPress(rawWorldPosition);
        if(inputHeld) TryNotifyLogicHold(rawWorldPosition);
        if(inputUp) TryNotifyLogicRelease(rawWorldPosition);
    }

    // Called by Logic when the stored toppings change so visuals update
    public void SetConnectedToppings(List<Transform> toppingTransforms)
    {
        // Replace transform list
        connectedToppingTransforms = toppingTransforms ?? new List<Transform>();

        // Ensure createdDots list has the same count as transforms
        // Create missing dots
        while(createdDots.Count < connectedToppingTransforms.Count)
        {
            CreateDotForTopping(); // helper creates a dot GameObject and adds to createdDots
        }

        // Destroy extra dots if player removed items (safety)
        while(createdDots.Count > connectedToppingTransforms.Count)
        {
            GameObject last = createdDots[createdDots.Count - 1];
            createdDots.RemoveAt(createdDots.Count - 1);
            if(last != null) Destroy(last);
        }
        // Immediately update positions
        UpdateDots();
    }

    // Create small dot at topping position and store it for later removal
    public void CreateDotForTopping()
    {
        if(dotPrefab == null) return;

        // Instantiate the dot prefab for the topping
        GameObject dot = Instantiate(dotPrefab, Vector3.zero, Quaternion.identity, dotContainer);
        createdDots.Add(dot);
    }

    // Update the dot's position to follow each chained topping
    private void UpdateDots()
    {
        for(int i = 0; i < createdDots.Count; i++)
        {
            if(i < connectedToppingTransforms.Count)
            {
                // Move the dot to the corresponding topping's position
                Transform toppingTransform = connectedToppingTransforms[i];
                GameObject dot = createdDots[i];
                if(toppingTransform != null && dot != null)
                {
                    dot.transform.position = toppingTransform.position;
                }
            }
        }
    }

    // Helper to clear all dots (callable to force clear)
    public void ClearDots()
    {
        for(int i = 0; i < createdDots.Count; i++) if(createdDots[i] != null) Destroy(createdDots[i]);
        createdDots.Clear();
    }

    // Update the LineRenderer positions to match connected toppings and finger
    private void UpdateLineVisual(bool includeFingerEnd, Vector3 fingerPosition)
    {
        int baseCount = connectedToppingTransforms.Count;
        int extra = includeFingerEnd ? 1 : 0;
        lineRenderer.positionCount = baseCount + extra;

        for(int i = 0; i < baseCount; i++)
        {
            Vector3 localPos = transform.InverseTransformPoint(connectedToppingTransforms[i].position);
            lineRenderer.SetPosition(i, localPos);
        }

        if(includeFingerEnd)
        {
            Vector3 localFinger = transform.InverseTransformPoint(fingerPosition);
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, localFinger);
        }
        lineRenderer.enabled = (lineRenderer.positionCount > 0);
    }

    // Play the chain increment sound and adjust pitch
    public void PlayChainIncrementSound()
    {
        if(chainSound == null || audioSource == null) return;

        // Increase pitch based on chain length
        float pitch = basePitch + Mathf.Max(0, connectedToppingTransforms.Count - 1) * pitchIncrease;
        pitch = Mathf.Clamp(pitch, 0.25f, 3f); // Keep pitch reasonable
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(chainSound);
    }

    // Use Unity 6 recommended API to find Logic
    static void TryNotifyLogicPress(Vector3 worldPosition)
    {
        var logic = Object.FindFirstObjectByType<Logic>();
        if(logic != null) logic.OnPress(worldPosition);
    }

    static void TryNotifyLogicHold(Vector3 worldPosition)
    {
        var logic = Object.FindFirstObjectByType<Logic>();
        if(logic != null) logic.OnHold(worldPosition);
    }

    static void TryNotifyLogicRelease(Vector3 worldPosition)
    {
        var logic = Object.FindFirstObjectByType<Logic>();
        if(logic != null) logic.OnRelease(worldPosition);
    }
}