using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ArcanepadSDK.Models;
using ArcanepadSDK;
using TMPro;
using UnityEngine.SceneManagement;
using ArcanepadSDK.Types;

public class AViewManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public List<PlayerController> players = new List<PlayerController>();
    public bool gameStarted { get; private set; }
    public static bool isGamePaused = false;
    public TextMeshProUGUI deviceTypeText;
    async void Start()
    {
        Arcane.Init();

        var initialState = await Arcane.ArcaneClientInitialized();

        deviceTypeText.text = Arcane.Msg.DeviceType;

        if (Arcane.Msg.DeviceType == ArcaneDeviceType.pad)
        {
            SceneManager.LoadScene("PadScene");
        }

        initialState.pads.ForEach(pad =>
        {
            createPlayer(pad);
        });

        Arcane.Msg.On(AEventName.OpenArcaneMenu, () =>
        {
            isGamePaused = true;
        });
        Arcane.Msg.On(AEventName.CloseArcaneMenu, () =>
        {
            isGamePaused = false;
        });

        Arcane.Msg.On(AEventName.IframePadConnect, (IframePadConnectEvent e) =>
        {
            var playerExists = players.Any(p => p.Pad.IframeId == e.iframeId);
            if (playerExists) return;

            var pad = new ArcanePad(deviceId: e.deviceId, internalId: e.internalId, iframeId: e.iframeId, isConnected: true, user: e.user);

            createPlayer(pad);
        });

        Arcane.Msg.On(AEventName.IframePadDisconnect, (IframePadDisconnectEvent e) =>
        {
            var player = players.FirstOrDefault(p => p.Pad.IframeId == e.IframeId);

            if (player == null) Debug.LogError("Player not found to remove on disconnect");
            destroyPlayer(player);
        });

    }

    void createPlayer(ArcanePad pad)
    {
        GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        PlayerController playerComponent = newPlayer.GetComponent<PlayerController>();
        playerComponent.Initialize(pad);

        players.Add(playerComponent);
    }

    void destroyPlayer(PlayerController playerComponent)
    {
        playerComponent.Pad.Dispose();
        players.Remove(playerComponent);
        Destroy(playerComponent.gameObject);
    }

}