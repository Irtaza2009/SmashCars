
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CarHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float minImpactSpeed = 4f;
    [SerializeField] private float damagePerSpeed = 2.5f;
    [SerializeField] private float maxCollisionDamage = 30f;

    [Header("Health HUD")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TMP_Text healthText;

    [Header("Elimination")]
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private Collider[] carColliders;
    [SerializeField] private Renderer[] carRenderers;
    [SerializeField] private Behaviour[] disableOnElimination;

    private float currentHealth;
    private bool isEliminated;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsEliminated => isEliminated;

    private void Awake()
    {
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();

        if (carColliders == null || carColliders.Length == 0)
            carColliders = GetComponentsInChildren<Collider>();

        if (carRenderers == null || carRenderers.Length == 0)
            carRenderers = GetComponentsInChildren<Renderer>();

        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isEliminated)
            return;

        CarHealth otherCar =
            collision.gameObject.GetComponentInParent<CarHealth>();

        if (otherCar == null || otherCar == this || otherCar.IsEliminated)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minImpactSpeed)
            return;

        float damage = Mathf.Min(
            (impactSpeed - minImpactSpeed) * damagePerSpeed,
            maxCollisionDamage
        );

        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        if (isEliminated || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        UpdateHealthUI();

        Debug.Log(
            $"{name} took {damage:F0} damage. " +
            $"Health: {currentHealth:F0}/{maxHealth:F0}"
        );

        if (currentHealth <= 0f)
            Eliminate();
    }

    public void Eliminate()
    {
        if (isEliminated)
            return;

        isEliminated = true;

        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
            carRigidbody.isKinematic = true;
        }

        if (disableOnElimination != null)
        {
            foreach (Behaviour behaviour in disableOnElimination)
            {
                if (behaviour != null && behaviour != this)
                    behaviour.enabled = false;
            }
        }

        foreach (Collider carCollider in carColliders)
        {
            if (carCollider != null)
                carCollider.enabled = false;
        }

        foreach (Renderer carRenderer in carRenderers)
        {
            if (carRenderer != null)
                carRenderer.enabled = false;
        }

        Debug.Log($"{name} has been eliminated!");
    }

    private void UpdateHealthUI()
    {
        float healthRatio = maxHealth > 0f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;

        if (healthBarFill != null)
            healthBarFill.fillAmount = healthRatio;

        if (healthText != null)
            healthText.text =
                $"HEALTH: {Mathf.CeilToInt(currentHealth):000}";
    }
}
