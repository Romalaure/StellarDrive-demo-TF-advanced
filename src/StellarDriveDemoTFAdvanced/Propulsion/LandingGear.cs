using System.Collections.Generic;
using Core.Services;
using FishNet.Connection;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Parts.Common.Utils;
using Ships.Physics;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using MainCamera = StellarDriveDemoTFAdvanced.Common.MainCamera;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// Retractable landing gear: a leg with a shock absorber, fixed under the hull (its up axis
    /// pointing away from the hull, toward the ground). Deployed, it holds the ship up like a
    /// suspension: each physics step the host casts a ray from its hip down the leg and, where it
    /// meets the ground, pushes the ship up with a spring and a damper (stiffness from the ship's
    /// mass and its number of legs, so any ship sits about 30 cm into its travel at 1 g), plus a
    /// friction that keeps it from sliding. Retracted, the leg folds flat against the hull and
    /// does nothing. Every gear of a ship (and what is docked to it) deploys or retracts together:
    /// the use key (T) on any of them, or <see cref="Settings.LandingGearKey"/> anywhere aboard.
    /// State: ConstantState, 1 deployed, 0 retracted.
    /// </summary>
    internal static class LandingGear
    {
        public const ushort Id = 7347;
        /// <summary>The leg's hinge in the part, and its length from there to the foot pad, extended (m).</summary>
        public static readonly Vector3 Hip = new Vector3(0f, 0.22f, 0f);
        public const float Rest = 1.5f;
        /// <summary>The shock absorber's travel (m); beyond it the leg is solid.</summary>
        public const float Travel = 0.6f;
        // Spring per leg: ship mass × this / legs (N/m per kg): about 0.33 m in at 1 g
        private const float StiffnessPerKg = 30f;
        private const float DampingRatio = 0.7f;

        public static bool Deployed(IStatefulPartState state) => state is ConstantState constant && constant.ConstantValue > 0.5;

        // ---- The part

        public static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Train d'atterrissage", "Landing gear", "Tren de aterrizaje"),
                Loc.Pick("Une jambe rétractable à amortisseur, à fixer sous la coque (elle sort vers le sol). Sortie, elle porte le vaisseau comme une suspension : le ressort et l'amortisseur absorbent l'atterrissage et le vaisseau se pose sur ses jambes sans glisser. Rentrée, elle se replie à plat contre la coque. Tous les trains du vaisseau sortent et rentrent ensemble : touche d'utilisation (T) sur l'un d'eux, ou touche L n'importe où à bord (au pilotage aussi). Mets-en au moins trois, écartés.",
                    "A retractable leg with a shock absorber, fixed under the hull (it comes out toward the ground). Deployed, it carries the ship like a suspension: the spring and damper soak up the landing and the ship stands on its legs without sliding. Retracted, it folds flat against the hull. All the ship's gear comes out and goes in together: use key (T) on one of them, or the L key anywhere aboard (when piloting too). Fit at least three, spread apart.",
                    "Una pata retráctil con amortiguador, fijada bajo el casco (sale hacia el suelo). Desplegada, sostiene la nave como una suspensión: el muelle y el amortiguador absorben el aterrizaje y la nave se apoya en sus patas sin deslizar. Recogida, se pliega contra el casco. Todos los trenes de la nave salen y entran a la vez: tecla de uso (T) sobre uno de ellos, o la tecla L en cualquier lugar a bordo (también al pilotar). Pon al menos tres, separados."),
                30f, new[] { (101u, 10), (112u, 4), (Items.ModItems.Titanium, 3) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.14f, 0f), new Vector3(0.7f, 0.28f, 0.7f));
            LandingGearModel.Build(visuals);
            UsablePart.AddBox(visuals, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 1.3f, 0.8f));
            visuals.gameObject.AddComponent<LandingGearVisuals>();
        }

        // ---- Deploying and retracting (host): every gear of the ship together

        public static void Install() => TFNet.OnServer(TFMessageKind.LandingGear, OnRequest);

        /// <summary>Asks the host to deploy or retract the gear of this ship (and what it is docked to).</summary>
        public static void Toggle(uint shipId) => TFNet.SendToServer(TFMessageKind.LandingGear, shipId, 0, "");

        private static void OnRequest(NetworkConnection sender, TFMessageKind kind, uint shipId, ushort partId, string text)
        {
            var ships = StellarDriveDemoTFAdvanced.Common.GameServices.ShipsServer;
            if (ships == null || !ships.TryGetRootShip(shipId, out IShipStateRead root))
                return;
            var gear = new List<(TrackedShipServer Ship, StatefulPart Part)>();
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(root.Id))
            {
                if (!(ships.GetShip(data.Ship.Id) is TrackedShipServer ship))
                    continue;
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings != null && part.Settings.id == Id)
                        gear.Add((ship, part));
                }
            }
            if (gear.Count == 0)
                return;
            // Any leg in: all out; else all in
            bool deploy = gear.Exists(g => !Deployed(g.Part.State));
            foreach ((TrackedShipServer ship, StatefulPart part) in gear)
                ship.UpdatePartState(part.Id, (ConstantState)part.State with { ConstantValue = deploy ? 1.0 : 0.0 });
            AdvancedMod.Log.Msg($"landing gear of ship {root.Id}: {gear.Count} leg(s) {(deploy ? "deployed" : "retracted")}");
        }

        /// <summary>The key aboard a ship (walking or piloting): the gear of that ship.</summary>
        private static readonly KeySetting Key = new KeySetting(() => TFItems.Common.Settings.LandingGearKey);

        public static void Update()
        {
            if (ModMenu.IsOpen || BuildSearch.Typing || !Key.WasPressed)
                return;
            uint? space = TFItems.Common.GameServices.PlayersClient?.LocalPlayerPhysicsStateSpaceId;
            var spaces = StellarDriveDemoTFAdvanced.Common.GameServices.Spaces;
            if (space == null || spaces == null || !spaces.TryGetSpace(space.Value, out ISpace current) || !(current is ShipRelativeSpace aboard))
                return;
            Toggle(aboard.ShipId);
        }

        // ---- The suspension (host physics)

        private static readonly int Mask = LayerMask.GetMask("Default", "Ship", "WorldObject");
        // Where each leg's pad touched the ground (root ship, leg), in its physics scene
        private static readonly Dictionary<(uint, int), Vector3> Anchors = new Dictionary<(uint, int), Vector3>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>How far the ground is from the hip, along the leg, in a physics scene; Rest when nothing is there.</summary>
        public static float Probe(PhysicsScene scene, Vector3 hip, Vector3 down, Transform own)
        {
            int count = scene.Raycast(hip, down, Hits, Rest, Mask, QueryTriggerInteraction.Ignore);
            float nearest = Rest;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || (own != null && hit.collider.transform.IsChildOf(own)))
                    continue;
                if (hit.distance < nearest)
                    nearest = hit.distance;
            }
            return nearest;
        }

        internal static void Apply(IShipStateWrite rootShip, Rigidbody body, IShipsServerProvider ships)
        {
            var legs = new List<(Vector3 Hip, Vector3 Down)>();
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(rootShip.Id))
            {
                if (data.Ship == null)
                    continue;
                foreach (StatefulPart part in data.Ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings == null || part.Settings.id != Id || !Deployed(part.State))
                        continue;
                    Quaternion rotation = data.RotationRelativeToRoot * part.LocalRotation;
                    legs.Add((data.PositionRelativeToRoot + data.RotationRelativeToRoot * part.LocalPosition + rotation * Hip, rotation * Vector3.up));
                }
            }
            if (legs.Count == 0)
                return;
            PhysicsScene scene = body.gameObject.scene.GetPhysicsScene();
            float share = body.mass / legs.Count;
            float stiffness = share * StiffnessPerKg;
            float damping = 2f * DampingRatio * Mathf.Sqrt(stiffness * share);
            for (int leg = 0; leg < legs.Count; leg++)
            {
                (Vector3 hipLocal, Vector3 downLocal) = legs[leg];
                Vector3 hip = body.position + body.rotation * hipLocal;
                Vector3 down = body.rotation * downLocal;
                float distance = Probe(scene, hip, down, body.transform);
                float compression = Rest - distance;
                var key = (rootShip.Id, leg);
                if (compression <= 0f)
                {
                    Anchors.Remove(key);
                    continue;
                }
                Vector3 foot = hip + down * distance;
                Vector3 velocity = body.GetPointVelocity(foot);
                // Into the leg: positive while it is pushed in
                float closing = Vector3.Dot(velocity, down);
                // Past its travel the leg is solid: much stiffer
                float spring = stiffness * compression + (compression > Travel ? stiffness * 8f * (compression - Travel) : 0f);
                float push = Mathf.Max(0f, spring + damping * closing);
                body.AddForceAtPosition(-down * push, foot, ForceMode.Force);
                // Grip: the pad holds where it touched down (a stiff sideways spring and damper),
                // and only slips past 40 cm, or when pushed harder than friction allows
                Vector3 sideways = velocity - down * Vector3.Dot(velocity, down);
                if (!Anchors.TryGetValue(key, out Vector3 anchor))
                    anchor = foot;
                Vector3 offset = foot - anchor;
                offset -= down * Vector3.Dot(offset, down);
                if (offset.sqrMagnitude > 0.4f * 0.4f)
                {
                    anchor = foot;
                    offset = Vector3.zero;
                }
                Anchors[key] = anchor;
                Vector3 grip = -offset * stiffness - sideways * damping * 2f;
                body.AddForceAtPosition(Vector3.ClampMagnitude(grip, push * 1.0f), foot, ForceMode.Force);
            }
        }
    }

    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class LandingGearPhysicsPatch
    {
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Body = AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");
        private static IShipsServerProvider _ships;
        // Ships with no deployed leg: skipped for a second
        private static readonly Dictionary<uint, float> Without = new Dictionary<uint, float>();

        private static void Postfix(IShipStateWrite ____ship, ShipRigidbody ____shipRigidbody)
        {
            try
            {
                if (____ship == null || ____shipRigidbody == null)
                    return;
                float now = Time.unscaledTime;
                if (Without.TryGetValue(____ship.Id, out float until) && now < until)
                    return;
                if (_ships == null)
                    _ships = ServiceLocator.GetService<IShipsServerProvider>();
                Rigidbody body = Body(____shipRigidbody);
                if (_ships == null || body == null || body.isKinematic)
                    return;
                bool any = false;
                foreach (RootRelativeShipData data in _ships.GetRootConnectedShips(____ship.Id))
                {
                    if (data.Ship == null)
                        continue;
                    foreach (StatefulPart part in data.Ship.GetAllStatefulParts<ConstantState>())
                    {
                        if (part.Settings != null && part.Settings.id == LandingGear.Id && LandingGear.Deployed(part.State))
                        {
                            any = true;
                            break;
                        }
                    }
                }
                if (!any)
                {
                    if (Without.Count > 512)
                        Without.Clear();
                    Without[____ship.Id] = now + 1f;
                    return;
                }
                Without.Remove(____ship.Id);
                LandingGear.Apply(____ship, body, _ships);
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("landing gear", e);
            }
        }
    }
}
