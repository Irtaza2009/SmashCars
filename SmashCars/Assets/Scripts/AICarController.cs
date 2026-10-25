using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    private sealed class WheelVisual
    {
        public Transform transform;
        public Quaternion rotationOffset;
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float targetSearchInterval = 1f;
    [Tooltip("How far ahead of a moving target the car aims. Set to zero for direct pursuit.")]
    [SerializeField] private float targetPredictionTime = 0.45f;
    [SerializeField] private float maxTargetPredictionDistance = 8f;

    [Header("NavMesh Pathfinding")]
    [Tooltip("Follow a NavMesh path to the target (ramps, tracks, overpasses). When off, or when no path exists, the car drives straight at the target.")]
    [SerializeField] private bool useNavMesh = true;
    [Tooltip("The NavMeshSurface baked for the car. Its Agent Type is used for all path queries. Leave empty to use the default agent type.")]
    [SerializeField] private NavMeshSurface navSurface;
    [SerializeField] private int navAreaMask = NavMesh.AllAreas;
    [SerializeField] private float repathInterval = 0.35f;
    [Tooltip("How far a point on the path the car steers towards at standstill.")]
    [SerializeField] private float pathLookAhead = 5f;
    [Tooltip("Extra look-ahead per m/s of speed, so fast cars cut corners smoothly instead of zig-zagging.")]
    [SerializeField] private float pathLookAheadSpeedFactor = 0.25f;
    [Tooltip("How far from the NavMesh a position may be and still snap onto it.")]
    [SerializeField] private float navSampleDistance = 3f;
    [Tooltip("Within this distance, if the NavMesh shows a clear straight line, the car drives straight at the target for accurate ramming.")]
    [SerializeField] private float directPursuitDistance = 12f;
    [Tooltip("Direct pursuit is only used when the target is at about the same height (so the car doesn't aim at someone on an overpass).")]
    [SerializeField] private float maxDirectHeightDifference = 1.5f;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider rearLeft;
    [SerializeField] private WheelCollider rearRight;

    [Header("Wheel Visuals")]
    [Tooltip("Root of the visible car model. If set, its wheel meshes are replaced with generated cylinders (same as the player). Leave empty to use the legacy meshes below.")]
    [SerializeField] private Transform carModel;
    [SerializeField] private Material tyreMaterial;
    [Header("Legacy Wheel Meshes (used when no Car Model is set)")]
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

    [Header("Obstacle Avoidance (local safety net)")]
    [SerializeField] private float obstacleDetectionDistance = 7f;
    [SerializeField] private float obstacleRayHeight = 0.65f;
    [SerializeField] private float obstacleAvoidanceStrength = 1.5f;
    [SerializeField] private float obstacleFrontOffset = 0.8f;
    [Tooltip("Makes the sensors account for the width of the car instead of only detecting a point ahead.")]
    [SerializeField] private float obstacleSensorRadius = 0.45f;
    [SerializeField] private float obstacleSlowdownDistance = 5f;
    [SerializeField] private float obstacleBrakeDistance = 2.25f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Surface Awareness")]
    [Tooltip("Surfaces flatter than this (degrees from horizontal) are driveable, not obstacles.")]
    [SerializeField, Range(10f, 60f)] private float maxDrivableSlope = 40f;
    [Tooltip("Optional: put ramps/tracks on these layers and they are never treated as obstacles.")]
    [SerializeField] private LayerMask drivableLayers;
    [Tooltip("Tilt the sensors to follow the ground so ramps are not hit head-on.")]
    [SerializeField] private bool alignSensorsToGround = true;
    [SerializeField] private float groundAlignSmoothing = 8f;

    [Header("Stuck Recovery")]
    [SerializeField] private float stuckSpeed = 1.2f;
    [SerializeField] private float stuckDetectionTime = 1.4f;
    [SerializeField] private float recoveryDuration = 1.4f;
    [SerializeField] private float recoveryStopSpeed = 1.5f;
    [SerializeField] private float recoveryCooldown = 3f;
    [SerializeField] private float minimumTargetDistanceForRecovery = 4f;

    [Header("Stability")]
    [SerializeField] private float downforce = 30f;
    [SerializeField] private float centerOfMassHeight = -0.3f;

    [Header("Debug")]
    [SerializeField] private bool showSensorRays = true;
    [Tooltip("Yellow = NavMesh path, magenta = point the car is steering towards.")]
    [SerializeField] private bool showPath = true;

    private Rigidbody rb;
    private CarHealth carHealth;
    private Rigidbody targetRigidbody;
    private readonly RaycastHit[] sensorHits = new RaycastHit[32];

    private Vector3 lastStuckCheckPosition;
    private float recoveryCooldownTimer;
    private Vector3 groundNormal = Vector3.up;

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

    // NavMesh path state
    private NavMeshPath navPath;
    private readonly Vector3[] pathCorners = new Vector3[64];
    private int cornerCount;
    private int pathSegmentIndex;
    private bool hasPath;
    private float repathTimer;
    private bool forceRepath = true;
    private bool hasValidTargetNavPos;
    private Vector3 lastValidTargetNavPos;

    private WheelVisual frontLeftVisual;
    private WheelVisual frontRightVisual;
    private WheelVisual rearLeftVisual;
    private WheelVisual rearRightVisual;
    private readonly List<GameObject> generatedWheelMeshes =
        new List<GameObject>();
    private readonly List<Renderer> hiddenModelWheelRenderers =
        new List<Renderer>();

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carHealth = GetComponent<CarHealth>();
        navPath = new NavMeshPath();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);
    }

    private void Start()
    {
        FindTarget();
        lastStuckCheckPosition = transform.position;

        if (carModel != null)
            BindModelWheels(carModel.gameObject);
        else
            BindLegacyWheels();
    }

    private void OnDestroy()
    {
        ClearGeneratedWheelMeshes();
    }

    private void OnDisable()
    {
        // Generated cylinders are created after CarHealth caches renderers, so
        // explicitly remove them when CarHealth disables this controller.
        DestroyGeneratedWheelMeshes();
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

        UpdateGroundNormal();

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

    // ------------------------------------------------------------------
    // Targeting
    // ------------------------------------------------------------------

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null && player.transform.root != transform.root)
        {
            target = player.transform;
            targetRigidbody = player.GetComponentInParent<Rigidbody>();
            forceRepath = true;
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

    // ------------------------------------------------------------------
    // NavMesh pathfinding
    // ------------------------------------------------------------------

    private NavMeshQueryFilter CreateNavFilter()
    {
        return new NavMeshQueryFilter
        {
            agentTypeID = navSurface != null ? navSurface.agentTypeID : 0,
            areaMask = navAreaMask
        };
    }

    // Recalculates the path every repathInterval seconds (or immediately when
    // forced). The path is queried from the car's current position, so the
    // car always follows a fresh route and never needs "waypoint reached" logic.
    private void UpdatePath(Vector3 aimPosition)
    {
        repathTimer -= Time.fixedDeltaTime;

        if (repathTimer > 0f && !forceRepath)
            return;

        repathTimer = repathInterval;
        forceRepath = false;

        NavMeshQueryFilter filter = CreateNavFilter();

        if (!NavMesh.SamplePosition(
                transform.position, out NavMeshHit startHit,
                navSampleDistance, filter))
        {
            hasPath = false;
            return;
        }

        Vector3 goal;

        if (NavMesh.SamplePosition(
                aimPosition, out NavMeshHit goalHit,
                navSampleDistance, filter))
        {
            goal = goalHit.position;
            lastValidTargetNavPos = goal;
            hasValidTargetNavPos = true;
        }
        else if (hasValidTargetNavPos)
        {
            // Target is airborne or off the mesh: head for where it last was.
            goal = lastValidTargetNavPos;
        }
        else
        {
            hasPath = false;
            return;
        }

        if (NavMesh.CalculatePath(startHit.position, goal, filter, navPath) &&
            navPath.status != NavMeshPathStatus.PathInvalid)
        {
            cornerCount = navPath.GetCornersNonAlloc(pathCorners);
            pathSegmentIndex = 0;
            hasPath = cornerCount >= 2;
        }
        else
        {
            hasPath = false;
        }
    }

    // True when the NavMesh has no edge (wall, cliff, hole) between the car
    // and the target, meaning a straight charge is safe.
    private bool HasClearNavLine(Vector3 aimPosition)
    {
        NavMeshQueryFilter filter = CreateNavFilter();

        if (!NavMesh.SamplePosition(
                transform.position, out NavMeshHit startHit,
                navSampleDistance, filter))
            return false;

        if (!NavMesh.SamplePosition(
                aimPosition, out NavMeshHit goalHit,
                navSampleDistance, filter))
            return false;

        return !NavMesh.Raycast(
            startHit.position, goalHit.position, out _, filter
        );
    }

    // Pure pursuit: find the nearest point on the path, then aim at a point
    // a look-ahead distance further along it.
    private bool TryGetPathSteerPoint(
        float lookAhead,
        out Vector3 steerPoint,
        out float remainingDistance,
        out float upcomingTurnAngle)
    {
        steerPoint = transform.position;
        remainingDistance = 0f;
        upcomingTurnAngle = 0f;

        if (!hasPath || cornerCount < 2)
            return false;

        Vector3 position = transform.position;
        int lastSegment = cornerCount - 2;
        int firstSegment = Mathf.Min(pathSegmentIndex, lastSegment);
        int searchEnd = Mathf.Min(lastSegment, firstSegment + 3);

        int bestSegment = firstSegment;
        float bestSqr = float.PositiveInfinity;
        Vector3 bestPoint = pathCorners[firstSegment];

        // Only look a few segments ahead so a path that loops back over
        // itself (spiral ramp, overpass) cannot snap to the wrong level.
        for (int i = firstSegment; i <= searchEnd; i++)
        {
            Vector3 a = pathCorners[i];
            Vector3 b = pathCorners[i + 1];
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            float t = lengthSqr > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSqr)
                : 0f;

            Vector3 point = a + ab * t;
            float sqr = (point - position).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestSegment = i;
                bestPoint = point;
            }
        }

        pathSegmentIndex = bestSegment;

        steerPoint = PointAlongPath(bestSegment, bestPoint, lookAhead);
        Vector3 beyond = PointAlongPath(
            bestSegment, bestPoint, lookAhead * 2f
        );

        // How sharply the path bends just after the steer point.
        Vector3 inDirection = Vector3.ProjectOnPlane(
            steerPoint - position, Vector3.up
        );
        Vector3 outDirection = Vector3.ProjectOnPlane(
            beyond - steerPoint, Vector3.up
        );

        if (inDirection.sqrMagnitude > 0.01f &&
            outDirection.sqrMagnitude > 0.01f)
        {
            upcomingTurnAngle = Vector3.Angle(inDirection, outDirection);
        }

        remainingDistance = Vector3.Distance(
            bestPoint, pathCorners[bestSegment + 1]
        );

        for (int i = bestSegment + 1; i < cornerCount - 1; i++)
        {
            remainingDistance += Vector3.Distance(
                pathCorners[i], pathCorners[i + 1]
            );
        }

        return true;
    }

    private Vector3 PointAlongPath(
        int segment, Vector3 from, float distance)
    {
        Vector3 current = from;

        for (int i = segment; i < cornerCount - 1; i++)
        {
            Vector3 end = pathCorners[i + 1];
            float segmentLength = Vector3.Distance(current, end);

            if (segmentLength >= distance)
            {
                return current + (end - current)
                    / Mathf.Max(segmentLength, 0.0001f) * distance;
            }

            distance -= segmentLength;
            current = end;
        }

        return pathCorners[cornerCount - 1];
    }

    private void DrawPath(Vector3 steerPoint)
    {
        if (!showPath)
            return;

        Vector3 lift = Vector3.up * 0.3f;

        if (hasPath)
        {
            for (int i = 0; i < cornerCount - 1; i++)
            {
                Debug.DrawLine(
                    pathCorners[i] + lift,
                    pathCorners[i + 1] + lift,
                    Color.yellow
                );
            }
        }

        Debug.DrawLine(
            transform.position + Vector3.up,
            steerPoint + Vector3.up,
            Color.magenta
        );
    }

    // ------------------------------------------------------------------
    // Surface awareness and obstacle sensing
    // ------------------------------------------------------------------

    // Averages the normals under the wheels. Falls back to "up" when airborne.
    private void UpdateGroundNormal()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        AddWheelNormal(frontLeft, ref sum, ref count);
        AddWheelNormal(frontRight, ref sum, ref count);
        AddWheelNormal(rearLeft, ref sum, ref count);
        AddWheelNormal(rearRight, ref sum, ref count);

        Vector3 desired = count > 0 ? (sum / count).normalized : Vector3.up;

        groundNormal = Vector3.Slerp(
            groundNormal,
            desired,
            groundAlignSmoothing * Time.fixedDeltaTime
        ).normalized;
    }

    private static void AddWheelNormal(
        WheelCollider wheel, ref Vector3 sum, ref int count)
    {
        if (wheel.GetGroundHit(out WheelHit hit))
        {
            sum += hit.normal;
            count++;
        }
    }

    // Returns the distance to the nearest relevant obstacle.
    // The car itself, the intended target and drivable surfaces are ignored.
    private float GetClearance(float angle)
    {
        Vector3 up = alignSensorsToGround ? groundNormal : Vector3.up;

        Vector3 origin = transform.position
            + up * obstacleRayHeight
            + transform.forward * obstacleFrontOffset;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up)
            .normalized;

        Vector3 direction = Quaternion.AngleAxis(angle, up) * forward;

        // Drivable layers are excluded from the cast entirely.
        int mask = obstacleLayers & ~drivableLayers;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            obstacleSensorRadius,
            direction,
            sensorHits,
            obstacleDetectionDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = obstacleDetectionDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = sensorHits[i];

            if (hit.transform.root == transform.root)
                continue;

            // Do not steer away from the player we are attacking.
            if (target != null && hit.transform.root == target.root)
                continue;

            // Ramps and sloped tracks are roads, not walls.
            if (IsDrivableSurface(hit, origin, direction))
            {
                if (showSensorRays)
                    Debug.DrawLine(origin, hit.point, Color.cyan);
                continue;
            }

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

    private bool IsDrivableSurface(
        RaycastHit hit, Vector3 origin, Vector3 direction)
    {
        Vector3 normal = hit.normal;

        // If the sphere starts inside a collider, Unity reports distance 0 and
        // the normal is just -direction (always "vertical"). Re-query the real
        // surface normal directly from that collider.
        if (hit.distance <= 0.001f)
        {
            if (hit.collider.Raycast(
                    new Ray(origin, direction), out RaycastHit exact,
                    obstacleDetectionDistance))
            {
                normal = exact.normal;
            }
            else if (hit.collider.Raycast(
                    new Ray(origin, Vector3.down), out exact,
                    obstacleRayHeight + obstacleSensorRadius + 1f))
            {
                normal = exact.normal;
            }
            else
            {
                return false; // can't tell, play it safe
            }
        }

        // Compare against world up: a 20° ramp is 20° however the car is tilted.
        return Vector3.Angle(normal, Vector3.up) <= maxDrivableSlope;
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

    // ------------------------------------------------------------------
    // Driving
    // ------------------------------------------------------------------

    private void DriveTowardsTarget()
    {
        Vector3 aimPosition = GetAimPosition();
        Vector3 toAim = aimPosition - transform.position;

        if (toAim.sqrMagnitude < 0.01f)
        {
            ApplyBrakes(coastBrake);
            SetSteering(0f);
            return;
        }

        float speed = rb.linearVelocity.magnitude;

        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        // True 3D distance, so a target on an overpass above us does not
        // count as "in ramming range".
        float directDistance = Vector3.Distance(
            transform.position, target.position
        );

        // Collision events are the normal way to detect a ram. This fallback
        // handles wheel/collider setups that can leave the two cars pressed
        // together without producing a fresh collision event on later charges.
        if (directDistance <= ramContactDistance &&
            (forwardSpeed >= ramContactSpeed || speed <= stuckSpeed) &&
            ramCooldownTimer <= 0f)
        {
            BeginRamRetreat();
            return;
        }

        // --- Decide where to steer -------------------------------------
        // Default: charge straight at the aim position.
        Vector3 steerPoint = aimPosition;
        float approachDistance = Vector3.ProjectOnPlane(
            toAim, Vector3.up
        ).magnitude;
        float upcomingTurn = 0f;

        if (useNavMesh)
        {
            UpdatePath(aimPosition);

            // Close, level and with nothing in between: skip the path and
            // aim directly so the ram stays accurate.
            bool chargeDirectly =
                directDistance < directPursuitDistance &&
                Mathf.Abs(toAim.y) < maxDirectHeightDifference &&
                HasClearNavLine(aimPosition);

            if (!chargeDirectly &&
                TryGetPathSteerPoint(
                    pathLookAhead + speed * pathLookAheadSpeedFactor,
                    out Vector3 pathPoint,
                    out float remaining,
                    out float turnAngle))
            {
                steerPoint = pathPoint;
                approachDistance = remaining;
                upcomingTurn = turnAngle;
            }
        }

        DrawPath(steerPoint);

        Vector3 flatDirection = Vector3.ProjectOnPlane(
            steerPoint - transform.position, Vector3.up
        );

        if (flatDirection.sqrMagnitude < 0.01f)
            flatDirection = transform.forward;

        float angle = Vector3.SignedAngle(
            transform.forward,
            flatDirection.normalized,
            Vector3.up
        );

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
            approachDistance > minimumTargetDistanceForRecovery;

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
            approachDistance < targetBrakeDistance && speedKmh > 20f;

        // Reduce speed before sharp turns instead of permanently
        // braking whenever the steer point is behind us.
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

        // Slow down for a bend in the path before reaching it.
        throttle *= Mathf.Lerp(
            1f,
            0.55f,
            Mathf.InverseLerp(30f, 100f, upcomingTurn)
        );

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
        if (approachDistance < attackDistance && speedKmh > 35f)
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
            forceRepath = true;
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
            forceRepath = true;
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

    // ------------------------------------------------------------------
    // Wheel visuals (mirrors the player controller)
    // ------------------------------------------------------------------

    private void UpdateWheelVisuals()
    {
        UpdateWheelVisual(frontLeft, frontLeftVisual);
        UpdateWheelVisual(frontRight, frontRightVisual);
        UpdateWheelVisual(rearLeft, rearLeftVisual);
        UpdateWheelVisual(rearRight, rearRightVisual);
    }

    private void UpdateWheelVisual(
        WheelCollider wheel,
        WheelVisual wheelVisual)
    {
        if (wheel == null || wheelVisual == null ||
            wheelVisual.transform == null)
            return;

        wheel.GetWorldPose(
            out Vector3 position,
            out Quaternion rotation
        );

        wheelVisual.transform.SetPositionAndRotation(
            position,
            rotation * wheelVisual.rotationOffset
        );
    }

    private void BindModelWheels(GameObject model)
    {
        if (model == null || !WheelsAssigned())
        {
            BindLegacyWheels();
            return;
        }

        ClearGeneratedWheelMeshes();

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        var usedRenderers = new HashSet<Renderer>();
        bool hasNamedWheelRenderers = false;

        foreach (Renderer renderer in renderers)
        {
            if (IsWheelRenderer(renderer))
            {
                hasNamedWheelRenderers = true;
                break;
            }
        }

        frontLeftVisual = BindWheelVisual(
            frontLeft, renderers, usedRenderers, frontLeftMesh,
            hasNamedWheelRenderers
        );
        frontRightVisual = BindWheelVisual(
            frontRight, renderers, usedRenderers, frontRightMesh,
            hasNamedWheelRenderers
        );
        rearLeftVisual = BindWheelVisual(
            rearLeft, renderers, usedRenderers, rearLeftMesh,
            hasNamedWheelRenderers
        );
        rearRightVisual = BindWheelVisual(
            rearRight, renderers, usedRenderers, rearRightMesh,
            hasNamedWheelRenderers
        );
    }

    private void BindLegacyWheels()
    {
        ClearGeneratedWheelMeshes();
        frontLeftVisual = CreateLegacyWheelVisual(frontLeftMesh);
        frontRightVisual = CreateLegacyWheelVisual(frontRightMesh);
        rearLeftVisual = CreateLegacyWheelVisual(rearLeftMesh);
        rearRightVisual = CreateLegacyWheelVisual(rearRightMesh);
    }

    private WheelVisual BindWheelVisual(
        WheelCollider wheel,
        Renderer[] modelRenderers,
        HashSet<Renderer> usedRenderers,
        Transform legacyVisual,
        bool hasNamedWheelRenderers)
    {
        if (wheel == null)
            return null;

        Renderer closestRenderer = null;
        float closestDistance = float.PositiveInfinity;

        foreach (Renderer renderer in modelRenderers)
        {
            if (!renderer.enabled || usedRenderers.Contains(renderer))
                continue;

            // Skip cylinders we generated earlier.
            if (generatedWheelMeshes.Contains(renderer.gameObject))
                continue;

            if (hasNamedWheelRenderers && !IsWheelRenderer(renderer))
                continue;

            float distance =
                (renderer.bounds.center - wheel.transform.position)
                .sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestRenderer = renderer;
            }
        }

        if (closestRenderer == null)
        {
            Debug.LogWarning(
                $"{name}: Could not find a model wheel for {wheel.name}."
            );
            return CreateLegacyWheelVisual(legacyVisual);
        }

        usedRenderers.Add(closestRenderer);

        // Use the model wheel's bounds to fit the physical wheel collider.
        // The generated cylinder avoids relying on the imported mesh pivot.
        wheel.transform.position = closestRenderer.bounds.center;
        wheel.radius = Mathf.Max(0.05f, closestRenderer.bounds.extents.y);

        wheel.GetWorldPose(out _, out Quaternion wheelRotation);

        if (legacyVisual != null)
            legacyVisual.gameObject.SetActive(false);

        closestRenderer.enabled = false;
        hiddenModelWheelRenderers.Add(closestRenderer);

        Transform cylinder = CreateWheelCylinder(
            closestRenderer,
            wheel.radius
        );

        return new WheelVisual
        {
            transform = cylinder,
            rotationOffset = Quaternion.Inverse(wheelRotation) *
                cylinder.rotation
        };
    }

    private Transform CreateWheelCylinder(
        Renderer sourceRenderer,
        float radius)
    {
        GameObject cylinder = GameObject.CreatePrimitive(
            PrimitiveType.Cylinder
        );
        cylinder.name = $"{sourceRenderer.name} Wheel Visual";
        cylinder.layer = gameObject.layer;

        Collider cylinderCollider = cylinder.GetComponent<Collider>();
        if (cylinderCollider != null)
        {
            cylinderCollider.enabled = false;
            Destroy(cylinderCollider);
        }

        cylinder.transform.SetParent(transform, true);
        cylinder.transform.position = sourceRenderer.bounds.center;
        cylinder.transform.rotation = transform.rotation *
            Quaternion.Euler(0f, 0f, 90f);

        float width = Mathf.Max(0.05f, sourceRenderer.bounds.size.x);
        cylinder.transform.localScale = new Vector3(
            radius * 2f,
            width * 0.5f,
            radius * 2f
        );

        MeshRenderer cylinderRenderer = cylinder.GetComponent<MeshRenderer>();
        if (cylinderRenderer != null && tyreMaterial != null)
            cylinderRenderer.sharedMaterial = tyreMaterial;

        generatedWheelMeshes.Add(cylinder);
        return cylinder.transform;
    }

    private WheelVisual CreateLegacyWheelVisual(Transform legacyVisual)
    {
        if (legacyVisual == null)
            return null;

        legacyVisual.gameObject.SetActive(true);

        return new WheelVisual
        {
            transform = legacyVisual,
            rotationOffset = Quaternion.Euler(wheelMeshRotationOffset)
        };
    }

    private void ClearGeneratedWheelMeshes()
    {
        DestroyGeneratedWheelMeshes();

        foreach (Renderer renderer in hiddenModelWheelRenderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        hiddenModelWheelRenderers.Clear();
    }

    private void DestroyGeneratedWheelMeshes()
    {
        foreach (GameObject wheelMesh in generatedWheelMeshes)
        {
            if (wheelMesh != null)
                Destroy(wheelMesh);
        }

        generatedWheelMeshes.Clear();
    }

    private bool IsWheelRenderer(Renderer renderer)
    {
        string objectName = renderer.name.ToLowerInvariant();
        return objectName.Contains("wheel") ||
               objectName.Contains("tire") ||
               objectName.Contains("tyre") ||
               objectName.Contains("rim");
    }
}