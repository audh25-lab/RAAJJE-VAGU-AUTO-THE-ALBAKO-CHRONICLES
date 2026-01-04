using UnityEngine;
using RVA.TAC.Vehicles;

namespace RVA.TAC.Vehicles
{
    /// <summary>
    /// A simple AI controller for traffic vehicles. It follows a path of waypoints.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class TrafficVehicleAI : MonoBehaviour
    {
        private VehicleController vehicleController;
        private Waypoint currentWaypoint;

        public void Initialize(Waypoint startWaypoint)
        {
            vehicleController = GetComponent<VehicleController>();
            currentWaypoint = startWaypoint;
        }

        void Update()
        {
            if (currentWaypoint == null)
            {
                // No waypoint, just stop.
                vehicleController.SetThrottleInput(0);
                return;
            }

            // Simple AI: Move towards the current waypoint
            Vector3 direction = (currentWaypoint.transform.position - transform.position).normalized;
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);

            // Steer towards the waypoint
            vehicleController.SetSteeringInput(Mathf.Clamp(angle / 45f, -1f, 1f));
            vehicleController.SetThrottleInput(0.5f); // Drive at half speed

            // If we're close to the waypoint, get the next one
            if (Vector3.Distance(transform.position, currentWaypoint.transform.position) < 5f)
            {
                currentWaypoint = TrafficManager.Instance.GetNextWaypoint(currentWaypoint);
            }
        }
    }
}
