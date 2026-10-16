
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider rearLeft;
    [SerializeField] private WheelCollider rearRight;

    [Header("Visible Wheel Meshes")]
    [SerializeField] private Transform frontLeftMesh;
    [SerializeField] private Transform frontRightMesh;
    [SerializeField] private Transform rearLeftMesh;
    [SerializeField] private Transform rearRightMesh;

    [SerializeField] private Vector3 wheelMeshRotationOffset =
        new Vector3(0f, 0f, 90f);

    [Header("Body Visuals")]
    [Tooltip("Assign the visible chassis mesh, not the PlayerCar root.")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private float pitchAnglePerAcceleration = 0.35f;
    [SerializeField] private float rollAnglePerAcceleration = 0.45f;
    [SerializeField] private float maxVisualPitch = 5f;
    [SerializeField] private float maxVisualRoll = 7f;
    [SerializeField] private float bodyVisualSmoothness = 5f;

    [Header("Driving")]
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float reverseAcceleration = 18f;
    [SerializeField] private float maxSpeed = 25f;
    [SerializeField] private float maxReverseSpeed = 10f;
    [SerializeField] private float maxSteerAngle = 32f;
    [SerializeField] private float steeringSpeed = 5f;

    [Header("Brakes")]
    [SerializeField] private float brakeStrength = 4500f;
    [SerializeField] private float handbrakeStrength = 6500f;
    [SerializeField] private float coastBrake = 30f;

    [Header("Suspension")]
    [SerializeField] private float suspensionSpring = 35000f;
    [SerializeField] private float suspensionDamper = 4500f;
    [SerializeField, Range(0f, 1f)] private float suspensionTargetPosition = 0.5f;
    [SerializeField] private float suspensionDistance = 0.2f;
    [SerializeField] private float wheelMass = 20f;

    [Header("Anti-Roll")]
    [SerializeField] private float frontAntiRoll = 7000f;
    [SerializeField] private float rearAntiRoll = 6000f;

    [Header("Grip and Drifting")]
    [SerializeField] private float frontSidewaysGrip = 1.8f;
    [SerializeField] private float rearSidewaysGrip = 1.8f;
    [SerializeField] private float rearHandbrakeGrip = 0.55f;
    [SerializeField] private float frontForwardGrip = 1.4f;
    [SerializeField] private float rearForwardGrip = 1.4f;

    [Header("Low-Speed Stop Assist")]
    [SerializeField] private float stopAssistSpeed = 0.25f;
    [SerializeField] private float stopAssistBrakeTorque = 1500f;

    [Header("Stability")]
    [SerializeField] private float downforce = 30f;
    [SerializeField] private float airAngularDamping = 0.5f;

    [Header("Reset")]
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    private Rigidbody rb;
    private float throttleInput;
    private float steerInput;
    private bool handbrake;
    private bool footbrake;

    private float currentSteerAngle;
    private Vector3 previousVelocity;
    private Vector3 localAcceleration;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Quaternion bodyStartLocalRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = new Vector3(0f, -0.3f, 0f);

        startPosition = transform.position;
        startRotation = transform.rotation;
        previousVelocity = rb.linearVelocity;

        if (bodyVisual != null)
            bodyStartLocalRotation = bodyVisual.localRotation;

        ConfigureWheel(frontLeft, frontSidewaysGrip, frontForwardGrip);
        ConfigureWheel(frontRight, frontSidewaysGrip, frontForwardGrip);
        ConfigureWheel(rearLeft, rearSidewaysGrip, rearForwardGrip);
        ConfigureWheel(rearRight, rearSidewaysGrip, rearForwardGrip);
    }

    private void Update()
    {
        throttleInput = Input.GetAxisRaw("Vertical");
        steerInput = Input.GetAxisRaw("Horizontal");

        footbrake = Input.GetKey(KeyCode.Space);
        handbrake = Input.GetKey(KeyCode.LeftShift);

        // Brakes always take priority over throttle.
        if (footbrake)
            throttleInput = 0f;

        if (Input.GetKeyDown(resetKey))
            ResetCar();

        UpdateWheelVisuals();
    }

    private void FixedUpdate()
    {
        if (!WheelsAssigned())
            return;

        UpdateAccelerationEstimate();
        HandleSteering();
        HandleMotor();
        HandleBrakes();
        HandleGrip();
        ApplyAntiRoll(frontLeft, frontRight, frontAntiRoll);
        ApplyAntiRoll(rearLeft, rearRight, rearAntiRoll);
        ApplyDownforce();
        UpdateBodyVisuals();
    }

    private bool WheelsAssigned()
    {
        return frontLeft != null && frontRight != null &&
               rearLeft != null && rearRight != null;
    }

    private void ConfigureWheel(
        WheelCollider wheel,
        float sidewaysGrip,
        float forwardGrip)
    {
        if (wheel == null)
            return;

        wheel.mass = wheelMass;
        wheel.suspensionDistance = suspensionDistance;
        wheel.wheelDampingRate = 1f;

        JointSpring spring = wheel.suspensionSpring;
        spring.spring = suspensionSpring;
        spring.damper = suspensionDamper;
        spring.targetPosition = suspensionTargetPosition;
        wheel.suspensionSpring = spring;

        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = forwardGrip;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = sidewaysGrip;
        wheel.sidewaysFriction = sideways;
    }

    private void UpdateAccelerationEstimate()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 accelerationWorld =
            (velocity - previousVelocity) / Time.fixedDeltaTime;

        localAcceleration = transform.InverseTransformDirection(
            accelerationWorld
        );

        previousVelocity = velocity;
    }

    private void HandleSteering()
    {
        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        float speedFactor = Mathf.Lerp(
            1f, 0.55f,
            Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxSpeed)
        );

        float targetAngle = steerInput * maxSteerAngle * speedFactor;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetAngle,
            steeringSpeed * Time.fixedDeltaTime
        );

        frontLeft.steerAngle = currentSteerAngle;
        frontRight.steerAngle = currentSteerAngle;
    }

    
    
    private void HandleMotor()
    {
        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        // Never drive against an active brake.
        if (footbrake || handbrake)
        {
            SetMotorTorque(0f);
            return;
        }

        float motorTorque = 0f;

        if (throttleInput > 0f)
        {
            // Wait for the car to stop reversing.
            if (forwardSpeed >= -0.3f && forwardSpeed < maxSpeed)
                motorTorque = acceleration * 120f;
        }
        else if (throttleInput < 0f)
        {
            // Wait for the car to stop moving forwards.
            if (forwardSpeed <= 0.3f && forwardSpeed > -maxReverseSpeed)
                motorTorque = -reverseAcceleration * 120f;
        }

        SetMotorTorque(motorTorque);
    }


    private void SetMotorTorque(float totalTorque)
    {
        frontLeft.motorTorque = totalTorque * 0.25f;
        frontRight.motorTorque = totalTorque * 0.25f;
        rearLeft.motorTorque = totalTorque * 0.25f;
        rearRight.motorTorque = totalTorque * 0.25f;
    }

    
    private void HandleBrakes()
    {
        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        bool changingDirection =
            (throttleInput < 0f && forwardSpeed > 0.3f) ||
            (throttleInput > 0f && forwardSpeed < -0.3f);

        float frontBrake = 0f;
        float rearBrake = 0f;

        if (footbrake || changingDirection)
        {
            frontBrake = brakeStrength;
            rearBrake = brakeStrength;
        }
        else if (Mathf.Abs(throttleInput) < 0.01f && !handbrake)
        {
            frontBrake = coastBrake;
            rearBrake = coastBrake;
        }

        if (handbrake)
            rearBrake = handbrakeStrength;

        frontLeft.brakeTorque = frontBrake;
        frontRight.brakeTorque = frontBrake;
        rearLeft.brakeTorque = rearBrake;
        rearRight.brakeTorque = rearBrake;

        // Stop residual movement near zero speed.
        bool wantsToStop =
            footbrake ||
            handbrake ||
            changingDirection ||
            Mathf.Abs(throttleInput) < 0.01f;

        if (wantsToStop &&
            rb.linearVelocity.magnitude < stopAssistSpeed)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }



    private void HandleGrip()
    {
        SetSidewaysGrip(frontLeft, frontSidewaysGrip);
        SetSidewaysGrip(frontRight, frontSidewaysGrip);

        float rearGrip = handbrake
            ? rearHandbrakeGrip
            : rearSidewaysGrip;

        SetSidewaysGrip(rearLeft, rearGrip);
        SetSidewaysGrip(rearRight, rearGrip);
    }

    private void SetSidewaysGrip(WheelCollider wheel, float grip)
    {
        WheelFrictionCurve friction = wheel.sidewaysFriction;
        friction.stiffness = grip;
        wheel.sidewaysFriction = friction;
    }

    private void ApplyAntiRoll(
        WheelCollider left,
        WheelCollider right,
        float antiRollForce)
    {
        float leftTravel = GetSuspensionTravel(left);
        float rightTravel = GetSuspensionTravel(right);

        float force = (leftTravel - rightTravel) * antiRollForce;

        if (left.GetGroundHit(out WheelHit leftHit))
        {
            rb.AddForceAtPosition(
                left.transform.up * -force,
                leftHit.point
            );
        }

        if (right.GetGroundHit(out WheelHit rightHit))
        {
            rb.AddForceAtPosition(
                right.transform.up * force,
                rightHit.point
            );
        }
    }

    private float GetSuspensionTravel(WheelCollider wheel)
    {
        if (wheel.GetGroundHit(out WheelHit hit))
        {
            Vector3 localHitPoint =
                wheel.transform.InverseTransformPoint(hit.point);

            return (-localHitPoint.y - wheel.radius) /
                   wheel.suspensionDistance;
        }

        return 1f;
    }

    private void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;

        if (speed > 1f)
        {
            rb.AddForce(
                -transform.up * downforce * speed,
                ForceMode.Force
            );
        }
    }

    private void UpdateBodyVisuals()
    {
        if (bodyVisual == null)
            return;

        // Longitudinal acceleration creates pitch;
        // lateral acceleration creates body roll.
        float pitch = Mathf.Clamp(
            -localAcceleration.z * pitchAnglePerAcceleration,
            -maxVisualPitch,
            maxVisualPitch
        );

        float roll = Mathf.Clamp(
            -localAcceleration.x * rollAnglePerAcceleration,
            -maxVisualRoll,
            maxVisualRoll
        );

        Quaternion targetRotation = bodyStartLocalRotation *
            Quaternion.Euler(pitch, 0f, roll);

        bodyVisual.localRotation = Quaternion.Slerp(
            bodyVisual.localRotation,
            targetRotation,
            bodyVisualSmoothness * Time.fixedDeltaTime
        );
    }

    private void UpdateWheelVisuals()
    {
        UpdateWheelVisual(frontLeft, frontLeftMesh);
        UpdateWheelVisual(frontRight, frontRightMesh);
        UpdateWheelVisual(rearLeft, rearLeftMesh);
        UpdateWheelVisual(rearRight, rearRightMesh);
    }

    private void UpdateWheelVisual(
        WheelCollider wheel,
        Transform wheelMesh)
    {
        if (wheel == null || wheelMesh == null)
            return;

        wheel.GetWorldPose(
            out Vector3 position,
            out Quaternion rotation
        );

        wheelMesh.SetPositionAndRotation(
            position,
            rotation * Quaternion.Euler(wheelMeshRotationOffset)
        );
    }

    public void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(startPosition, startRotation);

        if (bodyVisual != null)
            bodyVisual.localRotation = bodyStartLocalRotation;

        currentSteerAngle = 0f;
        localAcceleration = Vector3.zero;
        previousVelocity = Vector3.zero;

        if (WheelsAssigned())
        {
            frontLeft.steerAngle = 0f;
            frontRight.steerAngle = 0f;

            frontLeft.motorTorque = 0f;
            frontRight.motorTorque = 0f;
            rearLeft.motorTorque = 0f;
            rearRight.motorTorque = 0f;
        }

        rb.WakeUp();
    }
}
