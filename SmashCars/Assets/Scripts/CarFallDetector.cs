using UnityEngine;
using System.Collections;

public class CarFallDetector : MonoBehaviour
{
    [SerializeField] private CarHealth carHealth;
    [SerializeField] private float fallHeight = -15f;

    private void Awake()
    {
        if (carHealth == null)
            carHealth = GetComponentInParent<CarHealth>();
    }

    private void Update()
    {
        if (carHealth == null || carHealth.IsEliminated)
            return;

        if (transform.position.y < fallHeight)
        {
            carHealth.TakeDamage(carHealth.MaxHealth);
            Debug.Log($"{name} fell out of arena!");
            
        }
    }

}
