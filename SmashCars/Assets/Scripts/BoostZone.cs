using UnityEngine;
using System.Collections;


public class BoostZone : MonoBehaviour
{
    [Header("Target")]
    public Transform upperPlatform;
    public float extraHeight = 10f;

    [Header("Movement")]
    public float liftSpeed = 5f;
    private bool isLifting = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isLifting || !other.CompareTag("Player"))
            return;

        Rigidbody rb = other.attachedRigidbody;

        if(rb != null)
        {
            StartCoroutine(LiftCar(rb));
            Debug.Log("Lifting car!");
        }
    Debug.Log("Something entered the boost zone: " + other.name);
    }

    private IEnumerator LiftCar(Rigidbody rb)
    {
        isLifting = true;

        bool wasKinematic = rb.isKinematic;
        rb.isKinematic = true;

        Vector3 targetPosition = new Vector3(
            rb.position.x,
            upperPlatform.position.y + extraHeight,
            rb.position.z
        
        );

        while (rb != null && rb.position.y < targetPosition.y)
        {
            Vector3 nextPosition = Vector3.MoveTowards(
                rb.position,
                targetPosition,
                liftSpeed * Time.fixedDeltaTime
            );

        rb.position = nextPosition;

        yield return new WaitForFixedUpdate();

    }
    if (rb != null)
        {
            rb.position = targetPosition;
            rb.isKinematic = wasKinematic;
        }

        isLifting = false;

    }

    
}
