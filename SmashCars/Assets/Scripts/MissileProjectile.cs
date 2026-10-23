
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class MissileProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float lifetime = 6f;

    private Rigidbody rb;
    private Transform ownerRoot;
    private float damage;
    private ParticleSystem impactEffectPrefab;
    private bool hasImpacted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(
        Transform owner,
        float speed,
        float missileDamage,
        ParticleSystem impactEffect)
    {
        ownerRoot = owner;
        damage = missileDamage;
        impactEffectPrefab = impactEffect;

        // Prevent the missile from colliding with its own car.
        if (ownerRoot != null)
        {
            Collider[] ownerColliders =
                ownerRoot.GetComponentsInChildren<Collider>();

            Collider[] missileColliders =
                GetComponentsInChildren<Collider>();

            foreach (Collider ownerCollider in ownerColliders)
            {
                foreach (Collider missileCollider in missileColliders)
                {
                    if (ownerCollider != null && missileCollider != null)
                    {
                        Physics.IgnoreCollision(
                            ownerCollider,
                            missileCollider
                        );
                    }
                }
            }
        }

        rb.useGravity = false;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = transform.forward * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted)
            return;

        if (ownerRoot != null &&
            collision.transform.root == ownerRoot)
            return;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        Vector3 normal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : -transform.forward;

        Impact(point, normal, collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted)
            return;

        if (ownerRoot != null &&
            other.transform.root == ownerRoot)
            return;

        Impact(other.ClosestPoint(transform.position),
               -transform.forward, other);
    }

    private void Impact(
        Vector3 point,
        Vector3 normal,
        Collider hitCollider)
    {
        if (hasImpacted)
            return;

        hasImpacted = true;

        CarHealth hitCar =
            hitCollider.GetComponentInParent<CarHealth>();

        if (hitCar != null && !hitCar.IsEliminated &&
            hitCar.transform.root != ownerRoot)
        {
            hitCar.TakeDamage(damage);
        }

        if (impactEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(
                impactEffectPrefab,
                point,
                Quaternion.LookRotation(normal)
            );

            effect.Play();
            Destroy(effect.gameObject, 3f);
        }

        Destroy(gameObject);
    }
}
