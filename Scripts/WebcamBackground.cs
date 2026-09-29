using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the iPhone wireless webcam (EpocCam/iVCam) as the background.
/// Attach this to a UI RawImage that fills the entire screen.
/// </summary>
public class WebcamBackground : MonoBehaviour
{
    [Header("=== CAMERA SETTINGS ===")]
    [Tooltip("Leave empty to auto-detect. Or type the exact camera name from the Console log.")]
    public string preferredCameraName = "";

    private WebCamTexture webcamTexture;
    private RawImage rawImage;

    void Start()
    {
        rawImage = GetComponent<RawImage>();

        // List ALL cameras so user can see the names in Console
        WebCamDevice[] devices = WebCamTexture.devices;
        Debug.Log($"[Webcam] ========== FOUND {devices.Length} CAMERAS ==========");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"[Webcam] Camera {i}: \"{devices[i].name}\"");
        }
        Debug.Log("[Webcam] ============================================");

        string cameraName = "";

        // If user set a preferred camera name in Inspector, use that
        if (!string.IsNullOrEmpty(preferredCameraName))
        {
            cameraName = preferredCameraName;
            Debug.Log($"[Webcam] Using preferred camera: {cameraName}");
        }
        else
        {
            // Auto-detect: prefer phone webcam apps
            for (int i = 0; i < devices.Length; i++)
            {
                string nameLower = devices[i].name.ToLower();
                if (nameLower.Contains("camo") ||
                    nameLower.Contains("epoccam") ||
                    nameLower.Contains("ivcam") ||
                    nameLower.Contains("droidcam") ||
                    nameLower.Contains("reincubate") ||
                    nameLower.Contains("virtual"))
                {
                    cameraName = devices[i].name;
                }
            }

            // Fallback to first camera
            if (string.IsNullOrEmpty(cameraName) && devices.Length > 0)
            {
                cameraName = devices[0].name;
            }
        }

        if (!string.IsNullOrEmpty(cameraName))
        {
            webcamTexture = new WebCamTexture(cameraName, 1280, 720, 30);
            rawImage.texture = webcamTexture;
            webcamTexture.Play();
            Debug.Log($"[Webcam] Using camera: {cameraName}");
        }
        else
        {
            Debug.LogWarning("[Webcam] No camera found!");
        }
    }

    void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}
