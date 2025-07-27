using UnityEngine;
using Ilumisoft.ArcardeRacingKit;

[CreateAssetMenu(menuName = "PowerUps/AccelerationUp")]

public class AccelerationUp : PowerUpEffect
{
    public float amount;

    public override void Apply(GameObject target)
    {
        Vehicle vehicle = target.GetComponentInParent<Vehicle>();
        

        if (vehicle != null)
        {
            vehicle.FinalStats.Acceleration += amount;
        }
        else
        {
            Debug.LogWarning("PowerUpSpeed: Vehicle component not found on target.");
        }

    }
}
