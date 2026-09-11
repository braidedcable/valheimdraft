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

                // Real convention per dumped data (wood_floor): child names like
                // "$hud_snappoint_corner 1" — not "_snappoint" as Jotunn's piece
                // tutorial implied. Matching "snappoint" anywhere, case-insensitive,
                // to tolerate both this and the "_snappoint" convention if other
                // pieces use it.
                //
                // Position/rotation are composed into the prefab ROOT's local
                // space (same as GetBounds does for mesh corners), not taken as
                // raw localPosition/localRotation — those are relative to each
                // transform's immediate parent, which is wrong for any snap
                // point nested deeper than a direct child of the root.
                var worldToRoot = prefab.transform.worldToLocalMatrix;
                var rootRotInverse = Quaternion.Inverse(prefab.transform.rotation);
                var snapPoints = prefab.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.IndexOf("snappoint", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(t => new SnapPointData
                    {
                        pos = worldToRoot.MultiplyPoint3x4(t.position),
                        rot = rootRotInverse * t.rotation
                    })
                    .ToList();

                pieces.Add(new PieceData
                {
                    prefab = prefab.name,
                    bounds = bounds.size,
                    center = bounds.center,
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

        // Prefabs here are never instantiated into a live scene, so
        // Collider.bounds / Renderer.bounds (computed by the physics/render
        // systems for active objects) come back zero. Mesh.bounds is a
        // static property of the mesh asset itself and Transform matrices
        // are plain data regardless of active state, so both work on inert
        // prefabs — combine every child mesh's local bounds into the root's
        // local space via the transform hierarchy.
        private static Bounds GetBounds(GameObject root)
        {
            // Wear-state variants (New/Worn/Broken) sit as sibling subtrees,
            // each with their own mesh — including all of them skews bounds
            // badly. activeInHierarchy doesn't distinguish them (these
            // prefabs are never instantiated, so WearNTear never runs to
            // flip "New" active), so target the literal "New" child by name
            // instead; fall back to the whole prefab for pieces with no such
            // wear-state grouping at all.
            var newVariant = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "New");
            var searchRoot = newVariant != null ? newVariant.gameObject : root;

            var meshFilters = searchRoot.GetComponentsInChildren<MeshFilter>(true)
                .Where(mf => mf.sharedMesh != null)
                .ToArray();

            var b = new Bounds();
            var initialized = false;
            var worldToRoot = root.transform.worldToLocalMatrix;

            foreach (var mf in meshFilters)
            {
                var mb = mf.sharedMesh.bounds;
                var localToRoot = worldToRoot * mf.transform.localToWorldMatrix;

                for (var xi = 0; xi < 2; xi++)
                for (var yi = 0; yi < 2; yi++)
                for (var zi = 0; zi < 2; zi++)
                {
                    var corner = new Vector3(
                        xi == 0 ? mb.min.x : mb.max.x,
                        yi == 0 ? mb.min.y : mb.max.y,
                        zi == 0 ? mb.min.z : mb.max.z);
                    var p = localToRoot.MultiplyPoint3x4(corner);

                    if (!initialized)
                    {
                        b = new Bounds(p, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        b.Encapsulate(p);
                    }
                }
            }

            return b;
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
            sb.Append(",\"center\":");
            AppendVec3(sb, p.center);
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
        public Vector3 center;
        public List<SnapPointData> snapPoints;
    }
}
