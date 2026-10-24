using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishNet.Connection;
using FishNet.Transporting;
using HarmonyLib;
using Players;
using Players.Interface.Model;
using Players.Visuals.Camera;
using Ships.Interface.Model.Parts;
using Players.Network.Services;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTracking.Interface.Model;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Sitting on the furniture (sofa, armchair, chair, stool, tables). Look at one and press the
    /// use key: the host puts the player on the free seat nearest to them (standing on the piece,
    /// whose collision is its box) and tells everyone; the player's view drops to a seated eye
    /// height, and the other players see their avatar seated (legs bent, hips on the seat). The use
    /// key or a move key gets up, in front of the piece. The game has no seated state: the player
    /// stays "walking", only the view and the avatars are posed.
    /// </summary>
    internal static class Seats
    {
        /// <summary>Each piece's seats: the hips' point on the seat, in the part's frame (the piece faces -z).</summary>
        private static readonly Dictionary<ushort, Vector3[]> Points = new Dictionary<ushort, Vector3[]>
        {
            [7440] = new[] { new Vector3(-0.6f, 0.47f, -0.08f), new Vector3(0f, 0.47f, -0.08f), new Vector3(0.6f, 0.47f, -0.08f) },
            [7441] = new[] { new Vector3(0f, 0.47f, -0.08f) },
            [7442] = new[] { new Vector3(0f, 0.5f, -0.03f) },
            [7443] = new[] { new Vector3(0f, 0.71f, 0f) },
            [7444] = new[] { new Vector3(-0.5f, 0.77f, -0.3f), new Vector3(0.5f, 0.77f, -0.3f) },
            [7445] = new[] { new Vector3(-0.3f, 0.43f, -0.15f), new Vector3(0.3f, 0.43f, -0.15f) },
            [8208] = new[] { new Vector3(-0.52f, 0.47f, 0f), new Vector3(0f, 0.47f, 0f), new Vector3(0.52f, 0.47f, 0f) },
            [8209] = new[] { new Vector3(0f, 0.53f, -0.02f) },
            [8253] = new[] { new Vector3(-0.62f, 0.47f, -0.08f), new Vector3(0f, 0.47f, -0.08f), new Vector3(0.62f, 0.47f, -0.08f) },
            [8254] = new[] { new Vector3(-0.5f, 0.47f, -0.62f), new Vector3(0.5f, 0.47f, -0.62f), new Vector3(-0.5f, 0.47f, 0.62f), new Vector3(0.5f, 0.47f, 0.62f) },
        };

        private const float Reach = 4f, EyeAbove = 0.68f, LegAngle = 12f;

        public static bool IsSeat(ushort id) => Points.ContainsKey(id);

        /// <summary>The beds: the use key sleeps there, like the game's bed.</summary>
        public static bool IsBed(ushort id) => id == 8200 || id == 8201 || id == 8247 || id == 8256;

        /// <summary>A seat of a piece: (ship, part, seat).</summary>
        internal struct Seat
        {
            public uint Ship;
            public ushort Part;
            public int Index;

            public override string ToString() => Ship.ToString(CultureInfo.InvariantCulture) + "|" + Part.ToString(CultureInfo.InvariantCulture) + "|" + Index.ToString(CultureInfo.InvariantCulture);

            public static bool TryParse(string text, out Seat seat)
            {
                seat = default;
                string[] f = (text ?? "").Split('|');
                return f.Length == 3 && uint.TryParse(f[0], out seat.Ship) && ushort.TryParse(f[1], out seat.Part) && int.TryParse(f[2], out seat.Index);
            }
        }

        // Host: who sits where (player id -> seat); clients: the other players seated, and this one
        private static readonly Dictionary<uint, Seat> ServerSeated = new Dictionary<uint, Seat>();
        private static readonly Dictionary<uint, Seat> Others = new Dictionary<uint, Seat>();
        private static Seat? _mine;
        private static float _since;

        private static readonly System.Reflection.MethodInfo NotifyRespawnAtShip = AccessTools.Method(typeof(PlayerRespawnService), "NotifyRespawnAtShip");
        private static readonly System.Reflection.FieldInfo VisualField = AccessTools.Field(typeof(TrackedPlayerOtherClient), "_visualObject");

        public static bool Seated => _mine.HasValue;

        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.SitRequest, ServerRequest);
            TFNet.OnClient(TFMessageKind.SitState, (kind, player, b, text) =>
            {
                if (Seat.TryParse(text, out Seat seat))
                    Others[player] = seat;
                else
                    Others.Remove(player);
            });
            TFNet.OnClient(TFMessageKind.SitSelf, (kind, a, b, text) =>
            {
                _mine = Seat.TryParse(text, out Seat seat) ? seat : (Seat?)null;
                _since = Time.unscaledTime;
            });
            TFNet.SyncRequested += connection =>
            {
                foreach (KeyValuePair<uint, Seat> seated in ServerSeated)
                    TFNet.SendTo(connection, TFMessageKind.SitState, seated.Key, 0, seated.Value.ToString());
            };
            TFNet.Disconnected += () =>
            {
                ServerSeated.Clear();
                Others.Clear();
                _mine = null;
            };
            Application.onBeforeRender += PoseOthers;
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((30f, "seats", () =>
            {
                if (StellarDriveDemoTFAdvanced.Common.DevHarness.Off("seats"))
                    MelonLoader.MelonCoroutines.Start(DevRun());
            }));
        }

        // Development: a ship with a sofa and a chair; the host sits on the sofa (its view), a
        // mannequin is posed on the chair and photographed, then the host gets up
        private static System.Collections.IEnumerator DevRun()
        {
            var players = Core.Services.ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            var spaces = Core.Services.ServiceLocator.GetService<WorldTracking.Spaces.SpacesServer>();
            var ships = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>();
            var ids = Core.Services.ServiceLocator.GetService<WorldTracking.Services.WorldObjectsIdsServerTracker>();
            TrackedPlayerServer player = players?.ActivePlayers.FirstOrDefault();
            if (player == null || spaces == null || ships == null || ids == null)
                yield break;
            var placed = new List<(ushort, Vector3, int, int)> { (7440, new Vector3(0f, -0.46f, 1f), 0, 4), (7442, new Vector3(2f, -0.46f, 1f), 0, 4) };
            uint shipId = StellarDriveDemoTFAdvanced.Parts.Showcase.LoadShip(StellarDriveDemoTFAdvanced.Parts.Showcase.Json(placed, 3), player, spaces, ships, ids, 12f);
            MelonLoader.MelonCoroutines.Start(StellarDriveDemoTFAdvanced.Construction.SnapTest.Pin(spaces, ships, shipId, 60f));
            yield return Worlds.StationVisit.Wait(4f);
            Ships.TrackedShipServer ship = ships.GetShip(shipId);
            if (ship == null)
                yield break;
            Worlds.StationVisit.Move(ship, new Vector3(0f, 0.6f, -0.4f), Quaternion.identity);
            yield return Worlds.StationVisit.Wait(3f);
            Ask(new PartKey(shipId, 1));
            yield return Worlds.StationVisit.Wait(2f);
            TFMod.Log.Msg($"dev: seats: seated {Seated} at {_mine}, eye {(TryEye(out Vector3 eye) ? eye.ToString("F2") : "none")}");
            StellarDriveDemoTFAdvanced.Common.DevHarness.Screenshot("seat_view");
            // A mannequin on the chair
            if (TrySeat(new Seat { Ship = shipId, Part = 2, Index = 0 }, out Vector3 hips, out Quaternion facing))
            {
                var holder = new GameObject("TFA_SeatTest");
                GameObject mannequin = CosmeticCabin.Mannequin(holder.transform);
                if (mannequin != null)
                {
                    mannequin.transform.SetParent(null, true);
                    for (int i = 0; i < 3; i++)
                    {
                        Pose(mannequin.transform, hips, facing);
                        yield return null;
                    }
                    Camera camera = StellarDriveDemoTFAdvanced.Common.MainCamera.Get();
                    if (camera != null)
                    {
                        camera.transform.position = hips + facing * new Vector3(1.3f, 0.6f, 1.6f);
                        camera.transform.LookAt(hips + Vector3.up * 0.1f);
                    }
                    StellarDriveDemoTFAdvanced.Common.DevHarness.Screenshot("seat_mannequin");
                    TFMod.Log.Msg($"dev: seats: mannequin hips at {hips:F2}");
                }
            }
            TFNet.SendToServer(TFMessageKind.SitRequest, shipId, 1, "stand");
            _mine = null;
            yield return Worlds.StationVisit.Wait(2f);
            TFMod.Log.Msg($"dev: seats: stood up, seated players on the host {ServerSeated.Count}");
        }

        /// <summary>The use key on a piece: sit on its seat nearest to the player.</summary>
        public static void Ask(PartKey piece) => TFNet.SendToServer(TFMessageKind.SitRequest, piece.ShipId, piece.PartId, "sit");

        /// <summary>Every frame: the use key or a move key gets up.</summary>
        public static void Update()
        {
            if (!_mine.HasValue || Time.unscaledTime - _since < 0.4f || ModMenu.IsOpen)
                return;
            Keyboard keyboard = Keyboard.current;
            bool up = Settings.UseKey.Value != null && Settings.UseKey.Value.Length > 0 && UseKeyPressed()
                || keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame
                    || keyboard.dKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            if (up || GameServices.LocalState.Type != PlayerStateType.Walking)
            {
                Seat seat = _mine.Value;
                _mine = null;
                TFNet.SendToServer(TFMessageKind.SitRequest, seat.Ship, seat.Part, "stand");
            }
        }

        private static readonly KeySetting UseKey = new KeySetting(() => Settings.UseKey);

        private static bool UseKeyPressed() => UseKey.WasPressed;

        // ---- Host

        private static void ServerRequest(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            TrackedPlayerServer player = sender == null ? null : GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            var ships = GameServices.ShipsServer;
            if (player == null || ships == null)
                return;
            Ships.TrackedShipServer ship = ships.GetShip(shipId);
            if (text == "stand")
            {
                if (!ServerSeated.TryGetValue(player.Id, out Seat was))
                    return;
                ServerSeated.Remove(player.Id);
                TFNet.SendToAll(TFMessageKind.SitState, player.Id, 0, "");
                // Up in front of the piece, on the floor it stands on
                if (ship != null && ship.TryGetPartPlacement(was.Part, out IPartPlacement placement) && Points.TryGetValue(SettingsId(ship, was.Part), out Vector3[] seats))
                {
                    Vector3 seat = seats[Mathf.Clamp(was.Index, 0, seats.Length - 1)];
                    float front = Mathf.Abs(seat.z) + 0.75f;
                    Move(sender, player, ship, placement, new Vector3(seat.x, 1.05f, -front), Vector3.back);
                }
                return;
            }
            if (ship == null || !ship.TryGetPartPlacement(partId, out IPartPlacement where) || !Points.TryGetValue(SettingsId(ship, partId), out Vector3[] points))
                return;
            // The free seat nearest to the player
            Vector3 playerLocal = PlayerInShip(player, ship);
            int best = -1;
            float bestDistance = Reach;
            for (int i = 0; i < points.Length; i++)
            {
                if (ServerSeated.Values.Any(s => s.Ship == shipId && s.Part == partId && s.Index == i))
                    continue;
                float distance = Vector3.Distance(where.LocalPosition + where.Rotation * points[i], playerLocal);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            if (best < 0)
            {
                TFNet.SendTo(sender, TFMessageKind.NpcServiceResult, 0, 0, Loc.Pick("Pas de place libre (ou trop loin).", "No free seat (or too far).", "No hay sitio libre (o está demasiado lejos)."));
                return;
            }
            var chosen = new Seat { Ship = shipId, Part = partId, Index = best };
            ServerSeated[player.Id] = chosen;
            // Standing on the piece over the seat (its collision is its box); the view and the avatars do the sitting
            Move(sender, player, ship, where, new Vector3(points[best].x, Top(points[best]) + 1.05f, points[best].z), Vector3.back);
            TFNet.SendToAll(TFMessageKind.SitState, player.Id, 0, chosen.ToString());
            TFNet.SendTo(sender, TFMessageKind.SitSelf, 0, 0, chosen.ToString());
        }

        // The piece's box top over the seat (the seats are a little under it: cushions, seat boards)
        private static float Top(Vector3 seat) => seat.y + 0.4f;

        // (every placed part is a "stateful" part, the stateless ones with a default state)
        private static ushort SettingsId(IShipStateRead ship, ushort partId) =>
            ship.TryGetStatefulPart(partId, out StatefulPart part) && part.Settings != null ? part.Settings.id : (ushort)0;

        private static Vector3 PlayerInShip(TrackedPlayerServer player, Ships.TrackedShipServer ship)
        {
            var transpose = StellarDriveDemoTFAdvanced.Common.GameServices.TransposeServer;
            if (player.SpaceId == ship.RelativeSpaceId || transpose == null)
                return player.Position;
            try
            {
                return transpose.Position(player.SpaceId, ship.RelativeSpaceId, player.Position);
            }
            catch (System.ArgumentException)
            {
                return Vector3.positiveInfinity;
            }
        }

        private static void Move(NetworkConnection sender, TrackedPlayerServer player, Ships.TrackedShipServer ship, IPartPlacement placement, Vector3 local, Vector3 face)
        {
            var spaces = StellarDriveDemoTFAdvanced.Common.GameServices.SpacesServer;
            if (spaces == null)
                return;
            var physics = new LocalPhysicsState
            {
                Position = placement.LocalPosition + placement.Rotation * local,
                Rotation = placement.Rotation,
                Velocity = Vector3.zero
            };
            spaces.SetWorldObjectPhysics(player, ship.RelativeSpaceId, physics);
            Quaternion look = Quaternion.LookRotation(placement.Rotation * face, placement.Rotation * Vector3.up);
            PlayerRespawnService respawn = Object.FindFirstObjectByType<PlayerRespawnService>();
            if (respawn != null && NotifyRespawnAtShip != null)
                NotifyRespawnAtShip.Invoke(respawn, new object[] { sender, player.PhysicsState, look, Channel.Reliable });
        }

        // ---- Where a seat is, on this machine

        internal static bool TrySeat(Seat seat, out Vector3 hips, out Quaternion rotation)
        {
            hips = default;
            rotation = default;
            var ships = GameServices.ShipsClient;
            if (ships == null || !ships.TryGetShipVisualTransform(seat.Ship, out Transform visual) || visual == null
                || !ships.TryGetShip(seat.Ship, out IShipStateRead state) || !state.TryGetPartPlacement(seat.Part, out IPartPlacement placement))
                return false;
            if (!Points.TryGetValue(SettingsId(state, seat.Part), out Vector3[] points))
                return false;
            Vector3 point = points[Mathf.Clamp(seat.Index, 0, points.Length - 1)];
            hips = visual.TransformPoint(placement.LocalPosition + placement.Rotation * point);
            rotation = visual.rotation * placement.Rotation * Quaternion.Euler(0f, 180f, 0f);
            return true;
        }

        /// <summary>The seated view: the eyes over the seat, looking where the player looks.</summary>
        internal static bool TryEye(out Vector3 eye)
        {
            eye = default;
            if (!_mine.HasValue || !TrySeat(_mine.Value, out Vector3 hips, out Quaternion rotation))
                return false;
            eye = hips + rotation * new Vector3(0f, EyeAbove, 0.08f);
            return true;
        }

        // The other players seated: their avatars over the seat, legs bent forward, feet down
        private static void PoseOthers()
        {
            if (Others.Count == 0)
                return;
            var players = GameServices.PlayersClient;
            if (players == null)
                return;
            foreach (TrackedPlayerOtherClient other in players.OtherPlayers)
            {
                if (other == null || !Others.TryGetValue(other.Id, out Seat seat) || !other.IsVisualLoaded || !(VisualField?.GetValue(other) is GameObject visual) || visual == null)
                    continue;
                if (!TrySeat(seat, out Vector3 hips, out Quaternion rotation))
                    continue;
                Pose(visual.transform, hips, rotation);
            }
        }

        internal static void Pose(Transform avatar, Vector3 seat, Quaternion facing)
        {
            Transform Bone(string name) => avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mixamorig:" + name);
            Transform hipsBone = Bone("Hips");
            if (hipsBone == null)
                return;
            avatar.rotation = facing;
            avatar.position += seat - hipsBone.position;
            Vector3 forward = facing * Vector3.forward, down = facing * Vector3.down;
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform thigh = Bone(side + "UpLeg"), knee = Bone(side + "Leg"), foot = Bone(side + "Foot");
                if (thigh == null || knee == null || foot == null)
                    continue;
                // Thighs forward along the seat, a little apart and down to the knee
                Vector3 outward = (thigh.position - hipsBone.position);
                outward -= facing * Vector3.up * Vector3.Dot(outward, facing * Vector3.up);
                Vector3 thighDir = (Quaternion.AngleAxis(-LegAngle, facing * Vector3.right) * forward + outward.normalized * 0.12f).normalized;
                thigh.rotation = Quaternion.FromToRotation(knee.position - thigh.position, thighDir) * thigh.rotation;
                // Shins down to the floor
                Vector3 shinDir = (down + forward * 0.12f).normalized;
                knee.rotation = Quaternion.FromToRotation(foot.position - knee.position, shinDir) * knee.rotation;
            }
        }
    }

    /// <summary>The seated player's view: after the game places the cameras (and the third person view, and before the photo mode).</summary>
    [HarmonyPatch(typeof(PlayerCamerasUpdater), nameof(PlayerCamerasUpdater.UpdateCameras))]
    internal static class SeatCameraPatch
    {
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerCamera> Camera =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerCamera>("_camera");
        private static readonly AccessTools.FieldRef<PlayerCamerasUpdater, PlayerDistantCamera> Distant =
            AccessTools.FieldRefAccess<PlayerCamerasUpdater, PlayerDistantCamera>("_distantCamera");

        [HarmonyPriority(Priority.Low)]
        private static void Postfix(PlayerCamerasUpdater __instance)
        {
            if (!Seats.Seated || PhotoMode.Active || !Seats.TryEye(out Vector3 eye))
                return;
            PlayerCamera camera = Camera(__instance);
            if (camera == null)
                return;
            Quaternion look = camera.transform.rotation;
            camera.UpdateCamera(eye, look);
            Distant(__instance)?.UpdateCamera(eye, look);
        }
    }

    /// <summary>
    /// A bed: the use key lies down to pass the time like the game's bed (the player waits;
    /// the night goes by once every player waits). Not while the ship flies.
    /// </summary>
    internal sealed class BedVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Dormir (passer la nuit)", "Sleep (pass the night)", "Dormir (pasar la noche)");

        public override void Use()
        {
            var context = new PartContext(Key.ShipId, Key.PartId);
            var wait = Core.Services.ServiceLocator.GetService<WorldProvision.Interface.Services.IWaitPossibilityClientProvider>();
            if (wait != null && !wait.CanWait(context))
            {
                Core.Services.ServiceLocator.GetService<UI.Interface.Services.IWarningsDisplay>()?.ShowTempWarning(Loc.Pick("Impossible de dormir pendant le vol", "Can't sleep while flying", "No se puede dormir en vuelo"));
                return;
            }
            Core.Services.ServiceLocator.GetService<Managers.Interface.Services.IShipInteractionNetworkService>()?.Wait(new Managers.Interface.Model.PartInteractNetworkRequest { ShipId = Key.ShipId, PartId = Key.PartId });
        }
    }

    /// <summary>A piece of furniture one can sit on: the use key sits.</summary>
    internal sealed class SeatVisuals : UsablePart
    {
        public override string Label => Loc.Pick("S'asseoir", "Sit down", "Sentarse");

        public override void Use() => Seats.Ask(Key);
    }
}
