using Ilumisoft.ArcardeRacingKit;
using UnityEngine;

[CreateAssetMenu(menuName = "PowerUps/GripUp")]
public class GripUp : PowerUpEffect
{
    public float amount;

    public override void Apply(GameObject target)
    {
        Vehicle vehicle = target.GetComponentInParent<Vehicle>();


        if (vehicle != null)
        {
            vehicle.FinalStats.SteeringPower += amount;
        }
        else
        {
            Debug.LogWarning("PowerUpSpeed: Vehicle component not found on target.");
        }

    }
}
