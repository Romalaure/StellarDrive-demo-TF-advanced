using System.Collections.Generic;
using Core.Services;
using FishNet.Connection;
using HarmonyLib;
using Ships;
using Ships.Parts.Common;
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
    /// does nothing. Deploying and retracting:
    /// each leg follows the signal on its socket (a lever, a pilot seat, an advanced control...).
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
                Loc.Pick("Une jambe rétractable à amortisseur, à fixer sous la coque (elle sort vers le sol). Sortie, elle porte le vaisseau comme une suspension : le ressort et l'amortisseur absorbent l'atterrissage et le vaisseau se pose sur ses jambes sans glisser. Rentrée, elle se replie à plat contre la coque. Il sort et rentre selon le signal de sa prise : branche sa prise de commande (celle de droite) à un levier, un siège de pilotage, un contrôle avancé ou tout ce qui donne un signal (sorti au-dessus de 0,5) ; sans câble, il reste sorti. La prise de gauche donne son état (1 sorti). Mets-en au moins trois, écartés.",
                    "A retractable leg with a shock absorber, fixed under the hull (it comes out toward the ground). Deployed, it carries the ship like a suspension: the spring and damper soak up the landing and the ship stands on its legs without sliding. Retracted, it folds flat against the hull. It comes out and goes in with the signal on its socket: wire its command socket (the right one) to a lever, a pilot seat, an advanced control or anything giving a signal (out above 0.5); unwired, it stays out. The left socket gives its state (1 out). Fit at least three, spread apart.",
                    "Una pata retráctil con amortiguador, fijada bajo el casco (sale hacia el suelo). Desplegada, sostiene la nave como una suspensión: el muelle y el amortiguador absorben el aterrizaje y la nave se apoya en sus patas sin deslizar. Recogida, se pliega contra el casco. Sale y entra con la señal de su toma: conecta su toma de mando (la derecha) a una palanca, un asiento de piloto, un control avanzado o cualquier cosa que dé una señal (fuera por encima de 0,5); sin cable, se queda fuera. La toma izquierda da su estado (1 fuera). Pon al menos tres, separados."),
                30f, new[] { (101u, 10), (112u, 4), (Items.ModItems.Titanium, 3) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.14f, 0f), new Vector3(0.7f, 0.28f, 0.7f));
            LandingGearModel.Build(visuals);
            visuals.gameObject.AddComponent<LandingGearVisuals>();
            // The signal socket on the plate's edge, the cable running along the hull
            PowerParts.GraftRow(prefab, prefab.GetComponent<ShipPartBounds>(), 0.04f, -0.33f, 0.2f,
                (StatusSocket, CableType.Signal), (CommandSocket, CableType.Signal));
        }

        // ---- Deploying and retracting (host): by the signal on its socket

        /// <summary>
        /// Its signals: 0 gives its state out (1 deployed, the part emits its ConstantState there),
        /// 1 takes the command (a lever, a pilot seat, an advanced control...). The game numbers a
        /// part's sockets from 0 without gaps, and an input on the emitting socket would mix with it.
        /// </summary>
        public const byte StatusSocket = 0, CommandSocket = 1;

        /// <summary>
        /// Host, every power tick: each leg follows its command socket (out above 0.5). A leg with
        /// no cable stays out, like a fixed gear.
        /// </summary>
        public static void Command(ShipsServerTracker ships, Ships.Cables.ConnectionsServerTracker connections)
        {
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings == null || part.Settings.id != Id)
                        continue;
                    var socket = new EntityPartSocket(ship.Id, part.Id, CommandSocket);
                    bool deploy = !connections.IsSocketPhysicallyOccupied(socket) || connections.GetSignalValue(socket) > 0.5;
                    if (deploy != Deployed(part.State))
                        ship.UpdatePartState(part.Id, (ConstantState)part.State with { ConstantValue = deploy ? 1.0 : 0.0 });
                }
            }
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
