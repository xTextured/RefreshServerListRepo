using HarmonyLib;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace repoServerRefresh.Patches
{
    [HarmonyPatch(typeof(MenuPageServerList), nameof(MenuPageServerList.OnRoomListUpdate))]
    public static class ServerListPatch
    {
        static void Prefix(MenuPageServerList __instance, ref int __state)
        {
            // Save the current page so we can restore it after refresh
            __state = Traverse.Create(__instance).Field("pageCurrent").GetValue<int>();
        }

        static void Postfix(MenuPageServerList __instance, List<RoomInfo> _roomList, int __state)
        {
            // Access private fields
            var traverse = Traverse.Create(__instance);
            var roomListField = traverse.Field("roomList");

            var roomList = roomListField.GetValue() as IList;

            if (roomList == null || roomList.Count == 0)
                return;

            var itemType = roomList[0].GetType();

            // Get the playerCount field info once
            FieldInfo playerCountField = itemType.GetField("playerCount");

            // Create a sorted list
            var sorted = new List<object>(roomList.Cast<object>());
            sorted.Sort((a, b) =>
            {
                int countA = (int)playerCountField.GetValue(a);
                int countB = (int)playerCountField.GetValue(b);
                return countB.CompareTo(countA); // descending
            });

            // Clear and re-add sorted items
            roomList.Clear();
            foreach (var item in sorted)
                roomList.Add(item);

            // Restore the previous page, clamped to valid range
            int pageMax = traverse.Field("pageMax").GetValue<int>();
            int restoredPage = Mathf.Clamp(__state, 0, pageMax);
            traverse.Field("pageCurrent").SetValue(restoredPage);

            // Re-run page logic
            MethodInfo setPageLogic = AccessTools.Method(typeof(MenuPageServerList), "SetPageLogic");
            setPageLogic.Invoke(__instance, null);
        }
    }
}
