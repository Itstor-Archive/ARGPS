using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UI;

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;
using Unity.XR.CoreUtils;
using UnityEngine.Networking;
using Google.XR.ARCoreExtensions.GeospatialCreator.Internal;
using EasyUI.Toast;
using Unity.VisualScripting;
using System.Collections.Generic;





#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using TMPro;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines",
    Justification = "Bypass source check.")]
public class GeospatialController : MonoBehaviour
{
    [Header("AR Components")]
    public XROrigin SessionOrigin;
    public ARSession Session;
    public AREarthManager EarthManager;
    public ARCoreExtensions ARCoreExtensions;
    public ARAnchorManager AnchorManager;

    [Header("UI Elements")]
    public GameObject ArrowPrefab;
    public GameObject OnboardingCanvas;
    public GameObject ARViewCanvas;
    public TMP_Dropdown LocationDropdown;
    public Text DebugText;

    private const float _timeoutSeconds = 180;
    private const double _orientationYawAccuracyThreshold = 25;
    private const double _horizontalAccuracyThreshold = 20;

    private bool _waitingForLocationService = false;
    private bool _isInARView = false;
    private bool _isReturning = false;
    private bool _isLocalizing = false;
    private bool _enablingGeospatial = false;
    private float _localizationPassedTime = 0f;
    private float _configurePrepareTime = 3f;
    private IEnumerator _startLocationService = null;
    private IEnumerator _asyncCheck = null;

    [System.Serializable]
    private class Node
    {
        public int id;
        public string places_name;
        public double latitude;
        public double longitude;
        public int total_nodes;
    }

    [System.Serializable]
    private class NodeList
    {
        public Node[] nodes;
    }

    [System.Serializable]
    private class Error
    {
        public string error;
    }

    public void OnStartClicked()
    {
        var destinationId = LocationDropdown.GetComponent<DropdownPopulator>().GetSelectedPlaceId();

        Debug.Log("Selected location: " + destinationId);

        if (destinationId == -1)
        {
            Debug.LogError("Invalid dropdown index!");
            Toast.Show("Pilih lokasi terlebih dahulu");
            return;
        }

        Debug.Log("Start AR");

        if (EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            Debug.LogError("Earth is not tracking!");
            Toast.Show("Geospatial belum siap");
            return;
        }

        var location = Input.location.lastData;

        // var latitude = location.latitude;
        // var longitude = location.longitude;

        var latitude = -7.28454511607389;
        var longitude = 112.79530739674819;

        string apiURL = "https://backend-protel-nasdem.vercel.app/api/route"; // replace with your API endpoint
        apiURL += $"?latitude={latitude}&longitude={longitude}&endId={destinationId}";

        StartCoroutine(CallAPI(apiURL));
    }

    private IEnumerator CallAPI(string url)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            // if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            if (false)
            {
                var jsonResponse = request.downloadHandler.text;
                var error = JsonUtility.FromJson<Error>(jsonResponse);
                Toast.Show(error.error);
                Debug.LogError($"Error calling API: {request.error}");
            }
            else
            {
                var jsonResponse = request.downloadHandler.text;
                // var nodes = JsonUtility.FromJson<NodeList>(jsonResponse).nodes;

                List<Node> nodes = new List<Node>
                {
                    new Node
                    {
                        id = 1,
                        places_name = "Lokasi Anda",
                        latitude = -7.286942608847051,
                        longitude = 112.79832048186695,
                        total_nodes = 0
                    },
                    new Node
                    {
                        id = 2,
                        places_name = "Lokasi 1",
                        latitude = -7.286826875095186,
                        longitude = 112.79835669169225,
                        total_nodes = 0
                    },
                    new Node
                    {
                        id = 3,
                        places_name = "Lokasi 2",
                        latitude = -7.286786966875544,
                        longitude = 112.79826147329109,
                        total_nodes = 0
                    },
                    new Node
                    {
                        id = 4,
                        places_name = "Lokasi 3",
                        latitude = -7.286840177396419,
                        longitude = 112.79781220297657,
                        total_nodes = 0
                    },
                };

                GameObject previousArrow = null;
                Vector3? previousPosition = null;

                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];

                    ARGeospatialAnchor anchor = AnchorManager.AddAnchor(node.latitude, node.longitude, 0.5f, Quaternion.identity);

                    if (anchor == null)
                    {
                        Debug.LogError("Failed to create anchor!");
                        Toast.Show("Gagal membuat anchor");
                        continue;
                    }

                    GameObject arrowInstance = Instantiate(ArrowPrefab);

                    anchor.gameObject.SetActive(true);
                    arrowInstance.transform.SetParent(anchor.transform, false);

                    if (previousPosition.HasValue)
                    {
                        Vector3 directionToNext = anchor.transform.position - previousPosition.Value;
                        float angle = Mathf.Atan2(directionToNext.z, directionToNext.x) * Mathf.Rad2Deg;
                        previousArrow.transform.rotation = Quaternion.Euler(0, -angle, 0);
                    }

                    if (i == nodes.Count - 1)
                    {
                        arrowInstance.transform.rotation = Quaternion.Euler(0, 0, 90);
                    }

                    previousArrow = arrowInstance;
                    previousPosition = anchor.transform.position;
                }

            }

            ShowOnboarding(false);
        }
    }

    public void Awake()
    {
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.Portrait;

        Application.targetFrameRate = 60;

        if (SessionOrigin == null)
        {
            Debug.LogError("Cannot find ARSessionOrigin.");
        }

        if (Session == null)
        {
            Debug.LogError("Cannot find ARSession.");
        }

        if (ARCoreExtensions == null)
        {
            Debug.LogError("Cannot find ARCoreExtensions.");
        }
    }

    public void OnEnable()
    {
        _startLocationService = StartLocationService();
        StartCoroutine(_startLocationService);

        _isReturning = false;
        _enablingGeospatial = false;
        DebugText.gameObject.SetActive(Debug.isDebugBuild && EarthManager != null);

        _localizationPassedTime = 0f;
        _isLocalizing = true;
    }

    public void Start()
    {
        StartAR(true);
    }

    public void OnDisable()
    {
        StopCoroutine(_asyncCheck);
        _asyncCheck = null;
        StopCoroutine(_startLocationService);
        _startLocationService = null;
        Debug.Log("Stop location services.");
        Input.location.Stop();
    }

    public void Update()
    {
        UpdateDebugInfo();

        // Check session error status.
        LifecycleUpdate();
        if (_isReturning)
        {
            return;
        }

        if (ARSession.state != ARSessionState.SessionInitializing &&
            ARSession.state != ARSessionState.SessionTracking)
        {
            return;
        }

        // Check feature support and enable Geospatial API when it's supported.
        var featureSupport = EarthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
        switch (featureSupport)
        {
            case FeatureSupported.Unknown:
                return;
            case FeatureSupported.Unsupported:
                Toast.Show("Geospatial tidak didukung");
                return;
            case FeatureSupported.Supported:
                if (ARCoreExtensions.ARCoreExtensionsConfig.GeospatialMode ==
                    GeospatialMode.Disabled)
                {
                    Debug.Log("Geospatial switched to GeospatialMode.Enabled.");
                    ARCoreExtensions.ARCoreExtensionsConfig.GeospatialMode =
                        GeospatialMode.Enabled;
                    _configurePrepareTime = 3.0f;
                    _enablingGeospatial = true;
                    return;
                }

                break;
        }

        if (_enablingGeospatial)
        {
            _configurePrepareTime -= Time.deltaTime;
            if (_configurePrepareTime < 0)
            {
                _enablingGeospatial = false;
            }
            else
            {
                return;
            }
        }

        // Check earth state.
        var earthState = EarthManager.EarthState;
        if (earthState == EarthState.ErrorEarthNotReady)
        {
            Debug.LogWarning("Inisialisasi Geospatial");
            Toast.Show("Inisialisasi Geospatial");
            return;
        }
        else if (earthState != EarthState.Enabled)
        {
            string errorMessage =
                "Geospatial mengalami error: " + earthState;
            Debug.LogWarning(errorMessage);
            Toast.Show(errorMessage);
            return;
        }

        // Check earth localization.
        bool isSessionReady = ARSession.state == ARSessionState.SessionTracking &&
            Input.location.status == LocationServiceStatus.Running;
        var earthTrackingState = EarthManager.EarthTrackingState;
        var pose = earthTrackingState == TrackingState.Tracking ?
            EarthManager.CameraGeospatialPose : new GeospatialPose();
        if (!isSessionReady || earthTrackingState != TrackingState.Tracking ||
            pose.OrientationYawAccuracy > _orientationYawAccuracyThreshold ||
            pose.HorizontalAccuracy > _horizontalAccuracyThreshold)
        {
            // Lost localization during the session.
            if (!_isLocalizing)
            {
                _isLocalizing = true;
                _localizationPassedTime = 0f;
            }

            if (_localizationPassedTime > _timeoutSeconds)
            {
                Debug.LogError("Geospatial localization timed out.");
                Toast.Show("Geospatial localization timed out.");
            }
            else
            {
                _localizationPassedTime += Time.deltaTime;
            }
        }
        else if (_isLocalizing)
        {
            // Finished localization.
            _isLocalizing = false;
            _localizationPassedTime = 0f;
            Toast.Show("Lokalisasi Geospatial berhasil");
        }
    }

    private void StartAR(bool enable)
    {
        SessionOrigin.gameObject.SetActive(enable);
        Session.gameObject.SetActive(enable);
        ARCoreExtensions.gameObject.SetActive(enable);
        if (enable && _asyncCheck == null)
        {
            _asyncCheck = AvailabilityCheck();
            StartCoroutine(_asyncCheck);
        }
    }

    private void ShowOnboarding(bool enable)
    {
        OnboardingCanvas.SetActive(enable);
    }

    private IEnumerator AvailabilityCheck()
    {
        if (ARSession.state == ARSessionState.None)
        {
            yield return ARSession.CheckAvailability();
        }

        // Waiting for ARSessionState.CheckingAvailability.
        yield return null;

        if (ARSession.state == ARSessionState.NeedsInstall)
        {
            yield return ARSession.Install();
        }

        // Waiting for ARSessionState.Installing.
        yield return null;
#if UNITY_ANDROID

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("Requesting camera permission.");
            Permission.RequestUserPermission(Permission.Camera);
            yield return new WaitForSeconds(3.0f);
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            // User has denied the request.
            Debug.LogWarning(
                "Failed to get the camera permission. VPS availability check isn't available.");
            yield break;
        }
#endif

        while (_waitingForLocationService)
        {
            yield return null;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogWarning(
                "Location services aren't running. VPS availability check is not available.");
            yield break;
        }

        // Update event is executed before coroutines so it checks the latest error states.
        if (_isReturning)
        {
            yield break;
        }

        var location = Input.location.lastData;
        var vpsAvailabilityPromise =
            AREarthManager.CheckVpsAvailabilityAsync(location.latitude, location.longitude);
        yield return vpsAvailabilityPromise;

        Debug.LogFormat("VPS Availability at ({0}, {1}): {2}",
            location.latitude, location.longitude, vpsAvailabilityPromise.Result);
    }

    private IEnumerator StartLocationService()
    {
        _waitingForLocationService = true;
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.Log("Requesting the fine location permission.");
            Permission.RequestUserPermission(Permission.FineLocation);
            yield return new WaitForSeconds(3.0f);
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("Location service is disabled by the user.");
            _waitingForLocationService = false;
            yield break;
        }

        Debug.Log("Starting location service.");
        Input.location.Start();

        while (Input.location.status == LocationServiceStatus.Initializing)
        {
            yield return null;
        }

        _waitingForLocationService = false;
        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogWarningFormat(
                "Location service ended with {0} status.", Input.location.status);
            Input.location.Stop();
        }
    }

    private void LifecycleUpdate()
    {
        // Pressing 'back' button quits the app.
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Application.Quit();
        }

        if (_isReturning)
        {
            return;
        }

        // Only allow the screen to sleep when not tracking.
        var sleepTimeout = SleepTimeout.NeverSleep;
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            sleepTimeout = SleepTimeout.SystemSetting;
        }

        Screen.sleepTimeout = sleepTimeout;
    }

    private void UpdateDebugInfo()
    {
        if (!Debug.isDebugBuild || EarthManager == null)
        {
            return;
        }

        var pose = EarthManager.EarthState == EarthState.Enabled &&
            EarthManager.EarthTrackingState == TrackingState.Tracking ?
            EarthManager.CameraGeospatialPose : new GeospatialPose();
        var supported = EarthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
        DebugText.text =
            $"IsReturning: {_isReturning}\n" +
            $"IsLocalizing: {_isLocalizing}\n" +
            $"SessionState: {ARSession.state}\n" +
            $"LocationServiceStatus: {Input.location.status}\n" +
            $"FeatureSupported: {supported}\n" +
            $"EarthState: {EarthManager.EarthState}\n" +
            $"EarthTrackingState: {EarthManager.EarthTrackingState}\n" +
            $"  LAT/LNG: {pose.Latitude:F6}, {pose.Longitude:F6}\n" +
            $"  HorizontalAcc: {pose.HorizontalAccuracy:F6}\n" +
            $"  ALT: {pose.Altitude:F2}\n" +
            $"  VerticalAcc: {pose.VerticalAccuracy:F2}\n" +
            $". EunRotation: {pose.EunRotation:F2}\n" +
            $"  OrientationYawAcc: {pose.OrientationYawAccuracy:F2}" +
            $"  Reason: {ARSession.notTrackingReason}\n";
    }
}
