using UnityEngine;
using System;

/// <summary>
/// Bluetooth communication manager for Android phone ↔ Arduino (HC-05/HC-06).
/// Use this instead of ArduinoCommunicator when building for phone.
/// 
/// HOW TO USE:
/// 1. Pair your phone with HC-05 Bluetooth module first (Settings → Bluetooth)
/// 2. The default PIN for HC-05 is "1234" or "0000"
/// 3. Set the deviceName to "HC-05" or whatever your module is named
/// 4. Attach this script to a GameObject in your scene
/// </summary>
public class BluetoothManager : MonoBehaviour
{
    [Header("=== BLUETOOTH SETTINGS ===")]
    [Tooltip("Name of the Bluetooth device to connect to (e.g., HC-05)")]
    public string deviceName = "HC-05";

    [Tooltip("Try to connect on startup?")]
    public bool connectOnStart = true;

    // --- Events ---
    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    // --- Private ---
    private bool isConnected = false;
    public bool IsConnected => isConnected;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject bluetoothAdapter;
    private AndroidJavaObject bluetoothSocket;
    private AndroidJavaObject outputStream;
    private AndroidJavaObject inputStream;
#endif

    void Start()
    {
        if (connectOnStart)
        {
            ConnectBluetooth();
        }
    }

    /// <summary>
    /// Connect to the Bluetooth device (HC-05 module on Arduino).
    /// </summary>
    public void ConnectBluetooth()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Get the Bluetooth adapter
            AndroidJavaClass bluetoothAdapterClass = new AndroidJavaClass("android.bluetooth.BluetoothAdapter");
            bluetoothAdapter = bluetoothAdapterClass.CallStatic<AndroidJavaObject>("getDefaultAdapter");

            if (bluetoothAdapter == null)
            {
                Debug.LogError("[Bluetooth] Device does not support Bluetooth!");
                return;
            }

            if (!bluetoothAdapter.Call<bool>("isEnabled"))
            {
                Debug.LogError("[Bluetooth] Bluetooth is not enabled! Turn it on in Settings.");
                return;
            }

            // Get paired devices
            AndroidJavaObject pairedDevices = bluetoothAdapter.Call<AndroidJavaObject>("getBondedDevices");
            AndroidJavaObject[] devices = pairedDevices.Call<AndroidJavaObject[]>("toArray");

            AndroidJavaObject targetDevice = null;
            foreach (var device in devices)
            {
                string name = device.Call<string>("getName");
                Debug.Log($"[Bluetooth] Found paired device: {name}");
                if (name == deviceName)
                {
                    targetDevice = device;
                    break;
                }
            }

            if (targetDevice == null)
            {
                Debug.LogError($"[Bluetooth] Device '{deviceName}' not found! Make sure it's paired in phone Settings.");
                return;
            }

            // Connect using SPP UUID (standard for serial Bluetooth)
            AndroidJavaClass uuidClass = new AndroidJavaClass("java.util.UUID");
            AndroidJavaObject uuid = uuidClass.CallStatic<AndroidJavaObject>("fromString", "00001101-0000-1000-8000-00805F9B34FB");

            bluetoothSocket = targetDevice.Call<AndroidJavaObject>("createRfcommSocketToServiceRecord", uuid);
            bluetoothAdapter.Call("cancelDiscovery"); // Important: cancel discovery before connecting
            bluetoothSocket.Call("connect");

            outputStream = bluetoothSocket.Call<AndroidJavaObject>("getOutputStream");
            inputStream = bluetoothSocket.Call<AndroidJavaObject>("getInputStream");

            isConnected = true;
            Debug.Log($"[Bluetooth] Connected to {deviceName}!");
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bluetooth] Connection failed: {e.Message}");
            isConnected = false;
        }
#else
        Debug.LogWarning("[Bluetooth] Bluetooth only works on Android device, not in Unity Editor. Use ArduinoCommunicator for Editor testing.");
#endif
    }

    /// <summary>
    /// Send a message to Arduino via Bluetooth.
    /// </summary>
    public void SendMessage(string message)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isConnected || outputStream == null)
        {
            Debug.LogWarning("[Bluetooth] Not connected! Message not sent: " + message);
            return;
        }

        try
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(message + "\n");
            
            // Convert to Java byte array
            AndroidJavaObject byteArray = new AndroidJavaObject("java.lang.String", message + "\n");
            byte[] javaBytes = System.Text.Encoding.UTF8.GetBytes(message + "\n");
            
            outputStream.Call("write", javaBytes);
            outputStream.Call("flush");
            
            Debug.Log($"[Bluetooth] Sent: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bluetooth] Send failed: {e.Message}");
        }
#else
        Debug.Log($"[Bluetooth] (Editor Mode) Would send: {message}");
#endif
    }

    /// <summary>
    /// Send dustbin collection signal via Bluetooth.
    /// </summary>
    public void SendDustbinCollected(int dustbinNumber)
    {
        SendMessage($"DUSTBIN{dustbinNumber}_COLLECTED");
    }

    /// <summary>
    /// Disconnect Bluetooth.
    /// </summary>
    public void Disconnect()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            isConnected = false;
            if (inputStream != null) inputStream.Call("close");
            if (outputStream != null) outputStream.Call("close");
            if (bluetoothSocket != null) bluetoothSocket.Call("close");
            Debug.Log("[Bluetooth] Disconnected");
            OnDisconnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bluetooth] Disconnect error: {e.Message}");
        }
#endif
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }
}
