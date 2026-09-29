using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the garbage truck movement along waypoints on the AR map.
/// Attach this script to your Truck GameObject.
/// </summary>
public class TruckController : MonoBehaviour
{
    [Header("=== WAYPOINT SETTINGS ===")]
    [Tooltip("Drag ALL waypoints here IN ORDER. The truck follows this path.")]
    public Transform[] waypoints;

    [Tooltip("Which waypoint indices are dustbin stops? (0-based index). E.g., if dustbin is the 5th waypoint, enter 4")]
    public int[] dustbinWaypointIndices; // e.g., {4, 9, 14}

    [Header("=== MOVEMENT SETTINGS ===")]
    [Tooltip("How fast the truck moves. Start with 0.02, adjust as needed.")]
    public float moveSpeed = 0.02f;

    [Tooltip("How fast the truck turns. 5 is a good default.")]
    public float rotationSpeed = 5f;

    [Tooltip("How close the truck needs to be to a waypoint before moving to the next one.")]
    public float waypointReachDistance = 0.005f;

    [Header("=== DUSTBIN OBJECTS (Optional) ===")]
    [Tooltip("Drag the 3 dustbin GameObjects here for visual feedback")]
    public GameObject[] dustbinObjects;

    // --- Events that GameManager listens to ---
    public event Action<int> OnDustbinReached;      // Fired when truck reaches a dustbin (passes dustbin number 1,2,3)
    public event Action OnRouteCompleted;            // Fired when truck finishes entire route
    public event Action<int, int> OnWaypointReached; // Fired every waypoint (current, total)

    // --- Private variables ---
    private int currentWaypointIndex = 0;
    private bool isMoving = false;
    private bool isPaused = false;
    private int dustbinsCollected = 0;
    private HashSet<int> dustbinIndicesSet = new HashSet<int>();

    void Start()
    {
        // Convert dustbin indices array to a HashSet for fast lookup
        foreach (int index in dustbinWaypointIndices)
        {
            dustbinIndicesSet.Add(index);
        }
    }

    void Update()
    {
        if (!isMoving || isPaused) return;
        if (waypoints == null || waypoints.Length == 0) return;
        if (currentWaypointIndex >= waypoints.Length) return;

        // Get target waypoint position
        Transform target = waypoints[currentWaypointIndex];
        if (target == null) return;

        // Move towards the waypoint
        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        // Rotate truck to face movement direction
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Move forward
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            moveSpeed * Time.deltaTime
        );

        // Check if reached waypoint
        if (distance < waypointReachDistance)
        {
            OnWaypointReached?.Invoke(currentWaypointIndex, waypoints.Length);

            // Check if this waypoint is a dustbin stop
            if (dustbinIndicesSet.Contains(currentWaypointIndex))
            {
                dustbinsCollected++;
                Debug.Log($"[TruckController] Reached Dustbin #{dustbinsCollected} at waypoint {currentWaypointIndex}");

                // Pause truck at dustbin
                isPaused = true;

                // Fire the dustbin reached event
                OnDustbinReached?.Invoke(dustbinsCollected);

                // Visual feedback - change dustbin color
                if (dustbinObjects != null && dustbinsCollected - 1 < dustbinObjects.Length)
                {
                    ChangeDustbinColor(dustbinObjects[dustbinsCollected - 1], Color.green);
                }

                return; // Stay paused until ResumeAfterCollection() is called
            }

            // Move to next waypoint
            currentWaypointIndex++;

            // Check if route is complete
            if (currentWaypointIndex >= waypoints.Length)
            {
                isMoving = false;
                Debug.Log("[TruckController] Route completed!");
                OnRouteCompleted?.Invoke();
            }
        }
    }

    /// <summary>
    /// Call this to start the truck moving from the beginning.
    /// </summary>
    public void StartRoute()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError("[TruckController] No waypoints assigned! Drag waypoints into the Inspector.");
            return;
        }

        currentWaypointIndex = 0;
        dustbinsCollected = 0;
        isMoving = true;
        isPaused = false;

        // Move truck to starting position
        transform.position = waypoints[0].position;

        Debug.Log("[TruckController] Route started! Moving to first waypoint...");
    }

    /// <summary>
    /// Call this after garbage collection is confirmed at a dustbin.
    /// The truck will resume moving to the next waypoint.
    /// </summary>
    public void ResumeAfterCollection()
    {
        Debug.Log($"[TruckController] Resuming after dustbin #{dustbinsCollected}");
        isPaused = false;
        currentWaypointIndex++;

        // Check if that was the last waypoint
        if (currentWaypointIndex >= waypoints.Length)
        {
            isMoving = false;
            Debug.Log("[TruckController] Route completed!");
            OnRouteCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Stop the truck immediately.
    /// </summary>
    public void StopTruck()
    {
        isMoving = false;
        isPaused = false;
    }

    /// <summary>
    /// Reset truck to starting position.
    /// </summary>
    public void ResetTruck()
    {
        StopTruck();
        currentWaypointIndex = 0;
        dustbinsCollected = 0;

        if (waypoints != null && waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
        }

        // Reset dustbin colors
        if (dustbinObjects != null)
        {
            foreach (var dustbin in dustbinObjects)
            {
                if (dustbin != null)
                    ChangeDustbinColor(dustbin, new Color(0.4f, 0.4f, 0.4f)); // Gray
            }
        }
    }

    // Helper to change dustbin color for visual feedback
    private void ChangeDustbinColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    // Draw path in Scene view for easier editing
    void OnDrawGizmos()
    {
        if (waypoints == null) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            // Draw dustbin waypoints in red, others in yellow
            if (dustbinWaypointIndices != null && Array.IndexOf(dustbinWaypointIndices, i) >= 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(waypoints[i].position, 0.008f);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(waypoints[i].position, 0.004f);
            }

            // Draw line to next waypoint
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}
