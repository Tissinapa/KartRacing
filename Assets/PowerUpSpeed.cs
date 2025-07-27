using Ilumisoft.ArcardeRacingKit;
using UnityEngine;

public class PowerUpSpeed : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PickUp(other);

        }
    }
    void PickUp(Collider player)
    {
        VehicleStats stats = player.GetComponent<VehicleStats>();
        stats.MaxSpeed += 20;

        Destroy(gameObject);

    }
}
