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

    // Detection tuning (small, sensible defaults)
    [Tooltip("Degrees/sec below which rotation is considered stopped.")]
    public float stopAngularSpeedThreshold = 1f;

    [Tooltip("Seconds the angular speed must remain low to count as stopped.")]
    public float stopTimeThreshold = 0.06f;

    // Blow scheduling (Straw decides when to attempt a blow; the tip performs the effect)
    [Tooltip("Chance (0..1) that a blow will occur when the interval elapses (0.5 = 50%).")]
    [Range(0f, 1f)]
    public float blowChance = 0.5f;

    [Tooltip("Seconds between blow chance checks (when each interval passes Straw will roll the chance).")]
    public float blowInterval = 5f;

    // Optional: If you have a StrawTipPush component on a child, you may assign it here. If left null the script will search children.
    [Tooltip("Optional reference to the StrawTipPush component that owns the wind behaviour. If not assigned the script will try to find one in children.")]
    public StrawTipPush strawTipPushReference;

    private HingeJoint2D hinge; // Cached hinge joint
    private float currentTargetSpeed; // Current desired motor speed (positive or negative)
    private float prevZ; // Previous frame z-rotation
    private float stopTimer; // Accumulates time while rotation is near-stopped

    // Switching state
    private bool isSwitching; // Whether we are currently interpolating motor speed
    private float switchStartTime; // When interpolation started
    private float switchStartSpeed; // Motor speed at start of interpolation
    private float switchTargetSpeed; // Motor speed to reach at end of interpolation

    // Blow timing state
    private float blowIntervalTimer = 0f;

    private void Start()
    {
        hinge = GetComponent<HingeJoint2D>(); // cache hinge
        hinge.useMotor = true; // ensure motor active
        currentTargetSpeed = maxSpeed; // start with positive direction
        prevZ = transform.eulerAngles.z; // initialise previous rotation

        // If strawTipPushReference not assigned, try to find one in children
        if(strawTipPushReference == null)
        {
            strawTipPushReference = GetComponentInChildren<StrawTipPush>();
        }

        // Clamp blowChance to valid range
        blowChance = Mathf.Clamp01(blowChance);
        blowInterval = Mathf.Max(0.01f, blowInterval);
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
            blowIntervalTimer = 0f; // reset interval timer

            // Roll a random value and compare to blowChance
            float roll = Random.value; // 0..1
            if(roll <= blowChance)
            {
                // Only attempt to trigger if we have a tip reference
                if(strawTipPushReference != null)
                {
                    // Trigger tip's wind for its configured default duration
                    strawTipPushReference.TriggerWind();
                }
            }
        }
    }

    // Start a smooth switch by inverting current target speed
    void BeginSwitch()
    {
        isSwitching = true;
        switchStartTime = Time.time;
        switchStartSpeed = hinge.motor.motorSpeed; // begin from actual motor speed
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
}