using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCarController : MonoBehaviour
{
    [Header("Driving")]
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float reverseAcceleration = 18f;
    [SerializeField] private float maxSpeed = 25f;
    [SerializeField] private float maxReverseSpeed = 10f;
    [SerializeField] private float brakeStrength = 22f;
    [SerializeField] private float rollingResistance = 0.12f;

    [Header("Steering")]
    [SerializeField] private float turnStrength = 110f;
    [SerializeField] private float steeringSpeed = 5f;

    [Header("Grip")]
    [SerializeField] private float lateralGrip = 4f;
    [SerializeField] private float driftGrip = 1.2f;

    [Header("Stability")]
    [SerializeField] private float downforce = 0.5f;

    private Rigidbody rb;

    private float throttleInput;
    private float steeringInput;
    private bool braking;
    private bool drifting;

    private Vector3 startPosition;
    private Quaternion startRotation;

    public float Speed =>
        rb == null ? 0f : rb.linearVelocity.magnitude * 3.6f;

    public float ForwardSpeed =>
        rb == null
            ? 0f
            : Vector3.Dot(rb.linearVelocity, transform.forward);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        startPosition = transform.position;
        startRotation = transform.rotation;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 0f;
        rb.angularDamping = 1f;
        rb.centerOfMass = new Vector3(0f, -0.3f, 0f);
    }

    private void Update()
    {
        throttleInput = Input.GetAxisRaw("Vertical");
        steeringInput = Input.GetAxisRaw("Horizontal");

        braking = Input.GetKey(KeyCode.Space);
        drifting = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCar();
        }
    }

    private void FixedUpdate()
    {
        Drive();
        Steer();
        ApplyGrip();
        ApplyDownforce();
    }

    private void Drive()
    {
        float forwardSpeed = ForwardSpeed;

        if (braking)
        {
            // Strong, deliberate braking.
            ApplyBraking(brakeStrength);
            return;
        }

        if (throttleInput > 0f)
        {
            // Accelerate forward, respecting the forward speed limit.
            if (forwardSpeed < maxSpeed)
            {
                rb.AddForce(
                    transform.forward * acceleration * throttleInput,
                    ForceMode.Acceleration
                );
            }
        }
        else if (throttleInput < 0f)
        {
            if (forwardSpeed > 0.5f)
            {
                // Moving forward: S brakes before reversing.
                ApplyBraking(brakeStrength);
            }
            else if (forwardSpeed > -maxReverseSpeed)
            {
                // Slow enough: accelerate in reverse.
                rb.AddForce(
                    -transform.forward *
                    reverseAcceleration *
                    -throttleInput,
                    ForceMode.Acceleration
                );
            }
        }
        else
        {
            // Coast freely. Rolling resistance gently reduces speed over time.
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                rb.linearVelocity,
                Vector3.up
            );

            float speed = horizontalVelocity.magnitude;

            if (speed > 0.05f)
            {
                rb.AddForce(
                    -horizontalVelocity.normalized * rollingResistance,
                    ForceMode.Acceleration
                );
            }
        }

        // Clamp forward and reverse speed without removing vertical velocity.
        Vector3 verticalVelocity =
            Vector3.Project(rb.linearVelocity, Vector3.up);

        Vector3 horizontal =
            Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);

        float signedSpeed = Vector3.Dot(
            horizontal,
            transform.forward
        );

        if (signedSpeed > maxSpeed)
        {
            horizontal -= transform.forward *
                          (signedSpeed - maxSpeed);
        }
        else if (signedSpeed < -maxReverseSpeed)
        {
            horizontal += transform.forward *
                          (-maxReverseSpeed - signedSpeed);
        }

        rb.linearVelocity = horizontal + verticalVelocity;
    }

    private void ApplyBraking(float strength)
    {
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
            rb.linearVelocity,
            Vector3.up
        );

        if (horizontalVelocity.sqrMagnitude < 0.01f)
            return;

        rb.AddForce(
            -horizontalVelocity.normalized * strength,
            ForceMode.Acceleration
        );
    }

    private void Steer()
    {
        float forwardSpeed = ForwardSpeed;

        // Steering authority increases with speed in either direction.
        float speedFactor = Mathf.Clamp01(
            Mathf.Abs(forwardSpeed) / steeringSpeed
        );

        if (speedFactor < 0.01f)
            return;

        float reverseFactor = forwardSpeed >= 0f ? 1f : -1f;

        float turn = steeringInput *
                     turnStrength *
                     speedFactor *
                     reverseFactor *
                     Time.fixedDeltaTime;

        rb.MoveRotation(
            rb.rotation * Quaternion.Euler(0f, turn, 0f)
        );
    }

    private void ApplyGrip()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(
            rb.linearVelocity
        );

        float grip = drifting ? driftGrip : lateralGrip;

        // Remove sideways sliding progressively, not instantly.
        rb.AddForce(
            -transform.right * localVelocity.x * grip,
            ForceMode.Acceleration
        );
    }

    private void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;

        if (speed > 1f)
        {
            rb.AddForce(
                -transform.up * speed * downforce,
                ForceMode.Acceleration
            );
        }
    }

    private void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = startPosition;
        rb.rotation = startRotation;
    }
}
