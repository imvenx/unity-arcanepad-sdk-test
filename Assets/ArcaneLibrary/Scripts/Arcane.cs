using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ArcanepadSDK;
using ArcanepadSDK.Models;
using UnityEditor;
using UnityEngine;

public class Arcane : MonoBehaviour
{
    public static WebSocketService<string> Msg;
    public static IList<ArcaneDevice> Devices = new List<ArcaneDevice>();
    public static List<ArcanePad> Pads = new List<ArcanePad>();
    private static List<string> InternalViewsIds = new List<string>();
    private static List<string> InternalPadsIds = new List<string>();
    public static List<string> IframeViewsIds = new List<string>();
    public static List<string> IframePadsIds = new List<string>();
    public static ArcanePad Pad { get; private set; }
    public string LibraryVersion { get; } = "1.0.0";
    [DllImport("__Internal")]
    private static extern void SetFullScreen();

    [SerializeField]
    private ArcaneDeviceTypeEnum DeviceType;
    [SerializeField]
    private string Port = "3005";
    [SerializeField]
    private string ReverseProxyPort = "3009";
    [SerializeField]
    private string ArcaneCode = "";

    private static TaskCompletionSource<InitialState> _arcaneClientInitialized = new TaskCompletionSource<InitialState>();
    // private static TaskCompletionSource<ArcaneClientInitializeEvent> _ArcaneClientInitialized = new TaskCompletionSource<ArcaneClientInitializeEvent>();

    public static Task<InitialState> ArcaneClientInitialized()
    {
        return _arcaneClientInitialized.Task;
    }

    void Awake()
    {
#if !UNITY_EDITOR && UNITY_WEBGL
    SetFullScreen();
#endif

        DontDestroyOnLoad(this);

        // string url = "wss://localhost:3005";
        // #if DEBUG || UNITY_EDITOR || UNITY_STANDALONE
        // url = "ws://localhost:3009";
        // #endif

        var deviceType = DeviceType == ArcaneDeviceTypeEnum.view ? ArcaneDeviceType.view : ArcaneDeviceType.pad;
        var arcaneInitParams = new ArcaneInitParams(deviceType, Port, ReverseProxyPort, ArcaneCode);

        Msg = new WebSocketService<string>(arcaneInitParams);

        Action unsubscribeInit = null;
        unsubscribeInit = Msg.On(AEventName.Initialize, (InitializeEvent e, string from) => Initialize(e, unsubscribeInit));

        Msg.On(AEventName.RefreshGlobalState, (RefreshGlobalStateEvent e) => RefreshGlobalState(e.refreshedGlobalState));
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE 
        if (Msg != null && Msg.Ws != null)
        {
            Msg.Ws.DispatchMessageQueue();
        }
#endif
    }

    void OnDestroy()
    {
        Msg.Ws.Close();
    }

    void OnDisable()
    {
        Msg.Ws.Close();
    }

    private void Initialize(InitializeEvent e, Action unsubscribeInit)
    {
        unsubscribeInit();

        RefreshGlobalState(e.globalState);

        // if (DeviceType == ArcaneDeviceTypeEnum.pad)
        // {
        Pad = Pads.FirstOrDefault(p => p.DeviceId == Msg.DeviceId);
        // }

        var initialState = new InitialState(Pads);

        _arcaneClientInitialized.SetResult(initialState);

        // Msg.OnInitialize(e);
    }

    private void RefreshGlobalState(GlobalState globalState)
    {
        Devices = globalState.devices;

        RefreshClientsIds(Devices);

        Pads = GetPads(Devices);
    }

    public List<ArcanePad> GetPads(IList<ArcaneDevice> _devices)
    {
        var pads = new List<ArcanePad>();

        var padDevices = _devices.Where(device => device.deviceType == ArcaneDeviceType.pad && device.clients.Any(c => c.clientType == ArcaneClientType.iframe)).ToList();

        padDevices.ForEach(padDevice =>
        {
            var iframeClientId = padDevice.clients.FirstOrDefault(c => c.clientType == ArcaneClientType.iframe)?.id;
            var internalClientId = padDevice.clients.FirstOrDefault(c => c.clientType == ArcaneClientType.@internal)?.id;

            if (string.IsNullOrEmpty(iframeClientId))
            {
                Debug.LogError("Tried to set pad but iframeClientId was not found");
            }

            if (string.IsNullOrEmpty(internalClientId))
            {
                Debug.LogError("Tried to set pad but internalClientId was not found");
            }

            pads.Add(new ArcanePad(
                deviceId: padDevice.id,
                internalId: internalClientId,
                iframeId: iframeClientId,
                isConnected: true,
                user: padDevice.user
            ));
        });

        return pads;
    }


    // void InitPads(IList<ArcaneDevice> _devices)
    // {
    //     var padDevices = _devices.Where(device => device.deviceType == ArcaneDeviceType.pad).ToList();

    //     padDevices.ForEach(padDevice =>
    //     {
    //         var iframeClientId = padDevice.clients.FirstOrDefault(c => c.clientType == ArcaneClientType.iframe).id;
    //         var internalClientId = padDevice.clients.FirstOrDefault(c => c.clientType == ArcaneClientType.@internal).id;

    //         if (string.IsNullOrEmpty(iframeClientId))
    //         {
    //             Debug.LogError("Tried to set pad but iframeClientId was not found");
    //         }

    //         if (string.IsNullOrEmpty(internalClientId))
    //         {
    //             Debug.LogError("Tried to set pad but internalClientId was not found");
    //         }

    //         var padExists = Pads.Any(p => p.DeviceId == padDevice.id);

    //         if (!padExists)
    //         {
    //             Pads.Add(new ArcanePad(
    //                 deviceId: padDevice.id,
    //                 internalId: internalClientId,
    //                 iframeId: iframeClientId,
    //                 isConnected: true
    //             ));
    //         }
    //     });
    // }

    void RefreshClientsIds(IList<ArcaneDevice> _devices)
    {
        InternalPadsIds = _devices.Where(device => device.deviceType == ArcaneDeviceType.pad).SelectMany(device => device.clients
            .Where(client => client.clientType == ArcaneClientType.@internal).Select(client => client.id)).ToList();

        InternalViewsIds = _devices.Where(device => device.deviceType == ArcaneDeviceType.view).SelectMany(device => device.clients
            .Where(client => client.clientType == ArcaneClientType.@internal).Select(client => client.id)).ToList();

        IframePadsIds = _devices.Where(device => device.deviceType == ArcaneDeviceType.pad).SelectMany(device => device.clients
            .Where(client => client.clientType != ArcaneClientType.@internal).Select(client => client.id)).ToList();

        IframeViewsIds = _devices.Where(device => device.deviceType == ArcaneDeviceType.view).SelectMany(device => device.clients
            .Where(client => client.clientType != ArcaneClientType.@internal).Select(client => client.id)).ToList();
    }
}

