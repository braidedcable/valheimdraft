using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Jotunn.Managers;
using UnityEngine;

namespace ValheimDraft
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.braidedcable.valheimdraft";
        public const string PluginName = "ValheimDraft";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Dump;
            Logger.LogInfo($"{PluginName} loaded, waiting on OnVanillaPrefabsAvailable.");
        }

        private static void Dump()
        {
            try
            {
                DumpInternal();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{PluginName}] dump failed: {e}");
            }
        }

        private static void DumpInternal()
        {
            var pieces = new List<PieceData>();

            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab.GetComponent<Piece>() == null)
                    continue;

                var bounds = GetBounds(prefab);

                // ASSUMPTION TO VALIDATE: Valheim's convention (per Jotunn's piece
                // tutorial) is that snap points are child transforms named
                // "_snappoint*". Confirm this holds by checking the dumped
                // snapPoints against an in-game piece before trusting the data.
                var snapPoints = prefab.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("_snappoint"))
                    .Select(t => new SnapPointData { pos = t.localPosition, rot = t.localRotation })
                    .ToList();

                pieces.Add(new PieceData
                {
                    prefab = prefab.name,
                    bounds = bounds.size,
                    snapPoints = snapPoints
                });
            }

            var json = JsonUtility.ToJson(new DumpOutput { pieces = pieces }, prettyPrint: true);
            // Valheim's exe is 32-bit; writing under Program Files (x86) without
            // admin rights gets silently redirected by Windows' file
            // virtualization to %LOCALAPPDATA%\VirtualStore\.... Write to
            // Documents instead so the output lands where you actually look.
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                PluginName, "pieces-dump.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);

            Debug.Log($"[{PluginName}] dumped {pieces.Count} pieces to {path}");
        }

        private static Bounds GetBounds(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                var b = colliders[0].bounds;
                foreach (var c in colliders.Skip(1)) b.Encapsulate(c.bounds);
                return b;
            }

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var rb = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) rb.Encapsulate(r.bounds);
            return rb;
        }
    }

    [Serializable]
    internal class SnapPointData
    {
        public Vector3 pos;
        public Quaternion rot;
    }

    [Serializable]
    internal class PieceData
    {
        public string prefab;
        public Vector3 bounds;
        public List<SnapPointData> snapPoints;
    }

    [Serializable]
    internal class DumpOutput
    {
        public List<PieceData> pieces;
    }
}
