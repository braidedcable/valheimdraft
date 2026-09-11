using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
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

        private bool _dumped;

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} loaded, polling for ZNetScene.");
        }

        private void Update()
        {
            if (_dumped || ZNetScene.instance == null)
                return;

            _dumped = true;
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

            var json = ToJson(pieces);
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

        // Hand-rolled instead of JsonUtility: JsonUtility doesn't reliably
        // serialize a list whose elements themselves contain another list
        // (pieces -> each piece's snapPoints), silently dropping/mangling
        // output instead of throwing. This schema is small and fixed, so
        // writing it directly sidesteps that limitation entirely.
        private static string ToJson(List<PieceData> pieces)
        {
            var sb = new StringBuilder();
            sb.Append("{\"pieces\":[");
            for (var i = 0; i < pieces.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendPiece(sb, pieces[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendPiece(StringBuilder sb, PieceData p)
        {
            sb.Append("{\"prefab\":\"").Append(Escape(p.prefab)).Append("\",");
            sb.Append("\"bounds\":");
            AppendVec3(sb, p.bounds);
            sb.Append(",\"snapPoints\":[");
            for (var i = 0; i < p.snapPoints.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"pos\":");
                AppendVec3(sb, p.snapPoints[i].pos);
                sb.Append(",\"rot\":");
                AppendQuat(sb, p.snapPoints[i].rot);
                sb.Append('}');
            }
            sb.Append("]}");
        }

        private static void AppendVec3(StringBuilder sb, Vector3 v)
        {
            sb.Append("{\"x\":").Append(Num(v.x))
              .Append(",\"y\":").Append(Num(v.y))
              .Append(",\"z\":").Append(Num(v.z))
              .Append('}');
        }

        private static void AppendQuat(StringBuilder sb, Quaternion q)
        {
            sb.Append("{\"x\":").Append(Num(q.x))
              .Append(",\"y\":").Append(Num(q.y))
              .Append(",\"z\":").Append(Num(q.z))
              .Append(",\"w\":").Append(Num(q.w))
              .Append('}');
        }

        private static string Num(float f) => f.ToString(CultureInfo.InvariantCulture);

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    internal class SnapPointData
    {
        public Vector3 pos;
        public Quaternion rot;
    }

    internal class PieceData
    {
        public string prefab;
        public Vector3 bounds;
        public List<SnapPointData> snapPoints;
    }
}
