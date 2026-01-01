using UnityEngine;
using System.Collections.Generic;

// This component will be attached to every vehicle prefab.
// It handles the physics-based movement and control of the vehicle.
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Performance")]
    public float motorForce = 2000f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;

    [Header("Wheel Colliders")]
    public List<WheelCollider> frontWheels;
    public List<WheelCollider> rearWheels;

    [Header("Wheel Meshes")]
    public List<Transform> frontWheelMeshes;
    public List<Transform> rearWheelMeshes;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // This method will be called by the player's input script or an AI controller.
    public void Move(float steering, float acceleration, float braking)
    {
        // Apply braking
        float brake = braking * brakeForce;
        foreach (var wheel in frontWheels)
        {
            wheel.brakeTorque = brake;
        }
        foreach (var wheel in rearWheels)
        {
            wheel.brakeTorque = brake;
        }

        // Apply acceleration to rear wheels (RWD)
        float motor = acceleration * motorForce;
        foreach (var wheel in rearWheels)
        {
            wheel.motorTorque = motor;
        }

        // Apply steering to front wheels
        float steer = steering * maxSteerAngle;
        foreach (var wheel in frontWheels)
        {
            wheel.steerAngle = steer;
        }

        UpdateWheelVisuals();
    }

    // This method updates the visual wheel meshes to match the WheelColliders.
    private void UpdateWheelVisuals()
    {
        UpdateWheelVisuals(frontWheels, frontWheelMeshes);
        UpdateWheelVisuals(rearWheels, rearWheelMeshes);
    }

    private void UpdateWheelVisuals(List<WheelCollider> colliders, List<Transform> meshes)
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            if (meshes.Count <= i) continue;

            Quaternion rot;
            Vector3 pos;
            colliders[i].GetWorldPose(out pos, out rot);

            meshes[i].transform.position = pos;
            meshes[i].transform.rotation = rot;
        }
    }
}
