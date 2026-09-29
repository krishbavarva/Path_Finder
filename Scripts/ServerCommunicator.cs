using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

/// <summary>
/// Talks to the Node server on your PC. The PC bridges HTTP → USB serial → Arduino (no Wi‑Fi module on Arduino).
/// Unity in Editor on the same PC: use http://127.0.0.1:3000 (or localhost).
/// Unity on a phone: use your PC's LAN IP (same Wi‑Fi as the phone); Arduino stays on USB to the PC only.
/// Android http:// builds may need cleartext allowed (Player Settings → Android).
/// </summary>
public class ServerCommunicator : MonoBehaviour
{
    [Header("=== PC SERVER (Node bridges USB Arduino here) ===")]
    [Tooltip("Editor on this PC: http://127.0.0.1:3000 — Phone build: PC LAN IP from server console. No trailing slash")]
    public string serverURL = "http://192.168.0.10:3000";

    [Tooltip("Ping /api/status on play and optionally on an interval")]
    public bool connectOnStart = true;

    [Tooltip("0 = only check once at start; otherwise poll for reachability every N seconds")]
    public float statusPollInterval = 15f;

    public event Action OnConnected;
    public event Action OnDisconnected;

    [Serializable]
    private class CommandBody
    {
        public string command;
    }

    private bool wasReachable;
    private Coroutine pollCoroutine;

    void Start()
    {
        if (connectOnStart)
        {
            StartCoroutine(CheckStatusOnceThenMaybePoll());
        }
    }

    IEnumerator CheckStatusOnceThenMaybePoll()
    {
        yield return CheckStatusCoroutine();
        if (statusPollInterval > 0f)
        {
            pollCoroutine = StartCoroutine(StatusPollLoop());
        }
    }

    IEnumerator StatusPollLoop()
    {
        var wait = new WaitForSeconds(statusPollInterval);
        while (true)
        {
            yield return wait;
            yield return CheckStatusCoroutine();
        }
    }

    IEnumerator CheckStatusCoroutine()
    {
        string url = BaseUrl() + "/api/status";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            bool ok = request.result == UnityWebRequest.Result.Success;

            if (ok && !wasReachable)
            {
                wasReachable = true;
                OnConnected?.Invoke();
            }
            else if (!ok && wasReachable)
            {
                wasReachable = false;
                OnDisconnected?.Invoke();
            }
        }
    }

    /// <summary>
    /// Forwards a line to the PC server, which writes it to Arduino (same strings as ArduinoCommunicator).
    /// </summary>
    public void SendMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        StartCoroutine(PostCommandCoroutine(message));
    }

    IEnumerator PostCommandCoroutine(string message)
    {
        string url = BaseUrl() + "/api/command";
        string json = JsonUtility.ToJson(new CommandBody { command = message });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Server] POST /api/command failed: {request.error} (is serverURL correct?)");
            }
        }
    }

    string BaseUrl()
    {
        string u = serverURL == null ? "" : serverURL.Trim();
        if (u.EndsWith("/")) u = u.Substring(0, u.Length - 1);
        return u;
    }

    void OnDestroy()
    {
        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
        }
    }
}
