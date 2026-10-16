
using UnityEngine;

public class CarCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 7f;
    [SerializeField] private float height = 3.5f;
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float lookAhead = 3f;


    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition =
            target.position
            - target.forward * distance
            + Vector3.up * height;

        float positionT =
            1f - Mathf.Exp(-followSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionT
        );

        Vector3 lookPosition =
            target.position + target.forward * lookAhead;

        Vector3 lookDirection = lookPosition - transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(
                lookDirection,
                Vector3.up
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                1f - Mathf.Exp(-6f * Time.deltaTime)
            );
        }
    }

}
