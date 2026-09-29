using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

/// <summary>
/// Direct USB serial from Unity to Arduino (Unity opens the COM port).
/// Typical setup: leave this unassigned and use <see cref="ServerCommunicator"/> instead so only Node
/// opens the USB port (Arduino cable to PC, Node bridges to Unity over HTTP).
/// Use this only in the Editor on the same PC, and stop the Node server first to avoid COM conflicts.
/// </summary>
public class ArduinoCommunicator : MonoBehaviour
{
    [Header("=== SERIAL PORT SETTINGS ===")]
    [Tooltip("COM port of your Arduino. Check Device Manager → Ports (COM & LPT)")]
    public string portName = "COM3";

    [Tooltip("Must match your Arduino Serial.begin() baud rate")]
    public int baudRate = 9600;

    [Tooltip("Try to connect to Arduino on startup?")]
    public bool connectOnStart = true;

    // --- Events ---
    public event Action<string> OnMessageReceived;  // When Arduino sends a message back
    public event Action OnConnected;
    public event Action OnDisconnected;

    // --- Private ---
    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;
    private string lastReceivedMessage = "";
    private bool hasNewMessage = false;
    private bool isConnected = false;

    public bool IsConnected => isConnected;

    void Start()
    {
        if (connectOnStart)
        {
            ConnectToArduino();
        }
    }

    /// <summary>
    /// Connect to the Arduino via serial port.
    /// </summary>
    public void ConnectToArduino()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 100;
            serialPort.WriteTimeout = 100;
            serialPort.DtrEnable = true; // Some Arduinos need this
            serialPort.Open();

            isRunning = true;
            isConnected = true;

            // Start a background thread to read messages from Arduino
            readThread = new Thread(ReadFromArduino);
            readThread.IsBackground = true;
            readThread.Start();

            Debug.Log($"[Arduino] Connected on {portName} at {baudRate} baud");
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Arduino] Could not connect on {portName}: {e.Message}");
            Debug.LogWarning("[Arduino] Check: Is Arduino connected? Is the COM port correct? Is Serial Monitor closed?");
            isConnected = false;
        }
    }

    /// <summary>
    /// Send a message to Arduino.
    /// </summary>
    public void SendMessage(string message)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.WriteLine(message);
                Debug.Log($"[Arduino] Sent: {message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Arduino] Send failed: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[Arduino] Not connected. Message not sent: {message}");
        }
    }

    /// <summary>
    /// Send a signal that a specific dustbin was collected.
    /// Your Arduino code should listen for these messages.
    /// </summary>
    public void SendDustbinCollected(int dustbinNumber)
    {
        string message = $"DUSTBIN{dustbinNumber}_COLLECTED";
        SendMessage(message);
    }

    /// <summary>
    /// Send a signal that the entire route is complete.
    /// </summary>
    public void SendRouteComplete()
    {
        SendMessage("ROUTE_COMPLETE");
    }

    /// <summary>
    /// Send a start signal.
    /// </summary>
    public void SendStartSignal()
    {
        SendMessage("TRUCK_START");
    }

    // Background thread: continuously reads from Arduino
    private void ReadFromArduino()
    {
        while (isRunning)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    string message = serialPort.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        // Store message to process on main thread (Unity requires this)
                        lastReceivedMessage = message;
                        hasNewMessage = true;
                        Debug.Log($"[Arduino] Received: {message}");
                    }
                }
            }
            catch (TimeoutException)
            {
                // Normal - no data available, just continue
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"[Arduino] Read error: {e.Message}");
            }

            Thread.Sleep(10); // Small delay to prevent CPU overload
        }
    }

    // Process received messages on the main Unity thread
    void Update()
    {
        if (hasNewMessage)
        {
            hasNewMessage = false;
            OnMessageReceived?.Invoke(lastReceivedMessage);
        }
    }

    // Clean up when the app closes
    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    public void Disconnect()
    {
        isRunning = false;
        isConnected = false;

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(500); // Wait up to 500ms for thread to finish
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Close();
                Debug.Log("[Arduino] Disconnected");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Arduino] Error closing port: {e.Message}");
            }
        }

        OnDisconnected?.Invoke();
    }
}
