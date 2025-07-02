using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MenuLib;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace repoServerRefresh
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Loader : BaseUnityPlugin
    {
        private const string modGUID = "Textured.ServerRefreshMod";
        private const string modName = "Server Refresh Mod";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);
        internal static ManualLogSource mls;

        void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo("Server Refresh Mod - Loaded");

            harmony.PatchAll();

            // Inject the button into the server list menu using menuLib
            MenuAPI.AddElementToServerListMenu(parent =>
            {
                MenuAPI.CreateREPOButton("Refresh Servers", () =>
                {
                    var serverList = GameObject.FindObjectOfType<MenuPageServerList>();
                    if (serverList != null && !serverList.IsInvoking("StartRefresh"))
                    {
                        MethodInfo method = typeof(MenuPageServerList)
                            .GetMethod("GetServerList", BindingFlags.NonPublic | BindingFlags.Instance);
                        serverList.StopAllCoroutines(); // stop any current refresh
                        serverList.StartCoroutine(ForceServerRefresh(serverList));

                        mls.LogInfo("Server list refresh triggered via button.");
                    }
                }, parent, localPosition: Vector2.zero);
            });
        }

        private static IEnumerator ForceServerRefresh(MenuPageServerList serverList)
        {
            // Destroy all existing server buttons
            foreach (Transform child in serverList.serverElementParent)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            // Clear existing room data
            var roomListField = Traverse.Create(serverList).Field("roomList");
            var roomListObj = roomListField.GetValue();
            if (roomListObj is System.Collections.IList roomList)
            {
                roomList.Clear();
            }

            // Reset internal state
            Traverse.Create(serverList).Field("receivedList").SetValue(false);

            // Disconnect Photon and wait
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.NetworkingClient.State != ClientState.Disconnected &&
                   PhotonNetwork.NetworkingClient.State != ClientState.PeerCreated)
            {
                yield return null;
            }

            // Re-authenticate and reconnect
            SteamManager.instance.SendSteamAuthTicket();
            DataDirector.instance.PhotonSetRegion();
            DataDirector.instance.PhotonSetVersion();
            DataDirector.instance.PhotonSetAppId();
            PhotonNetwork.ConnectUsingSettings();

            // Wait until server list is received (room list updated and receivedList = true)
            while (!Traverse.Create(serverList).Field("receivedList").GetValue<bool>())
            {
                yield return null;
            }

            // Start UpdatePage coroutine to rebuild UI
            serverList.StartCoroutine("UpdatePage");
        }

    }
}
