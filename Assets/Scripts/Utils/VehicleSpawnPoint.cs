using UnityEngine;
using RVA.TAC.Vehicles;

namespace RVA.TAC.Utils
{
    /// <summary>
    /// Defines a point in the world where vehicles can be spawned.
    /// </summary>
    public class VehicleSpawnPoint : MonoBehaviour
    {
        public VehicleController.VehicleType spawnType; // Land or Boat
        public bool isOccupied = false;
        public GameObject currentVehicle;

        public void SetOccupied(GameObject vehicle)
        {
            isOccupied = true;
            currentVehicle = vehicle;
        }

        public void SetUnoccupied()
        {
            isOccupied = false;
            currentVehicle = null;
        }
    }
}
