using UnityEngine;
using Ilumisoft.ArcardeRacingKit;

[CreateAssetMenu(menuName = "PowerUps/SpeedUp")]
public class SpeedUp : PowerUpEffect
{
    public float amount;

    public override void Apply(GameObject target)
    {
        Vehicle vehicle = target.GetComponentInParent<Vehicle>();
        //target.GetComponent<VehicleStats>().MaxSpeed += amount;

        if (vehicle != null)
        {
            vehicle.FinalStats.MaxSpeed += amount;
        }
        else 
        {
            Debug.LogWarning("PowerUpSpeed: Vehicle component not found on target.");
        }

    }
}
