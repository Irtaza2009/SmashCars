using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float targetSearchInterval = 1f;
    [Tooltip("How far ahead of a moving target the car aims. Set to zero for direct pursuit.")]
    [SerializeField] private float targetPredictionTime = 0.45f;
    [SerializeField] private float maxTargetPredictionDistance = 8f;

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

    [Header("Ram Tactics")]
    [Tooltip("Distance used as a safety fallback when physics contact events are missed.")]
    [SerializeField] private float ramContactDistance = 1.25f;
    [Tooltip("Minimum forward speed for the close-contact fallback, in metres per second.")]
    [SerializeField] private float ramContactSpeed = 1f;
    [SerializeField] private float ramRetreatDuration = 1.1f;
    [SerializeField] private float ramRetreatDistance = 5f;
    [SerializeField, Range(0.1f, 1f)] private float ramRetreatTorqueMultiplier = 0.85f;
    [SerializeField] private float ramRetreatCooldown = 0.5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float obstacleDetectionDistance = 7f;
    [SerializeField] private float obstacleRayHeight = 0.65f;
    [SerializeField] private float obstacleAvoidanceStrength = 1.5f;
    [SerializeField] private float obstacleFrontOffset = 0.8f;
    [Tooltip("Makes the sensors account for the width of the car instead of only detecting a point ahead.")]
    [SerializeField] private float obstacleSensorRadius = 0.45f;
    [SerializeField] private float obstacleSlowdownDistance = 5f;
    [SerializeField] private float obstacleBrakeDistance = 2.25f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Stuck Recovery")]
    [SerializeField] private float stuckSpeed = 1.2f;
    [SerializeField] private float stuckDetectionTime = 1.4f;
    [SerializeField] private float recoveryDuration = 1.4f;
    [SerializeField] private float recoveryStopSpeed = 1.5f;
    [SerializeField] private float recoveryCooldown = 3f;
    [SerializeField] private float minimumTargetDistanceForRecovery = 4f;

    private Vector3 lastStuckCheckPosition;
    private float recoveryCooldownTimer;

    [Header("Stability")]
    [SerializeField] private float downforce = 30f;
    [SerializeField] private float centerOfMassHeight = -0.3f;

    [Header("Debug")]
    [SerializeField] private bool showSensorRays = true;

    private Rigidbody rb;
    private CarHealth carHealth;
    private Rigidbody targetRigidbody;
    private readonly RaycastHit[] sensorHits = new RaycastHit[32];

    private float currentSteerAngle;
    private float searchTimer;
    private float stuckTimer;
    private float recoveryTimer;
    private float recoverySteerDirection = 1f;
    private float ramRetreatTimer;
    private float ramCooldownTimer;
    private float ramRetreatSteerDirection = 1f;
    private Vector3 ramRetreatStartPosition;

    private bool recovering;
    private bool ramRetreating;

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

        ramCooldownTimer -= Time.fixedDeltaTime;

        if (ramRetreating)
        {
            PerformRamRetreat();
            ApplyDownforce();
            return;
        }

        if (target == null)
        {
            ApplyBrakes(brakeStrength);
            SetSteering(0f);
            return;
        }

        DriveTowardsTarget();
        ApplyDownforce();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (target == null || ramRetreating || ramCooldownTimer > 0f)
            return;

        // A hit on any child collider of the target counts as a successful ram.
        if (collision.collider.transform.root != target.root)
            return;

        BeginRamRetreat();
    }

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null && player.transform.root != transform.root)
        {
            target = player.transform;
            targetRigidbody = player.GetComponentInParent<Rigidbody>();
        }
    }

    private Vector3 GetAimPosition()
    {
        if (target == null || targetRigidbody == null)
            return target != null ? target.position : transform.position;

        float distance = Vector3.Distance(transform.position, target.position);
        float predictionTime = Mathf.Min(
            targetPredictionTime,
            distance / Mathf.Max(maxSpeed, 0.01f)
        );

        Vector3 predictedOffset = Vector3.ClampMagnitude(
            targetRigidbody.linearVelocity * predictionTime,
            maxTargetPredictionDistance
        );

        return target.position + predictedOffset;
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

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            obstacleSensorRadius,
            direction,
            sensorHits,
            obstacleDetectionDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = obstacleDetectionDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = sensorHits[i];
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
        Vector3 toTarget = GetAimPosition() - transform.position;
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

        // Collision events are the normal way to detect a ram. This fallback
        // handles wheel/collider setups that can leave the two cars pressed
        // together without producing a fresh collision event on later charges.
        if (distance <= ramContactDistance &&
            (forwardSpeed >= ramContactSpeed || speed <= stuckSpeed) &&
            ramCooldownTimer <= 0f)
        {
            BeginRamRetreat();
            return;
        }

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

        float stoppingDistance = speed * speed / (2f * 10f);
        float targetBrakeDistance = Mathf.Max(
            approachBrakeDistance,
            stoppingDistance + 1.5f
        );

        bool tooFastNearTarget =
            distance < targetBrakeDistance && speedKmh > 20f;

        // Reduce speed before sharp turns instead of permanently
        // braking whenever the target is behind us.
        float turnFactor = Mathf.Lerp(
            1f,
            0.35f,
            Mathf.InverseLerp(25f, 100f, Mathf.Abs(angle))
        );

        bool shouldBrakeForTurn =
            Mathf.Abs(angle) > 100f && speedKmh > 25f;

        bool mustBrakeForObstacle =
            frontDistance < obstacleBrakeDistance;

        if (tooFastNearTarget || shouldBrakeForTurn || mustBrakeForObstacle)
        {
            ApplyBrakes(brakeStrength);
            return;
        }

        ApplyBrakes(0f);

        float throttle = turnFactor;

        // Start slowing before the avoidance steering becomes urgent. This
        // keeps the car controllable through narrow gaps at high speed.
        float obstacleSpeedFactor = Mathf.InverseLerp(
            obstacleBrakeDistance,
            obstacleSlowdownDistance,
            frontDistance
        );
        throttle *= Mathf.Lerp(0.35f, 1f, obstacleSpeedFactor);

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

    private void BeginRamRetreat()
    {
        ramRetreating = true;
        recovering = false;
        stuckTimer = 0f;
        ramRetreatTimer = ramRetreatDuration;
        ramCooldownTimer = ramRetreatDuration + ramRetreatCooldown;
        ramRetreatStartPosition = transform.position;

        // Alternate the escape side when the target is directly ahead, so the
        // AI does not repeat the same line after every collision.
        float targetSide = Vector3.Dot(
            transform.right,
            target.position - transform.position
        );

        if (Mathf.Abs(targetSide) > 0.2f)
            ramRetreatSteerDirection = Mathf.Sign(targetSide);
        else
            ramRetreatSteerDirection = -ramRetreatSteerDirection;
    }

    private void PerformRamRetreat()
    {
        ramRetreatTimer -= Time.fixedDeltaTime;

        // Counter-steering while reversing creates a short arc away from the
        // player, leaving enough room to turn back into the next charge.
        SetSteering(-ramRetreatSteerDirection);

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (forwardSpeed > recoveryStopSpeed)
        {
            ApplyBrakes(brakeStrength);
        }
        else
        {
            ApplyBrakes(0f);
            SetMotorTorque(
                -acceleration * 120f * ramRetreatTorqueMultiplier
            );
        }

        Vector3 retreatOffset = Vector3.ProjectOnPlane(
            transform.position - ramRetreatStartPosition,
            Vector3.up
        );

        if (ramRetreatTimer <= 0f ||
            retreatOffset.magnitude >= ramRetreatDistance)
        {
            ramRetreating = false;
            recoveryCooldownTimer = recoveryCooldown;
            lastStuckCheckPosition = transform.position;
        }
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
