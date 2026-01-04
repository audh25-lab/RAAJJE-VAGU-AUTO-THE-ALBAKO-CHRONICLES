using UnityEngine;
using RVA.TAC.Vehicles;

namespace RVA.TAC.Vehicles
{
    /// <summary>
    /// Manages the traffic system, including waypoints and vehicle spawning.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public static TrafficManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        public Waypoint GetNextWaypoint(Waypoint currentWaypoint)
        {
            if (currentWaypoint == null || currentWaypoint.nextWaypoints.Count == 0)
            {
                return null;
            }
            return currentWaypoint.nextWaypoints[Random.Range(0, currentWaypoint.nextWaypoints.Count)];
        }
    }
}
