using System;
using System.Collections.Generic;
using Core.Services;
using Core.Utils;
using Core.Values;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Interface.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using StellarDriveDemoTFAdvanced.TFItems.Devices;
using Tools.PaintTool;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTracking.Interface.Values;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>
    /// Paint zoop, the paint tool's take on the BuildZoop mod: holding its zoop key (Shift unless
    /// BuildZoop says otherwise) fixes a corner where the player aims; the box from it to where they
    /// aim now is shown, and a click paints everything in it at once (walls, floors and slopes, and
    /// placed parts), with the brush's color, finish and camouflage, on the side aimed at first
    /// (both with "both sides"). Same requests as single strokes, sent a few per frame.
    /// The paint pot (a mode of the paint window): a click paints the whole hull of the ship aimed
    /// at (every wall, floor and slope joined to the one aimed at), never its placed parts.
    /// </summary>
    internal static class PaintZoop
    {
        private const int Limit = 4000, PerFrame = 60;
        private const float Margin = 0.35f;

        private static bool _anchored, _front = true, _hasAim;
        private static uint _ship;
        private static IShipPartTargeter _targeter;
        private static Vector3 _anchor, _aim;
        private static readonly Queue<Action> Outbox = new Queue<Action>();
        private static readonly List<HullPartPosition> Hull = new List<HullPartPosition>();
        private static readonly List<ushort> Floating = new List<ushort>();
        private static float _nextCount;
        private static GameObject _box;
        private static Material _boxMaterial;

        private static object _zoopKey;
        private static System.Reflection.PropertyInfo _held;
        private static bool _looked;

        public static bool Anchored => _anchored;

        /// <summary>The paint pot mode: a click paints the whole hull.</summary>
        public static bool Bucket;
        private const int BucketLimit = 40000;
        private static bool _bucketAim, _bucketFront;
        private static uint _bucketShip;
        private static IShipPartTargeter _bucketTargeter;
        private static Vector3 _bucketAt;

        /// <summary>Held: the BuildZoop zoop key when that mod is there, Shift otherwise.</summary>
        private static bool Held
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    Type settings = AccessTools.TypeByName("BuildZoop.Settings");
                    _zoopKey = settings != null ? AccessTools.Field(settings, "ZoopKey")?.GetValue(null) : null;
                    _held = _zoopKey != null ? AccessTools.Property(_zoopKey.GetType(), "Held") : null;
                }
                if (_held != null)
                {
                    try
                    {
                        return (bool)_held.GetValue(_zoopKey);
                    }
                    catch
                    {
                        _held = null;
                    }
                }
                return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            }
        }

        /// <summary>The paint tool's aim this frame (from its checks): the point on a ship, in ship space.</summary>
        public static void Aim(PaintTool tool, Vector3 origin, Vector3 direction, float distance, int mask, bool hullFront)
        {
            _hasAim = false;
            _bucketAim = false;
            if (!(Held || Bucket) || ModMenu.IsOpen || PaintHud.MenuOpen)
            {
                Cancel();
                return;
            }
            if (!ClientScene.PhysicsScene.Raycast(origin, direction, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
                return;
            var ship = hit.collider.GetComponentInParent<IShipPartTargeter>();
            if (ship == null)
                return;
            PartHit local = ship.ConvertHitToLocalHit(hit);
            // Just under the surface aimed at: inside the floor or wall hit
            Vector3 at = local.Pos - local.Normal.normalized * 0.1f;
            if (Bucket)
            {
                Cancel();
                _bucketAim = true;
                _bucketShip = ship.ShipId;
                _bucketTargeter = ship;
                _bucketAt = at;
                _bucketFront = hullFront;
                return;
            }
            if (!_anchored)
            {
                _anchored = true;
                _ship = ship.ShipId;
                _targeter = ship;
                _anchor = at;
                _front = hullFront;
                _nextCount = 0f;
            }
            if (ship.ShipId != _ship)
                return;
            _aim = at;
            _hasAim = true;
        }

        public static void Cancel()
        {
            _anchored = false;
            _hasAim = false;
            Hull.Clear();
            Floating.Clear();
            if (_box != null)
                _box.SetActive(false);
        }

        private static Bounds Box()
        {
            var bounds = new Bounds(_anchor, Vector3.zero);
            bounds.Encapsulate(_aim);
            bounds.Expand(Margin * 2f);
            return bounds;
        }

        /// <summary>Each frame: the requests out, the box and its count refreshed.</summary>
        public static void Update()
        {
            for (int i = 0; i < PerFrame && Outbox.Count > 0; i++)
            {
                try
                {
                    Outbox.Dequeue()();
                }
                catch (Exception e)
                {
                    StellarDriveDemoTFAdvanced.Common.ErrorLog.Report("paint zoop", e);
                }
            }
            if (!PaintHud.ToolActive || !Held)
            {
                if (_anchored)
                    Cancel();
                return;
            }
            if (!_anchored || !_hasAim)
                return;
            if (Time.unscaledTime >= _nextCount)
            {
                _nextCount = Time.unscaledTime + 0.15f;
                Collect();
            }
            ShowBox();
        }

        // What the box holds: the hull parts by their middle, the placed parts by their position
        private static void Collect()
        {
            Hull.Clear();
            Floating.Clear();
            IShipsProvider ships = ServiceLocator.GetService<IShipsProvider>();
            if (ships == null || !ships.TryGetShip(_ship, out IShipStateRead ship) || ship == null)
                return;
            Bounds box = Box();
            Vector3Int min = Vector3Int.FloorToInt(box.min) - Vector3Int.one, max = Vector3Int.CeilToInt(box.max) + Vector3Int.one;
            for (int x = min.x; x <= max.x && Hull.Count < Limit; x++)
                for (int y = min.y; y <= max.y && Hull.Count < Limit; y++)
                    for (int z = min.z; z <= max.z && Hull.Count < Limit; z++)
                    {
                        var coord = new Vector3Int(x, y, z);
                        Ships.Grid.Model.ShipVoxel voxel = ship.GetVoxel(coord);
                        if (voxel == null || !voxel.HasPart)
                            continue;
                        foreach (VoxelPartType type in new[] { VoxelPartType.Floor, VoxelPartType.ForwardWall, VoxelPartType.RightWall, VoxelPartType.Interior })
                        {
                            HullPart? part = voxel.GetPartFromType(type);
                            var position = new HullPartPosition(coord, type);
                            if (part.HasValue && part.Value.Settings != null && box.Contains(PartPositionHelper.GetHullPartInteriorPos(position)))
                                Hull.Add(position);
                        }
                    }
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (Floating.Count >= Limit)
                    break;
                if (part?.Settings != null && box.Contains(part.LocalPosition))
                    Floating.Add(part.Id);
            }
        }

        private static void ShowBox()
        {
            var visuals = GameServices.ShipsClient;
            if (visuals == null || !visuals.TryGetShipVisualTransform(_ship, out Transform shipTransform) || shipTransform == null)
                return;
            if (_box == null)
            {
                _box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _box.name = "TFA_PaintZoopBox";
                UnityEngine.Object.Destroy(_box.GetComponent<Collider>());
                _boxMaterial = StellarDriveDemoTFAdvanced.Common.TransparentMaterials.Blended("TFA_PaintZoopBox", new Color(1f, 0.6f, 0.15f, 0.18f));
                var renderer = _box.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _boxMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                UnityEngine.Object.DontDestroyOnLoad(_box);
            }
            if (_box.transform.parent != shipTransform)
                _box.transform.SetParent(shipTransform, false);
            Bounds box = Box();
            _box.transform.localPosition = box.center;
            _box.transform.localRotation = Quaternion.identity;
            _box.transform.localScale = box.size;
            if (_boxMaterial != null && GameServices.PaintSelection != null)
            {
                Color c = GameServices.PaintSelection.SelectedPaintColor;
                _boxMaterial.color = new Color(c.r, c.g, c.b, 0.22f);
            }
            _box.SetActive(true);
        }

        /// <summary>The click while the box is up: everything in it painted. True when the click is taken.</summary>
        public static bool Commit()
        {
            if (Bucket)
                return _bucketAim && Fill();
            if (!_anchored || !_hasAim)
                return false;
            Collect();
            var selection = GameServices.PaintSelection;
            if (selection == null)
                return false;
            uint shipId = _ship;
            bool both = selection.PaintBothSides;
            PaintHull(Hull, shipId, _targeter, _front, both);
            IShipsProvider provider = ServiceLocator.GetService<IShipsProvider>();
            foreach (ushort id in Floating)
            {
                ushort partId = id;
                Vector3 where = provider != null && provider.TryGetShip(shipId, out IShipStateRead read) && read.TryGetStatefulPart(partId, out StatefulPart part) ? part.LocalPosition : Vector3.zero;
                Outbox.Enqueue(() =>
                {
                    var key = new PartKey(shipId, partId);
                    PaintNet.RequestPaint(key, Camouflage.Enabled ? PaintBrush.At(where) : PaintBrush.Current, false);
                    if (both && !Camouflage.Enabled)
                        PaintNet.RequestPaint(key.Interior, PaintBrush.Current, false);
                });
            }
            _toast = Loc.Pick($"Zone peinte : {Hull.Count + Floating.Count} pièces", $"Area painted: {Hull.Count + Floating.Count} parts", $"Zona pintada: {Hull.Count + Floating.Count} piezas");
            _toastUntil = Time.unscaledTime + 2.5f;
            // A new corner for the next box (the key still held)
            Cancel();
            return true;
        }

        // The paint pot: the hull joined to the voxel aimed at (26 neighbours), placed parts left alone
        private static bool Fill()
        {
            IShipsProvider ships = ServiceLocator.GetService<IShipsProvider>();
            var selection = GameServices.PaintSelection;
            if (selection == null || ships == null || !ships.TryGetShip(_bucketShip, out IShipStateRead ship) || ship == null)
                return false;
            Vector3Int start = Vector3Int.RoundToInt(_bucketAt);
            var seen = new HashSet<Vector3Int>();
            var open = new Queue<Vector3Int>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int c = start + new Vector3Int(x, y, z);
                        if (ship.GetVoxel(c)?.HasPart == true && seen.Add(c))
                            open.Enqueue(c);
                    }
            var hull = new List<HullPartPosition>();
            while (open.Count > 0 && seen.Count < BucketLimit)
            {
                Vector3Int coord = open.Dequeue();
                Ships.Grid.Model.ShipVoxel voxel = ship.GetVoxel(coord);
                foreach (VoxelPartType type in new[] { VoxelPartType.Floor, VoxelPartType.ForwardWall, VoxelPartType.RightWall, VoxelPartType.Interior })
                {
                    HullPart? part = voxel?.GetPartFromType(type);
                    if (part.HasValue && part.Value.Settings != null)
                        hull.Add(new HullPartPosition(coord, type));
                }
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++)
                        {
                            Vector3Int next = coord + new Vector3Int(x, y, z);
                            if (!seen.Contains(next) && ship.GetVoxel(next)?.HasPart == true && seen.Add(next))
                                open.Enqueue(next);
                        }
            }
            if (hull.Count == 0)
                return false;
            PaintHull(hull, _bucketShip, _bucketTargeter, _bucketFront, selection.PaintBothSides);
            _toast = Loc.Pick($"Pot de peinture : {hull.Count} pièces de coque", $"Paint pot: {hull.Count} hull pieces", $"Bote de pintura: {hull.Count} piezas de casco");
            _toastUntil = Time.unscaledTime + 3f;
            return true;
        }

        private static void PaintHull(List<HullPartPosition> hull, uint shipId, IShipPartTargeter targeter, bool front, bool both)
        {
            var network = ServiceLocator.GetService<IShipInteractionNetworkService>();
            Color brush = GameServices.PaintSelection.SelectedPaintColor;
            foreach (HullPartPosition position in hull)
            {
                HullPartPosition at = position;
                Outbox.Enqueue(() =>
                {
                    if (network == null)
                        return;
                    Color color = Camouflage.Enabled ? Camouflage.ColorAt(at.VoxelCoord, brush) : brush;
                    var paint = new Vector3Byte((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f));
                    HullPartColor current = targeter != null ? targeter.GetHullColor(at) : new HullPartColor(paint, paint);
                    network.PaintHullPart(new PaintHullPartNetworkRequest
                    {
                        ShipId = shipId,
                        Position = at,
                        Color = new HullPartColor(front || both ? paint : current.Front, !front || both ? paint : current.Back)
                    });
                });
            }
        }

        private static string _toast;
        private static float _toastUntil;

        public static void Draw()
        {
            if (_toast != null && Time.unscaledTime < _toastUntil)
                DeviceUi.Toast(Screen.width, Screen.height, _toast);
            if (!PaintHud.ToolActive || ModMenu.IsOpen)
                return;
            string text = Bucket
                ? Loc.Pick("Pot de peinture · clic : toute la coque du vaisseau", "Paint pot · click: the whole ship's hull", "Bote de pintura · clic: todo el casco de la nave")
                : _anchored && _hasAim
                ? Loc.Pick($"Zone : {Hull.Count + Floating.Count} pièces · clic pour peindre", $"Area: {Hull.Count + Floating.Count} parts · click to paint", $"Zona: {Hull.Count + Floating.Count} piezas · clic para pintar")
                : null;
            if (text == null)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            var rect = new Rect(width / 2f - 230f, height - 200f, 460f, 32f);
            DeviceUi.Panel(rect);
            GUI.Label(rect, text, new GUIStyle(DeviceUi.Text) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            GUI.matrix = previous;
        }
    }

    /// <summary>The click of the paint tool while a zoop box is up paints the box, not the part aimed at.</summary>
    [HarmonyPatch(typeof(PaintTool), nameof(PaintTool.PrimaryActionBegin))]
    internal static class PaintZoopClickPatch
    {
        private static bool Prefix() => !PaintZoop.Commit();
    }
}
