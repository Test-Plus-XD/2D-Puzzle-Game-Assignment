using System.Collections;
using System.Reflection;
using UnityEngine;

/// Controls HingeJoint2D motor and flips its direction when z-rotation stops changing (optional loop).
[RequireComponent(typeof(HingeJoint2D))]
public class Straw : MonoBehaviour
{
    [Tooltip("Toggle automatic looping when rotation stops changing.")]
    public bool loop = true;
    [Tooltip("Motor target speed magnitude in degrees/sec when not switching.")]
    public float maxSpeed = 60f;
    [Tooltip("Time in seconds to smoothly interpolate motor speed when switching.")]
    public float changeDuration = 1f;
    [Tooltip("Torque applied by the motor.")]
    public float maxMotorTorque = 1000f;
    [Tooltip("Degrees/sec below which rotation is considered stopped.")]
    public float stopAngularSpeedThreshold = 1f;
    [Tooltip("Seconds the angular speed must remain low to count as stopped.")]
    public float stopTimeThreshold = 0.06f;
    [Tooltip("Chance (0..1) that a blow will occur when the interval elapses (0.5 = 50%).")]
    [Range(0f, 1f)]
    public float blowChance = 0.5f;
    [Tooltip("Seconds between blow chance checks (when each interval passes Straw will roll the chance).")]
    public float blowInterval = 5f;
    [Tooltip("Reference to the StrawTipPush component that owns the wind behaviour. If not assigned the script will try to find one in children.")]
    public StrawTipPush strawTipPushReference;
    [Tooltip("Buoyancy Effector 2D to toggle when blowing. If not assigned the script will try to find one in children.")]
    public BuoyancyEffector2D buoyancyEffector;
    [Tooltip("Duration in seconds to enable the buoyancy effector when a blow occurs. Used if the straw tip does not expose its own duration.")]
    public float buoyancyActiveDuration = 0.5f;
    [Tooltip("Automatically toggle buoyancy during straw tip wind events.")]
    public bool toggleBuoyancyOnBlow = true;

    // Cached hinge joint
    private HingeJoint2D hinge;
    // Current desired motor speed (positive or negative)
    private float currentTargetSpeed;
    // Previous frame z-rotation
    private float prevZ;
    // Accumulates time while rotation is near-stopped
    private float stopTimer;
    // Whether we are currently interpolating motor speed
    private bool isSwitching;
    // When interpolation started
    private float switchStartTime;
    // Motor speed at start of interpolation
    private float switchStartSpeed;
    // Motor speed to reach at end of interpolation
    private float switchTargetSpeed;
    // Blow timing state
    private float blowIntervalTimer = 0f;
    // Running coroutine for timed buoyancy toggles
    private Coroutine buoyancyCoroutine;

    private void Start()
    {
        hinge = GetComponent<HingeJoint2D>(); // Cache hinge
        hinge.useMotor = true; // Ensure motor active
        currentTargetSpeed = maxSpeed; // Start with positive direction
        prevZ = transform.eulerAngles.z; // Initialise previous rotation

        // If strawTipPushReference not assigned, try to find one in children
        if(strawTipPushReference == null)
        {
            strawTipPushReference = GetComponentInChildren<StrawTipPush>();
        }

        // If buoyancyEffector not assigned, try to find one in children
        if(buoyancyEffector == null)
        {
            buoyancyEffector = GetComponentInChildren<BuoyancyEffector2D>();
        }

        // Ensure buoyancy effector is off by default
        if(buoyancyEffector != null)
        {
            buoyancyEffector.enabled = false;
        }

        // Clamp blowChance and blowInterval to valid ranges
        blowChance = Mathf.Clamp01(blowChance);
        blowInterval = Mathf.Max(0.01f, blowInterval);
        buoyancyActiveDuration = Mathf.Max(0f, buoyancyActiveDuration);
    }

    private void FixedUpdate()
    {
        // Motor rotation detection and switching (unchanged logic)
        float curZ = transform.eulerAngles.z;
        float deltaAngle = Mathf.DeltaAngle(prevZ, curZ);
        float angularSpeed = Mathf.Abs(deltaAngle) / Mathf.Max(Time.deltaTime, 1e-6f); // degrees/sec

        if(angularSpeed < stopAngularSpeedThreshold)
        {
            stopTimer += Time.deltaTime;
        } else
        {
            stopTimer = 0f;
        }

        if(!isSwitching && loop && stopTimer >= stopTimeThreshold)
        {
            BeginSwitch();
            stopTimer = 0f;
        }

        if(isSwitching)
        {
            float t = Mathf.Clamp01((Time.time - switchStartTime) / Mathf.Max(changeDuration, 1e-6f));
            float newSpeed = Mathf.Lerp(switchStartSpeed, switchTargetSpeed, t);

            ApplyMotor(newSpeed);

            if(t >= 1f)
            {
                isSwitching = false;
                currentTargetSpeed = switchTargetSpeed;
            }
        } else
        {
            ApplyMotor(currentTargetSpeed);
        }

        prevZ = curZ;

        // Blow scheduling: accumulate interval timer and roll chance when it elapses
        blowIntervalTimer += Time.deltaTime;
        if(blowIntervalTimer >= blowInterval)
        {
            blowIntervalTimer = 0f; // Reset interval timer

            // Roll a random value and compare to blowChance
            float roll = Random.value; // 0..1
            if(roll <= blowChance)
            {
                // Only attempt to trigger if we have a tip reference
                if(strawTipPushReference != null)
                {
                    // Trigger tip's wind for its configured default duration
                    strawTipPushReference.TriggerWind();

                    // Optionally toggle buoyancy when blowing
                    if(toggleBuoyancyOnBlow && buoyancyEffector != null)
                    {
                        float duration = GetTipWindDurationOrFallback();
                        EnableBuoyancyFor(duration);
                    }
                }
            }
        }
    }

    // Start a smooth switch by inverting current target speed
    void BeginSwitch()
    {
        isSwitching = true;
        switchStartTime = Time.time;
        switchStartSpeed = hinge.motor.motorSpeed; // Begin from actual motor speed
        // If motor is nearly zero, use currentTargetSpeed sign; otherwise invert current motor speed
        float baseSpeedSign = (Mathf.Abs(switchStartSpeed) < 1e-3f) ? Mathf.Sign(currentTargetSpeed) : Mathf.Sign(switchStartSpeed);
        switchTargetSpeed = -baseSpeedSign * Mathf.Abs(maxSpeed);
    }

    // Helper to apply motor speed and torque to the hinge
    void ApplyMotor(float speed)
    {
        JointMotor2D motor = hinge.motor;
        motor.motorSpeed = speed;
        motor.maxMotorTorque = maxMotorTorque;
        hinge.motor = motor;
    }

    // Enable the buoyancy effector for a given duration (seconds)
    void EnableBuoyancyFor(float duration)
    {
        // Clamp duration to sensible minimum
        float clampedDuration = Mathf.Max(0.01f, duration);
        // Stop existing coroutine if running
        if(buoyancyCoroutine != null)
        {
            StopCoroutine(buoyancyCoroutine);
            buoyancyCoroutine = null;
        }
        // Enable effector immediately
        buoyancyEffector.enabled = true;
        // Start coroutine to disable after duration
        buoyancyCoroutine = StartCoroutine(DisableBuoyancyAfter(clampedDuration));
    }

    // Coroutine that waits and then disables buoyancy
    IEnumerator DisableBuoyancyAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if(buoyancyEffector != null)
        {
            buoyancyEffector.enabled = false;
        }
        buoyancyCoroutine = null;
    }

    // Try to infer wind duration from the StrawTipPush component using reflection; fallback to configured duration
    float GetTipWindDurationOrFallback()
    {
        if(strawTipPushReference == null)
        {
            return buoyancyActiveDuration;
        }

        // Candidate field/property names commonly used for durations
        string[] candidateNames = { "duration", "windDuration", "blowDuration", "windTime", "pushDuration", "windSeconds" };
        System.Type tipType = strawTipPushReference.GetType();

        foreach(string name in candidateNames)
        {
            // Check for field
            FieldInfo field = tipType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if(field != null && field.FieldType == typeof(float))
            {
                object val = field.GetValue(strawTipPushReference);
                if(val != null) return (float)val;
            }
            // Check for property
            PropertyInfo prop = tipType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if(prop != null && prop.PropertyType == typeof(float) && prop.GetGetMethod(true) != null)
            {
                object val = prop.GetValue(strawTipPushReference, null);
                if(val != null) return (float)val;
            }
        }

        // Fallback to configured duration
        return buoyancyActiveDuration;
    }
}