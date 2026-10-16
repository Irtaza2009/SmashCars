
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarWheelVisuals : MonoBehaviour
{
    [Header("Wheel Dimensions")]
    [SerializeField] private float wheelRadius = 0.38f;
    [SerializeField] private float wheelWidth = 0.24f;

    [Header("Wheel Placement")]
    [SerializeField] private float wheelOffsetX = 0.92f;
    [SerializeField] private float wheelOffsetZ = 1.0f;
    [SerializeField] private float wheelCenterY = -0.62f;

    [Header("Steering")]
    [SerializeField] private float maxSteerAngle = 30f;

    [Header("Appearance")]
    [SerializeField] private Material wheelMaterial;

    private Rigidbody rb;

    private Transform[] steeringPivots = new Transform[4];
    private Transform[] wheelMeshes = new Transform[4];

    private float steeringInput;

    private const int FrontLeft = 0;
    private const int FrontRight = 1;
    private const int RearLeft = 2;
    private const int RearRight = 3;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        CreateWheel(FrontLeft, -wheelOffsetX, wheelOffsetZ, true);
        CreateWheel(FrontRight, wheelOffsetX, wheelOffsetZ, true);
        CreateWheel(RearLeft, -wheelOffsetX, -wheelOffsetZ, false);
        CreateWheel(RearRight, wheelOffsetX, -wheelOffsetZ, false);
    }

    private void Update()
    {
        steeringInput = Input.GetAxis("Horizontal");

        UpdateSteering();
        AnimateWheels();
    }

    private void CreateWheel(
        int index,
        float x,
        float z,
        bool canSteer)
    {
        GameObject pivot = new GameObject(
            index switch
            {
                FrontLeft => "FrontLeftWheel",
                FrontRight => "FrontRightWheel",
                RearLeft => "RearLeftWheel",
                _ => "RearRightWheel"
            }
        );

        pivot.transform.SetParent(transform, false);
        pivot.transform.localPosition =
            new Vector3(x, wheelCenterY, z);

        steeringPivots[index] = pivot.transform;

        GameObject wheel = GameObject.CreatePrimitive(
            PrimitiveType.Cylinder
        );

        wheel.name = "WheelMesh";
        wheel.transform.SetParent(pivot.transform, false);
        wheel.transform.localPosition = Vector3.zero;

        // A cylinder's axis is Y. Rotate it so its axle is X.
        wheel.transform.localRotation =
            Quaternion.Euler(0f, 0f, 90f);

        // Cylinder defaults: diameter 1, height 2.
        wheel.transform.localScale = new Vector3(
            wheelRadius * 2f,
            wheelWidth * 0.5f,
            wheelRadius * 2f
        );

        // Add a physical tire collider.
        CapsuleCollider wheelCollider = wheel.AddComponent<CapsuleCollider>();

        wheelCollider.direction = 0; // X axis
        wheelCollider.radius = wheelRadius;
        wheelCollider.height = wheelWidth;
        wheelCollider.material = null;

        if (wheelMaterial != null)
        {
            wheel.GetComponent<MeshRenderer>().material =
                wheelMaterial;
        }

        wheelMeshes[index] = wheel.transform;
    }

    private void UpdateSteering()
    {
        for (int i = 0; i < steeringPivots.Length; i++)
        {
            if (steeringPivots[i] == null)
                continue;

            bool frontWheel = i == FrontLeft || i == FrontRight;

            float angle = frontWheel
                ? steeringInput * maxSteerAngle
                : 0f;

            steeringPivots[i].localRotation =
                Quaternion.Euler(0f, angle, 0f);
        }
    }

    private void AnimateWheels()
    {
        float forwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.forward
        );

        float rollAngle =
            forwardSpeed / wheelRadius *
            Mathf.Rad2Deg *
            Time.deltaTime;

        foreach (Transform wheel in wheelMeshes)
        {
            if (wheel == null)
                continue;

            // Roll around the cylinder's axle.
            wheel.Rotate(
                Vector3.up,
                rollAngle,
                Space.Self
            );
        }
    }
}
