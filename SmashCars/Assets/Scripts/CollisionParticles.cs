using UnityEngine;

public class CollisionParticles : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private ParticleSystem collisionEffect;

    [Header("Impact Settings")]
    [SerializeField] private float minimumImpactSpeed = 3f;
    [SerializeField] private float strongImpactSpeed = 12f;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnOffset = 0.05f;
    [SerializeField] private float destroyAfter = 3f;

    
    private void OnCollisionEnter(Collision collision)
    {
        if (collisionEffect == null || collision.contactCount == 0)
            return;

        CarHealth otherCar =
            collision.gameObject.GetComponentInParent<CarHealth>();

        if (otherCar == null || otherCar == GetComponent<CarHealth>())
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minimumImpactSpeed)
            return;

        ContactPoint contact = collision.GetContact(0);

        Vector3 spawnPosition =
            contact.point + contact.normal * spawnOffset;

        Quaternion spawnRotation =
            Quaternion.FromToRotation(Vector3.up, contact.normal);

        ParticleSystem effect = Instantiate(
            collisionEffect,
            spawnPosition,
            spawnRotation
        );

        float intensity = Mathf.InverseLerp(
            minimumImpactSpeed,
            strongImpactSpeed,
            impactSpeed
        );

        var emission = effect.emission;
        emission.SetBurst(
            0,
            new ParticleSystem.Burst(
                0f,
                (short)Mathf.RoundToInt(
                    Mathf.Lerp(10f, 40f, intensity)
                )
            )
        );

        effect.Play();
        Destroy(effect.gameObject, destroyAfter);
    }


   }