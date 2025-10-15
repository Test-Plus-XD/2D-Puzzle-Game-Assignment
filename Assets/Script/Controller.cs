using System.Collections.Generic;
using UnityEngine;

/// Handles player touch/mouse input and renders the connecting line and pointers.
[RequireComponent(typeof(LineRenderer))]
public class Controller : MonoBehaviour
{
    public Camera mainCamera; // assign main camera (defaults to Camera.main)
    public LayerMask interactableLayerMask; // layer mask for toppings
    public float fingerFollowSmoothing = 0.02f; // lerp smoothing for finger pointer

    public GameObject fingerPointerPrefab; // pointer sprite following the finger
    public GameObject lastToppingPointerPrefab; // pointer sprite at the last connected topping

    private LineRenderer lineRenderer; // line renderer used to draw chain
    private GameObject fingerPointerInstance; // instance for finger
    private GameObject lastToppingPointerInstance; // instance for last topping
    private List<Transform> connectedToppingTransforms = new List<Transform>(); // for visual chain
    private Vector3 smoothedFingerWorldPosition = Vector3.zero; // smoothed position
    private bool isTouching = false; // whether input is active

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if(mainCamera == null) mainCamera = Camera.main;
        // ensure LineRenderer settings for tiled sprite
        lineRenderer.useWorldSpace = false; // match Lesson 04 recommendation
        lineRenderer.positionCount = 0;
    }

    void Start()
    {
        // create pointer instances if prefabs assigned
        if(fingerPointerPrefab != null)
        {
            fingerPointerInstance = Instantiate(fingerPointerPrefab, transform);
            fingerPointerInstance.SetActive(false);
        }
        if(lastToppingPointerPrefab != null)
        {
            lastToppingPointerInstance = Instantiate(lastToppingPointerPrefab, transform);
            lastToppingPointerInstance.SetActive(false);
        }
    }

    void Update()
    {
        // update input: support mouse or first touch only
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

        // smooth finger pointer movement
        smoothedFingerWorldPosition = Vector3.Lerp(smoothedFingerWorldPosition, rawWorldPosition, Mathf.Clamp01(Time.deltaTime / Mathf.Max(1e-6f, fingerFollowSmoothing)));

        // show/hide finger pointer
        if(fingerPointerInstance != null) fingerPointerInstance.SetActive(inputHeld);
        if(fingerPointerInstance != null) fingerPointerInstance.transform.position = smoothedFingerWorldPosition;

        // update line rendering points from current connected list + finger position
        UpdateLineVisual(inputHeld, smoothedFingerWorldPosition);

        // show/hide last topping pointer
        if(lastToppingPointerInstance != null)
        {
            if(connectedToppingTransforms.Count > 0)
            {
                var lastTransform = connectedToppingTransforms[connectedToppingTransforms.Count - 1];
                lastToppingPointerInstance.SetActive(true);
                lastToppingPointerInstance.transform.position = lastTransform.position;
            } else lastToppingPointerInstance.SetActive(false);
        }

        // forward input events to the logic system by using events or FindObjectOfType
        // (Logic will manage storing the touched items). Call the methods here as necessary.
        if(inputDown) TryNotifyLogicPress(rawWorldPosition);
        if(inputHeld) TryNotifyLogicHold(rawWorldPosition);
        if(inputUp) TryNotifyLogicRelease(rawWorldPosition);
    }

    // Called by Logic when a topping is added/removed so the visual chain updates
    public void SetConnectedToppings(List<Transform> toppingTransforms)
    {
        connectedToppingTransforms = toppingTransforms ?? new List<Transform>();
    }

    // Update LineRenderer positions to match connected toppings and current finger
    void UpdateLineVisual(bool includeFingerEnd, Vector3 fingerPosition)
    {
        int baseCount = connectedToppingTransforms.Count;
        int extra = includeFingerEnd ? 1 : 0;
        lineRenderer.positionCount = baseCount + extra;

        for(int i = 0; i < baseCount; i++)
        {
            // positions are local to the controller GameObject (lineRenderer.useWorldSpace = false)
            Vector3 localPos = transform.InverseTransformPoint(connectedToppingTransforms[i].position);
            lineRenderer.SetPosition(i, localPos);
        }

        if(includeFingerEnd)
        {
            Vector3 localFinger = transform.InverseTransformPoint(fingerPosition);
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, localFinger);
        }

        // If there are no points, reset
        if(lineRenderer.positionCount == 0) lineRenderer.enabled = false;
        else lineRenderer.enabled = true;
    }

    // These helper methods try to forward input to a Logic instance in the scene
    void TryNotifyLogicPress(Vector3 worldPosition)
    {
        var logic = FindFirstObjectByType<Logic>();
        if(logic != null) logic.OnPress(worldPosition);
    }

    void TryNotifyLogicHold(Vector3 worldPosition)
    {
        var logic = FindFirstObjectByType<Logic>();
        if(logic != null) logic.OnHold(worldPosition);
    }

    void TryNotifyLogicRelease(Vector3 worldPosition)
    {
        var logic = FindFirstObjectByType<Logic>();
        if(logic != null) logic.OnRelease(worldPosition);
    }
}