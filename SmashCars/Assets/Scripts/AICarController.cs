using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float targetSearchInterval = 1f;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider rearLeft;
    [SerializeField] private WheelCollider rearRight;

    [Header("Wheel Visuals")]
    [SerializeField] private Transform frontLeftMesh;
    [SerializeField] private Transform frontRightMesh;
    [SerializeField] private Transform rearLeftMesh;
    [SerializeField] private Transform rearRightMesh;
    [SerializeField] private Vector3 wheelMeshRotationOffset;

    [Header("Driving")]
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float maxSpeed = 25f;
    [SerializeField] private float maxReverseSpeed = 6f;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float steeringSpeed = 100f;
    [SerializeField] private float brakeStrength = 2500f;
    [SerializeField] private float coastBrake = 100f;

    [Header("Target Approach")]
    [SerializeField] private float attackDistance = 12f;
    [SerializeField] private float approachBrakeDistance = 8f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleDetectionDistance = 7f;
    [SerializeField] private float obstacleRayHeight = 0.65f;
    [SerializeField] private float obstacleAvoidanceStrength = 1.5f;
    [SerializeField] private float obstacleFrontOffset = 0.8f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Stuck Recovery")]
    [SerializeField] private float stuckSpeed = 1.2f;
    [SerializeField] private float stuckDetectionTime = 1.4f;
    [SerializeField] private float recoveryDuration = 1.4f;
    [SerializeField] private float recoveryStopSpeed = 1.5f;
    [SerializeField] private float blockedDistance = 1.5f;
    [SerializeField] private float minimumStuckDistance = 2f;
    [SerializeField] private float recoveryCooldown = 3f;
    [SerializeField] private float minimumTargetDistanceForRecovery = 4f;

    private Vector3 lastStuckCheckPosition;
    private float recoveryCooldownTimer;

    [Header("Stability")]
    [SerializeField] private float downforce = 30f;
    [SerializeField] private float centerOfMassHeight = -0.3f;

    [Header("Debug")]
    [SerializeField] private bool showSensorRays = true;

    private static readonly float[] SensorAngles =
        { -65f, -35f, 0f, 35f, 65f };

    private Rigidbody rb;
    private CarHealth carHealth;

    private float currentSteerAngle;
    private float searchTimer;
    private float stuckTimer;
    private float recoveryTimer;
    private float recoverySteerDirection = 1f;

    private bool recovering;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carHealth = GetComponent<CarHealth>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);
    }

    private void Start()
    {
        FindTarget();
        lastStuckCheckPosition = transform.position;
    }

    private void Update()
    {
        if (target == null)
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0f)
            {
                searchTimer = targetSearchInterval;
                FindTarget();
            }
        }

        UpdateWheelVisuals();
    }

    private void FixedUpdate()
    {
        if (carHealth != null && carHealth.IsEliminated)
        {
            StopCar();
            return;
        }

        if (!WheelsAssigned())
            return;

        if (target == null)
        {
            ApplyBrakes(brakeStrength);
            SetSteering(0f);
            return;
        }

        DriveTowardsTarget();
        ApplyDownforce();
    }

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null && player.transform.root != transform.root)
            target = player.transform;
    }

    // Returns the distance to the nearest relevant obstacle.
    // The car itself and the intended target are ignored.
    private float GetClearance(float angle)
    {
        Vector3 origin = transform.position
            + Vector3.up * obstacleRayHeight
            + transform.forward * obstacleFrontOffset;

        Vector3 forward = Vector3.ProjectOnPlane(
            transform.forward, Vector3.up
        ).normalized;

        Vector3 direction =
            Quaternion.AngleAxis(angle, Vector3.up) * forward;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            obstacleDetectionDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = obstacleDetectionDistance;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.root == transform.root)
                continue;

            // Do not steer away from the player we are attacking.
            if (target != null &&
                hit.transform.root == target.root)
                continue;

            if (hit.distance < nearestDistance)
                nearestDistance = hit.distance;
        }

        if (showSensorRays)
        {
            Debug.DrawRay(
                origin,
                direction * nearestDistance,
                nearestDistance < obstacleDetectionDistance
                    ? Color.red
                    : Color.green
            );
        }

        return nearestDistance;
    }

    // Positive steering turns right; negative steering turns left.
    private float GetObstacleAvoidance(
        out float frontDistance,
        out float preferredTurn)
    {
        float leftNear = GetClearance(-35f);
        float leftFar = GetClearance(-65f);
        frontDistance = GetClearance(0f);
        float rightNear = GetClearance(35f);
        float rightFar = GetClearance(65f);

        float leftSpace = (leftNear + leftFar) * 0.5f;
        float rightSpace = (rightNear + rightFar) * 0.5f;

        preferredTurn = rightSpace >= leftSpace ? 1f : -1f;

        float frontDanger = 1f -
            Mathf.Clamp01(frontDistance / obstacleDetectionDistance);

        if (frontDanger > 0f)
        {
            // If something is ahead, choose the more open side.
            return preferredTurn * frontDanger
                * obstacleAvoidanceStrength;
        }

        // If the front is clear, gently steer away from
        // obstacles detected to either side.
        float leftDanger = 1f -
            Mathf.Clamp01(leftNear / obstacleDetectionDistance);

        float rightDanger = 1f -
            Mathf.Clamp01(rightNear / obstacleDetectionDistance);

        float sideAvoidance =
            (leftDanger - rightDanger)
            * obstacleAvoidanceStrength * 0.6f;

        if (Mathf.Abs(sideAvoidance) > 0.05f)
            preferredTurn = Mathf.Sign(sideAvoidance);

        return sideAvoidance;
    }

    private void DriveTowardsTarget()
    {
        Vector3 toTarget = target.position - transform.position;
        Vector3 flatDirection = Vector3.ProjectOnPlane(
            toTarget, Vector3.up
        );

        if (flatDirection.sqrMagnitude < 0.01f)
        {
            ApplyBrakes(coastBrake);
            SetSteering(0f);
            return;
        }

        float distance = flatDirection.magnitude;

        float angle = Vector3.SignedAngle(
            transform.forward,
            flatDirection.normalized,
            Vector3.up
        );

        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        float speed = rb.linearVelocity.magnitude;

        float targetSteer = Mathf.Clamp(angle / 45f, -1f, 1f);

        float avoidance = GetObstacleAvoidance(
            out float frontDistance,
            out float preferredTurn
        );

        // Avoidance takes priority when an obstacle is close.
        float avoidanceWeight = Mathf.InverseLerp(
            obstacleDetectionDistance,
            1f,
            frontDistance
        );

        float steerInput = Mathf.Clamp(
            targetSteer * (1f - avoidanceWeight)
                + avoidance,
            -1f,
            1f
        );

        // Detect being stuck by measuring actual movement,
        // not by depending on obstacle raycasts.
        recoveryCooldownTimer -= Time.fixedDeltaTime;

        float movedDistance = Vector3.Distance(
            transform.position,
            lastStuckCheckPosition
        );

        lastStuckCheckPosition = transform.position;

        bool farFromTarget =
            distance > minimumTargetDistanceForRecovery;

        bool barelyMoving =
            speed < stuckSpeed &&
            movedDistance < 0.15f;

        // If we're close to the target, don't trigger recovery
        // simply because we're slowing down for an attack.
        if (!recovering && recoveryCooldownTimer <= 0f)
        {
            if (farFromTarget && barelyMoving)
            {
                stuckTimer += Time.fixedDeltaTime;

                if (stuckTimer >= stuckDetectionTime)
                {
                    BeginRecovery(preferredTurn);
                    recoveryCooldownTimer = recoveryCooldown;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        SetSteering(steerInput);

        if (recovering)
        {
            PerformRecovery();
            return;
        }

        float speedKmh = speed * 3.6f;

        bool tooFastNearTarget =
            distance < approachBrakeDistance && speedKmh > 20f;

        // Reduce speed before sharp turns instead of permanently
        // braking whenever the target is behind us.
        float turnFactor = Mathf.Lerp(
            1f,
            0.35f,
            Mathf.InverseLerp(25f, 100f, Mathf.Abs(angle))
        );

        bool shouldBrakeForTurn =
            Mathf.Abs(angle) > 100f && speedKmh > 25f;

        if (tooFastNearTarget || shouldBrakeForTurn)
        {
            ApplyBrakes(brakeStrength);
            return;
        }

        ApplyBrakes(0f);

        float throttle = turnFactor;

        if (forwardSpeed >= maxSpeed)
            throttle = 0f;

        // Ease off slightly during the final approach.
        if (distance < attackDistance && speedKmh > 35f)
            throttle *= 0.75f;

        // If the route is blocked but one side is available,
        // steering handles the detour. If all paths are blocked,
        // stuck detection will trigger reverse recovery.
        SetMotorTorque(acceleration * 120f * throttle);
    }

    private void BeginRecovery(float turnDirection)
    {
        recovering = true;
        recoveryTimer = recoveryDuration;
        stuckTimer = 0f;

        recoverySteerDirection =
            Mathf.Abs(turnDirection) > 0.1f
                ? Mathf.Sign(turnDirection)
                : 1f;

        Debug.Log($"{name}: stuck. Reversing to recover.");
    }

    private void PerformRecovery()
    {
        recoveryTimer -= Time.fixedDeltaTime;

        // Reverse steering is inverted: counter-steer so the
        // front of the car rotates toward the escape direction.
        SetSteering(-recoverySteerDirection);

        if (Vector3.Dot(rb.linearVelocity, transform.forward)
            > recoveryStopSpeed)
        {
            ApplyBrakes(brakeStrength);
        }
        else
        {
            ApplyBrakes(0f);
            SetMotorTorque(-acceleration * 120f);
        }

        if (recoveryTimer <= 0f)
        {
            recovering = false;
            stuckTimer = 0f;
            recoveryCooldownTimer = recoveryCooldown;
            lastStuckCheckPosition = transform.position;
        }
    }

    private void SetSteering(float input)
    {
        float targetAngle = input * maxSteerAngle;

        currentSteerAngle = Mathf.MoveTowards(
            currentSteerAngle,
            targetAngle,
            steeringSpeed * Time.fixedDeltaTime
        );

        if (frontLeft != null)
            frontLeft.steerAngle = currentSteerAngle;

        if (frontRight != null)
            frontRight.steerAngle = currentSteerAngle;
    }

    private void SetMotorTorque(float totalTorque)
    {
        if (!WheelsAssigned())
            return;

        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        if (totalTorque > 0f && forwardSpeed >= maxSpeed)
            totalTorque = 0f;

        if (totalTorque < 0f && forwardSpeed <= -maxReverseSpeed)
            totalTorque = 0f;

        frontLeft.motorTorque = totalTorque * 0.25f;
        frontRight.motorTorque = totalTorque * 0.25f;
        rearLeft.motorTorque = totalTorque * 0.25f;
        rearRight.motorTorque = totalTorque * 0.25f;
    }

    private void ApplyBrakes(float brakeTorque)
    {
        if (frontLeft != null)
        {
            frontLeft.brakeTorque = brakeTorque;
            frontLeft.motorTorque = 0f;
        }

        if (frontRight != null)
        {
            frontRight.brakeTorque = brakeTorque;
            frontRight.motorTorque = 0f;
        }

        if (rearLeft != null)
        {
            rearLeft.brakeTorque = brakeTorque;
            rearLeft.motorTorque = 0f;
        }

        if (rearRight != null)
        {
            rearRight.brakeTorque = brakeTorque;
            rearRight.motorTorque = 0f;
        }
    }

    private void StopCar()
    {
        ApplyBrakes(brakeStrength);
        SetSteering(0f);
    }

    private bool WheelsAssigned()
    {
        return frontLeft != null &&
               frontRight != null &&
               rearLeft != null &&
               rearRight != null;
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
}
