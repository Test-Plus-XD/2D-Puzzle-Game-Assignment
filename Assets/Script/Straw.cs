using UnityEngine;

/// Controls HingeJoint2D motor and flips its direction when z-rotation stops changing (optional loop).
[RequireComponent(typeof(HingeJoint2D))]
public class Straw : MonoBehaviour
{
    public bool loop = true; // Toggle automatic looping when rotation stops changing
    public float maxSpeed = 60f; // Motor target speed magnitude in degrees/sec when not switching
    public float changeDuration = 1f; // Time in seconds to smoothly interpolate motor speed when switching
    public float maxMotorTorque = 100f; // Torque applied by the motor

    // Detection tuning (small, sensible defaults)
    public float stopAngularSpeedThreshold = 1f; // degrees/sec below which rotation is considered 'stopped'
    public float stopTimeThreshold = 0.06f; // seconds the angular speed must remain low to count as stopped

    private HingeJoint2D hinge; // cached hinge joint
    private float currentTargetSpeed; // current desired motor speed (positive or negative)
    private float prevZ; // previous frame z-rotation
    private float stopTimer; // accumulates time while rotation is near-stopped

    // Switching state
    private bool isSwitching; // whether we are currently interpolating motor speed
    private float switchStartTime; // when interpolation started
    private float switchStartSpeed; // motor speed at start of interpolation
    private float switchTargetSpeed; // motor speed to reach at end of interpolation

    void Start()
    {
        hinge = GetComponent<HingeJoint2D>(); // cache hinge
        hinge.useMotor = true; // ensure motor active
        currentTargetSpeed = maxSpeed; // start with positive direction
        prevZ = transform.eulerAngles.z; // initialise previous rotation
    }

    void Update()
    {
        // Read current z rotation and compute delta (shortest angle)
        float curZ = transform.eulerAngles.z;
        float deltaAngle = Mathf.DeltaAngle(prevZ, curZ);
        float angularSpeed = Mathf.Abs(deltaAngle) / Mathf.Max(Time.deltaTime, 1e-6f); // degrees/sec

        // Detect near-stop: accumulate time while angular speed below threshold
        if(angularSpeed < stopAngularSpeedThreshold)
        {
            stopTimer += Time.deltaTime;
        } else
        {
            stopTimer = 0f;
        }

        // If not currently switching and loop enabled and we've detected a stop, begin switching
        if(!isSwitching && loop && stopTimer >= stopTimeThreshold)
        {
            BeginSwitch(); // start smooth inversion of motor speed
            stopTimer = 0f; // reset detection timer so we don't immediately retrigger
        }

        // If switching, interpolate motor speed smoothly over changeDuration
        if(isSwitching)
        {
            float t = Mathf.Clamp01((Time.time - switchStartTime) / Mathf.Max(changeDuration, 1e-6f));
            float newSpeed = Mathf.Lerp(switchStartSpeed, switchTargetSpeed, t);

            ApplyMotor(newSpeed);

            if(t >= 1f) // finished interpolation
            {
                isSwitching = false;
                currentTargetSpeed = switchTargetSpeed; // commit new target
            }
        } else
        {
            // If not switching, ensure motor holds the current target speed
            ApplyMotor(currentTargetSpeed);
        }

        prevZ = curZ; // store for next frame
    }

    // Start a smooth switch by inverting current target speed
    void BeginSwitch()
    {
        isSwitching = true;
        switchStartTime = Time.time;
        switchStartSpeed = hinge.motor.motorSpeed; // begin from actual motor speed
        switchTargetSpeed = -Mathf.Sign(Mathf.Abs(switchStartSpeed) < 1e-3f ? currentTargetSpeed : switchStartSpeed) * Mathf.Abs(maxSpeed);
        // If motor was nearly zero, use currentTargetSpeed sign; otherwise invert current motor speed
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