using UnityEngine;
using Unity.Cinemachine;

namespace Ilumisoft.ArcardeRacingKit
{
    /// <summary>
    /// This is actually a helper class allowing to attach a cinemachine virtual camera to a vehicle prefab.
    /// Since a virtual camera should not be a child of its target, the behaviour will automatically unparent it on start. 
    /// </summary>
    public class VehicleCamera : VehicleComponent
    {
        [SerializeField]
        CinemachineCamera cinemachineCamera = null;

        void Start()
        {
            SetupCamera();
        }

        /// <summary>
        /// Umparents the virtual camera and set it to follow and look at the vehicle
        /// </summary>
        void SetupCamera()
        {
            if(Vehicle != null)
            {
                cinemachineCamera.transform.SetParent(null);

                if (cinemachineCamera.gameObject.activeSelf == false)
                {
                    cinemachineCamera.gameObject.SetActive(true);
                }
            }
        }
    }
}