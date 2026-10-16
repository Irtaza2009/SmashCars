using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CarHUD : MonoBehaviour
{
    [Header("Car")]
    [SerializeField] private Rigidbody carRigidbody;

    [Header("Speedometer")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text unitsText;
    [SerializeField] private Image speedBarFill;
    [SerializeField] private float maxSpeedKmh = 240f;

    [Header("Status")]
    [SerializeField] private TMP_Text handbrakeText;
    [SerializeField] private TMP_Text driftScoreText;

    private void Start()
    {
        if (unitsText != null)
            unitsText.text = "KM/H";

        if (driftScoreText != null)
            driftScoreText.text = "DRIFT: 000";
    }

    private void Update()
    {
        if (carRigidbody == null)
            return;

        // Horizontal speed in km/h, excluding vertical movement.
        Vector3 flatVelocity = Vector3.ProjectOnPlane(
            carRigidbody.linearVelocity,
            Vector3.up
        );

        float speedKmh = flatVelocity.magnitude * 3.6f;

        if (speedText != null)
            speedText.text = Mathf.RoundToInt(speedKmh).ToString("000");

        if (speedBarFill != null)
            speedBarFill.fillAmount = Mathf.Clamp01(
                speedKmh / Mathf.Max(1f, maxSpeedKmh)
            );

        if (handbrakeText != null)
        {
            bool handbrakeOn = Input.GetKey(KeyCode.LeftShift);

            handbrakeText.text = handbrakeOn
                ? "HANDBRAKE: ON"
                : "HANDBRAKE: OFF";

            handbrakeText.color = handbrakeOn
                ? new Color32(255, 107, 107, 255)
                : new Color32(127, 231, 255, 255);
        }
    }
}
