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

    // Camera zoom / pan settings
    public bool enableCameraFollow = true; // Toggle camera follow behaviour
    public float cameraFollowSmoothing = 0.12f; // Smoothing for camera movement
    public float cameraZoomInAmount = 0.8f; // Target orthographic size multiplier when zoomed in (0.8 => 20% zoom in)
    public float cameraZoomSmoothing = 0.12f; // Smoothing for camera zoom transitions
    public float minCameraSize = 3f; // Minimum orthographic size clamp
    public float maxCameraSize = 12f; // Maximum orthographic size clamp
    [Tooltip("Weight for the last connected topping when computing the camera focus point (0..1). 0.75 means last topping contributes 75% and finger 25%.")]
    public float cameraFocusWeightForLastTopping = 0.75f;
    [Tooltip("Multiplier that controls how much camera follow speed is reduced with distance from world origin. Larger values slow more aggressively.")]
    public float cameraFollowSlowdownMultiplier = 0.12f;
    [Tooltip("Minimum allowed smoothing value for camera follow (slower).")]
    public float cameraFollowMinSmoothing = 0.02f;
    [Tooltip("Maximum allowed smoothing value for camera follow (faster).")]
    public float cameraFollowMaxSmoothing = 0.18f;

    public Logic gameLogic; // Cached Logic reference

    private LineRenderer lineRenderer; // For chain rendering
    private GameObject pointerInstance; // Finger pointer instance
    private List<Transform> connectedToppingTransforms = new List<Transform>(); // Transforms for line points
    readonly List<GameObject> createdDots = new List<GameObject>(); // Created dots
    private Vector3 smoothedWorldPosition = Vector3.zero; // Smoothed finger position
    private bool isTouching = false; // Whether input is active
    // Camera runtime values
    private Vector3 initialCameraPosition;
    private float initialCameraSize;
    private float targetCameraSize;
    private Vector3 targetCameraPosition;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if(mainCamera == null) mainCamera = Camera.main;
        // Ensure LineRenderer settings for tiled sprite and camera-facing alignment
        lineRenderer.positionCount = 0;

        audioSource = gameObject.AddComponent<AudioSource>(); // Add audio source component if not present

        // Cache camera initial values
        if(mainCamera != null)
        {
            initialCameraPosition = mainCamera.transform.position;
            initialCameraSize = Mathf.Clamp(mainCamera.orthographicSize, minCameraSize, maxCameraSize);
            targetCameraSize = initialCameraSize;
            targetCameraPosition = initialCameraPosition;
        }
    }

    private void Start()
    {
        // Create finger pointer instance if prefab assigned
        if(pointerPrefab != null)
        {
            pointerInstance = Instantiate(pointerPrefab, transform);
            pointerInstance.SetActive(false);
        }

        if(dotContainer == null) dotContainer = this.transform;
    }

    private void Update()
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

        // Rotate pointer to face from last connected topping to the finger
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
            pointerInstance.transform.rotation = Quaternion.identity;
        }

        // Update line visuals
        UpdateLineVisual(inputHeld, smoothedWorldPosition);

        // Update dot positions each frame
        UpdateDots();

        // Camera follow & zoom while dragging
        UpdateCameraFollow(inputHeld);

        // Notify Logic using cached reference
        if(inputDown && gameLogic != null) gameLogic.OnPress(rawWorldPosition);
        if(inputHeld && gameLogic != null) gameLogic.OnHold(rawWorldPosition);
        if(inputUp && gameLogic != null) gameLogic.OnRelease(rawWorldPosition);
    }

    // Called by Logic when the stored toppings change so visuals update
    public void SetConnectedToppings(List<Transform> toppingTransforms)
    {
        connectedToppingTransforms = toppingTransforms ?? new List<Transform>();

        // Ensure dot instances match transforms count
        while(createdDots.Count < connectedToppingTransforms.Count) CreateDotForTopping();
        while(createdDots.Count > connectedToppingTransforms.Count)
        {
            GameObject last = createdDots[createdDots.Count - 1];
            createdDots.RemoveAt(createdDots.Count - 1);
            if(last != null) Destroy(last);
        }

        UpdateDots();
    }

    // Create small dot instance and register it
    public void CreateDotForTopping()
    {
        if(dotPrefab == null) return;
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
                Transform toppingTransform = connectedToppingTransforms[i];
                GameObject dot = createdDots[i];
                if(toppingTransform != null && dot != null) dot.transform.position = toppingTransform.position;
            }
        }
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
        float pitch = basePitch + Mathf.Max(0, connectedToppingTransforms.Count - 1) * pitchIncrease;
        pitch = Mathf.Clamp(pitch, 0.5f, 3f);
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(chainSound);
    }

    // Smooth camera follow & zoom while dragging, with position clamped to +-2 in X and Y axes
    public void UpdateCameraFollow(bool isDragging)
    {
        // If camera is missing or camera follow disabled, smoothly restore to initial values
        if(mainCamera == null || !enableCameraFollow)
        {
            if(mainCamera != null)
            {
                mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, initialCameraPosition, cameraFollowSmoothing);
                mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, initialCameraSize, cameraZoomSmoothing);
            }
            return;
        }

        // Default to initial camera targets
        targetCameraPosition = initialCameraPosition;
        targetCameraSize = initialCameraSize;

        if(isDragging && connectedToppingTransforms.Count > 0)
        {
            // Compute weighted focus point between last topping and finger
            Transform lastToppingTransform = connectedToppingTransforms[connectedToppingTransforms.Count - 1];
            Vector3 lastToppingPosition = lastToppingTransform.position;
            Vector3 fingerPosition = smoothedWorldPosition;

            float lastWeight = Mathf.Clamp01(cameraFocusWeightForLastTopping);
            float fingerWeight = 1f - lastWeight;

            // Weighted focus point
            Vector3 weightedFocus = lastToppingPosition * lastWeight + fingerPosition * fingerWeight;
            weightedFocus.z = mainCamera.transform.position.z; // Keep camera Z constant

            // Compute distance from world origin to slow follow when far away
            float focusDistanceFromOrigin = weightedFocus.magnitude;
            float distanceFactor = 1f / (1f + focusDistanceFromOrigin * cameraFollowSlowdownMultiplier);
            float adaptiveSmoothing = Mathf.Lerp(cameraFollowMinSmoothing, cameraFollowMaxSmoothing, distanceFactor);

            // Apply clamping to restrict camera X and Y movement within +-2
            weightedFocus.x = Mathf.Clamp(weightedFocus.x, -2f, 2f);
            weightedFocus.y = Mathf.Clamp(weightedFocus.y, -2f, 2f);

            // Update target position and zoom
            targetCameraPosition = weightedFocus;
            targetCameraSize = Mathf.Clamp(initialCameraSize * cameraZoomInAmount, minCameraSize, maxCameraSize);

            // Smoothly interpolate camera position and size
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCameraPosition, adaptiveSmoothing);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetCameraSize, cameraZoomSmoothing);
            return;
        }
        // When not dragging, return smoothly to initial position and zoom
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, initialCameraPosition, cameraFollowSmoothing);
        mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, initialCameraSize, cameraZoomSmoothing);
    }
}