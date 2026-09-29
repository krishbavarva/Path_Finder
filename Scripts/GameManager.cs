using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Networking;

/// <summary>
/// Main game manager: UI, truck, and talking to the Arduino through the PC.
/// Default: <see cref="ServerCommunicator"/> → HTTP to Node on the PC → USB serial → Arduino.
/// Optional: <see cref="ArduinoCommunicator"/> only for Unity Editor and only if Node is not using the same COM port.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("=== REFERENCES (Drag from Hierarchy) ===")]
    [Tooltip("Drag the Truck object here")]
    public TruckController truckController;

    [Tooltip("Required for normal play: Node server on PC forwards to Arduino over USB")]
    public ServerCommunicator serverComm;

    [Tooltip("Optional: direct USB serial from Unity Editor only. Leave empty if Node owns the COM port.")]
    public ArduinoCommunicator arduinoComm;

    [Tooltip("Drag the Start Button here")]
    public Button startButton;

    [Tooltip("Drag the StatusText (TextMeshPro) here")]
    public TextMeshProUGUI statusText;

    [Tooltip("Drag the SensorText (TextMeshPro) here - shows sensor readings")]
    public TextMeshProUGUI sensorText;

    [Header("=== SETTINGS ===")]
    [Tooltip("Seconds to wait at each dustbin before moving on")]
    public float dustbinWaitTime = 4f;

    // --- Private ---
    private bool isCollecting = false;
    private int totalDustbins = 3;

    void Start()
    {
        // Set up button click
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonPressed);
        }

        // Subscribe to truck events
        if (truckController != null)
        {
            truckController.OnDustbinReached += HandleDustbinReached;
            truckController.OnRouteCompleted += HandleRouteCompleted;
            truckController.OnWaypointReached += HandleWaypointReached;
        }

        // Subscribe to server events
        if (serverComm != null)
        {
            serverComm.OnConnected += () => UpdateStatus("Server connected!");
            serverComm.OnDisconnected += () => UpdateStatus("Server disconnected");
        }

        // Subscribe to Arduino events (editor testing)
        if (arduinoComm != null)
        {
            arduinoComm.OnMessageReceived += HandleArduinoMessage;
        }

        if (sensorText != null)
            sensorText.text = "";

        UpdateStatus("Ready! Point camera at the map and press START");
    }

    /// <summary>
    /// Called when the START button is pressed.
    /// </summary>
    void OnStartButtonPressed()
    {
        if (isCollecting)
        {
            Debug.Log("[GameManager] Already collecting, ignoring button press");
            return;
        }

        isCollecting = true;
        startButton.interactable = false; // Disable button during collection

        UpdateStatus("Starting garbage collection route...");

        // Send start signal to Arduino
        SendToArduino("TRUCK_START");

        // Start the truck!
        if (truckController != null)
        {
            truckController.StartRoute();
        }

        UpdateStatus("Truck is moving to Dustbin #1...");
    }

    /// <summary>
    /// Called when truck reaches a dustbin.
    /// </summary>
    void HandleDustbinReached(int dustbinNumber)
    {
        Debug.Log($"[GameManager] Dustbin #{dustbinNumber} reached!");

        UpdateStatus($"Collecting garbage at Dustbin #{dustbinNumber}...");

        // Send signal to Arduino
        SendToArduino($"DUSTBIN{dustbinNumber}_COLLECTED");

        // Wait a few seconds, then resume
        StartCoroutine(WaitAndResume(dustbinNumber));
    }

    /// <summary>
    /// Wait at dustbin, fetch sensor data, then resume truck movement.
    /// </summary>
    IEnumerator WaitAndResume(int dustbinNumber)
    {
        // Fetch sensor data from server
        if (serverComm != null)
        {
            yield return StartCoroutine(FetchSensorData(dustbinNumber));
        }

        yield return new WaitForSeconds(dustbinWaitTime);

        UpdateStatus($"Dustbin #{dustbinNumber} collected! ✓");

        yield return new WaitForSeconds(1f);

        if (dustbinNumber < totalDustbins)
        {
            UpdateStatus($"Moving to Dustbin #{dustbinNumber + 1}...");
        }
        else
        {
            UpdateStatus("All dustbins collected! Returning...");
        }

        // Resume truck movement
        if (truckController != null)
        {
            truckController.ResumeAfterCollection();
        }
    }

    /// <summary>
    /// Fetch sensor readings for a specific dustbin from the API server.
    /// </summary>
    IEnumerator FetchSensorData(int dustbinNumber)
    {
        string url = serverComm.serverURL + $"/api/sensors/{dustbinNumber}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Debug.Log($"[GameManager] Dustbin {dustbinNumber} sensors: {json}");
                DisplaySensorData(dustbinNumber, json);
            }
            else
            {
                Debug.LogWarning($"[GameManager] Could not fetch sensors: {request.error}");
            }
        }
    }

    /// <summary>
    /// Display sensor readings on the UI.
    /// </summary>
    void DisplaySensorData(int dustbinNumber, string json)
    {
        if (sensorText == null) return;

        // Parse simple JSON values (avoiding need for JsonUtility with nested objects)
        string display = "";

        switch (dustbinNumber)
        {
            case 1:
                display = $"--- Dustbin 1 (Solid Waste) ---\n";
                display += $"Trash Level: {ExtractJsonValue(json, "trash")}%\n";
                display += $"Lid: {ExtractJsonValue(json, "lid")}\n";
                display += $"Full: {ExtractJsonValue(json, "full")}";
                break;
            case 2:
                display = $"--- Dustbin 2 (Liquid Waste) ---\n";
                display += $"Water Level: {ExtractJsonValue(json, "water")}%\n";
                display += $"Temperature: {ExtractJsonValue(json, "temp")}°C\n";
                display += $"Overheating: {ExtractJsonValue(json, "hot")}";
                break;
            case 3:
                display = $"--- Dustbin 3 (General Waste) ---\n";
                display += $"Lid: {ExtractJsonValue(json, "lid")}\n";
                display += $"Light Reading: {ExtractJsonValue(json, "light")}";
                break;
        }

        sensorText.text = display;
    }

    /// <summary>
    /// Simple JSON value extractor (works for flat key-value pairs).
    /// </summary>
    string ExtractJsonValue(string json, string key)
    {
        string searchKey = $"\"{key}\":";
        int idx = json.IndexOf(searchKey);
        if (idx < 0) return "N/A";

        int valueStart = idx + searchKey.Length;
        // Skip whitespace
        while (valueStart < json.Length && json[valueStart] == ' ') valueStart++;

        if (valueStart >= json.Length) return "N/A";

        // Check if value is a string (starts with quote)
        if (json[valueStart] == '"')
        {
            int strStart = valueStart + 1;
            int strEnd = json.IndexOf('"', strStart);
            return strEnd > strStart ? json.Substring(strStart, strEnd - strStart) : "N/A";
        }

        // Numeric value - read until comma, }, or end
        int numEnd = valueStart;
        while (numEnd < json.Length && json[numEnd] != ',' && json[numEnd] != '}' && json[numEnd] != ' ')
            numEnd++;
        return json.Substring(valueStart, numEnd - valueStart);
    }

    /// <summary>
    /// Called when the truck finishes the entire route.
    /// </summary>
    void HandleRouteCompleted()
    {
        Debug.Log("[GameManager] Route completed!");

        isCollecting = false;
        startButton.interactable = true;

        UpdateStatus("ALL GARBAGE COLLECTED! Route complete. ✓✓✓");

        if (sensorText != null)
            sensorText.text = "All 3 dustbins serviced!";

        // Send completion signal to Arduino
        SendToArduino("ROUTE_COMPLETE");
    }

    /// <summary>
    /// Called every time truck reaches any waypoint.
    /// </summary>
    void HandleWaypointReached(int current, int total)
    {
        // You can use this for a progress bar if you want
        float progress = (float)current / total * 100f;
        Debug.Log($"[GameManager] Progress: {progress:F0}% (waypoint {current}/{total})");
    }

    /// <summary>
    /// Handle messages received from Arduino.
    /// </summary>
    void HandleArduinoMessage(string message)
    {
        Debug.Log($"[GameManager] Arduino says: {message}");

        // You can react to Arduino messages here
        // For example, if Arduino sends confirmation:
        if (message.Contains("OK"))
        {
            Debug.Log("[GameManager] Arduino confirmed action");
        }
        else if (message.Contains("BIN_FULL"))
        {
            UpdateStatus("WARNING: Dustbin is full!");
        }
    }

    /// <summary>
    /// Send a line to the Arduino: prefer Node on this PC (which uses USB to the board).
    /// </summary>
    void SendToArduino(string message)
    {
        if (serverComm != null)
        {
            serverComm.SendMessage(message);
        }
        else if (arduinoComm != null)
        {
            arduinoComm.SendMessage(message);
        }
        else
        {
            Debug.LogWarning("[GameManager] No communication method available!");
        }
    }

    /// <summary>
    /// Update the on-screen status text.
    /// </summary>
    void UpdateStatus(string message)
    {
        Debug.Log($"[Status] {message}");
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    void OnDestroy()
    {
        if (truckController != null)
        {
            truckController.OnDustbinReached -= HandleDustbinReached;
            truckController.OnRouteCompleted -= HandleRouteCompleted;
            truckController.OnWaypointReached -= HandleWaypointReached;
        }

        if (arduinoComm != null)
        {
            arduinoComm.OnMessageReceived -= HandleArduinoMessage;
        }
    }
}
