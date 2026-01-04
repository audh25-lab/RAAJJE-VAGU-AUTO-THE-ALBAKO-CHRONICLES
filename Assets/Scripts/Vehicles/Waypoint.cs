using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A Waypoint component that defines a point in a path for AI vehicles.
/// It holds a reference to the next possible waypoints, allowing for branching paths.
/// </summary>
public class Waypoint : MonoBehaviour
{
    public VehicleController.VehicleType waypointType; // Land or Boat
    public List<Waypoint> nextWaypoints;
}
