using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCarController : MonoBehaviour
{
    [Header("Driving")]
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float reverseAcceleration = 12f;
    [SerializeField] private float maxSpeed = 25f;
    [SerializeField] private float brakeStrength = 18f;

    [Header("Steering")]
    [SerializeField] private float turnStrength = 100f;
    [SerializeField] private float minimumSteeringSpeed = 0.15f;

    [Header("Grip")]
    [SerializeField] private float lateralGrip = 5f;
    [SerializeField] private float driftGrip = 1.5f;

    [Header("Stability")]
    [SerializeField] private float downforce = 2f;

    private Rigidbody rb;
    private float throttleInput;
    private float steeringInput;
    private bool braking;
    private bool drifting;

    private Vector3 startPosition;
    private Quaternion startRotation;

    public float Speed => rb == null ? 0f : rb.linearVelocity.magnitude * 3.6f;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        startPosition = transform.position;
        startRotation = transform.rotation;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = new Vector3(0f, -0.3f, 0f);
    }

    private void Update()
    {
        throttleInput = Input.GetAxis("Vertical");
        steeringInput = Input.GetAxis("Horizontal");

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
        Vector3 forwardVelocity = Vector3.Project(rb.linearVelocity, transform.forward);

        float forwardSpeed = forwardVelocity.magnitude;

        if (throttleInput > 0f && forwardSpeed < maxSpeed)
        {
            rb.AddForce(transform.forward * throttleInput * acceleration, ForceMode.Acceleration);
        }
        else if (throttleInput < 0f)
        {
            if (Vector3.Dot(rb.linearVelocity, transform.forward) > 1f)
            {
                rb.AddForce(-transform.forward * brakeStrength, ForceMode.Acceleration);
            }
            else
            {
                rb.AddForce(transform.forward * throttleInput * reverseAcceleration, ForceMode.Acceleration);
            }
        }

        if (braking)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.AddForce(-velocity.normalized * brakeStrength, ForceMode.Acceleration);
        }
    }

    private void Steer()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(forwardSpeed / 5f);

        if (speedFactor < minimumSteeringSpeed)
            return;

        float reverseFactor = forwardSpeed >= 0f ? 1f : -1f;

        float turn = steeringInput * turnStrength * speedFactor * reverseFactor * Time.fixedDeltaTime;

        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }

    private void ApplyGrip()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        float grip = drifting ? driftGrip : lateralGrip;

        rb.AddForce(-transform.right * localVelocity.x * grip, ForceMode.Acceleration);
    }

    private void ApplyDownforce()
    {
        rb.AddForce(-transform.up * rb.linearVelocity.magnitude * downforce, ForceMode.Acceleration);
    }

    private void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(startPosition, startRotation);
    }

}
