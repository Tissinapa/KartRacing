using UnityEngine;

namespace Ilumisoft.ArcardeRacingKit.Effects
{
    /// <summary>
    /// Allows to create a speed line effect when the vehicle reaches a specific speed.
    /// Please note that the effect will not look good if using a first person or top view.
    /// </summary>
    public class CameraSpeedLinesEffect : VehicleComponent
    {
        [SerializeField, Range(0, 1)]
        [Tooltip("The min rate of it's max velociyt the vehicle needs to reach, in order to produce speed lines")]
        float startVelocity = 0.5f;

        [SerializeField]
        [Tooltip("The max emission rate of the particle system")]
        float maxEmissionRate = 50;

        [SerializeField]
        [Tooltip("The particle system creating the speed lines")]
        new ParticleSystem particleSystem = null;

        Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        void Update()
        {
            UpdateEmission();

            UpdateRotation();
        }

        /// <summary>
        /// Adjusts the emission rate of the particle system depending of the velocity of the vehicle
        /// </summary>
        void UpdateEmission()
        {
            // Only show speed line particles if the vehicle is moving and faster than the required velocity
            if (Vehicle.NormalizedForwardSpeed >= startVelocity && Vehicle.CanMove)
            {
                var emission = particleSystem.emission;

                //Interpolate the emission of the particle system
                var t = startVelocity > 0 ? Vehicle.NormalizedForwardSpeed / startVelocity : 1.0f;

                emission.rateOverTime = Mathf.Lerp(0, maxEmissionRate, t);
            }
            // Otherwise stop emitting
            else
            {
                var emission = particleSystem.emission;
                emission.rateOverTime = 0;
            }
        }

        /// <summary>
        /// Adjusts the rotation in order to make the speed lines point into the view of the camera
        /// </summary>
        void UpdateRotation()
        {
            var targetPos = mainCamera.transform.position;
            targetPos.y = transform.position.y;

            transform.LookAt(targetPos);
            transform.forward = -transform.forward;
        }
    }
}