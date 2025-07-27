using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Ilumisoft.ArcardeRacingKit.Effects
{
    /// <summary>
    /// Smoothly blends in a given post processing volume when the vehicle accelerates to create a nice visual speed effect.
    /// </summary>
    public class SpeedPostProcessingEffect : VehicleComponent
    {
        [SerializeField]
        PostProcessVolume postProcessVolume = null;

        private void Update()
        {
            if (postProcessVolume != null && Vehicle != null)
            {
                float speed = Vehicle.CanMove ? Vehicle.NormalizedForwardSpeed : 0.0f;

                // Adjust the weight of the volume according to the normalized speed of the vehicle
                postProcessVolume.weight = speed;
            }
        }
    }
}