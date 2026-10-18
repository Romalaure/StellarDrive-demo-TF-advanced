using System.Collections.Generic;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using Ships.Physics;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;

namespace StellarDriveDemoTFAdvanced.Propulsion
{
    /// <summary>
    /// The mini Stellar Core: the game's warp drive (part 16) at a little over half its size,
    /// cheap and researched early, but its warp is twenty times slower (50 000 km/s at full
    /// signal instead of 1 000 000 km/s): enough to cross the star system in a minute or two,
    /// not to jump across it in seconds. Everything else is the game's own drive: same signal
    /// socket (warp power), warm-up, screen, spinner and effects, same stops near planets and
    /// beacons.
    /// </summary>
    internal static class MiniWarpCore
    {
        public const ushort Id = 7349;
        /// <summary>Its size against the Stellar Core's.</summary>
        private const float Scale = 0.55f;
        /// <summary>Its warp speed against the Stellar Core's, at the same signal.</summary>
        public const float SpeedFactor = 0.05f;

        public static void Configure(PartSettings settings, GameObject prefab)
        {
            float mass = settings.mass * 0.35f;
            PartFactory.Describe(settings, Loc.Pick("Mini cœur stellaire", "Mini Stellar Core", "Mini núcleo estelar"),
                Loc.Pick("Un cœur stellaire compact, moins cher et accessible tôt dans la recherche. Il marche comme le Cœur stellaire (signal sur sa prise = puissance du saut, même préchauffage), mais son saut est 20 fois plus lent : 50 000 km/s au maximum, de quoi traverser le système en une ou deux minutes.",
                    "A compact stellar core, cheaper and reached early in research. It works like the Stellar Core (signal on its socket = warp power, same warm-up), but its warp is 20 times slower: 50,000 km/s at most, enough to cross the system in a minute or two.",
                    "Un núcleo estelar compacto, más barato y accesible pronto en la investigación. Funciona como el Núcleo estelar (señal en su toma = potencia del salto, mismo precalentamiento), pero su salto es 20 veces más lento: 50 000 km/s como máximo, lo justo para cruzar el sistema en uno o dos minutos."),
                mass, new[] { (101u, 12), (112u, 6), (102u, 4), (103u, 2) });

            // The model, its screen, spinner and effects, shrunk around the part's floor origin
            // (the visual container itself keeps its scale: the game reads it)
            Transform visuals = prefab.transform.Find("Visuals");
            if (visuals != null)
            {
                foreach (Transform child in visuals)
                {
                    child.localPosition *= Scale;
                    child.localScale *= Scale;
                }
            }
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
                PartFactory.SetBounds(prefab, bounds.center * Scale, bounds.bounds * Scale);
            // Its plugs follow the base's edge, still on the floor
            foreach (Transform socket in new[] { prefab.transform.Find("SignalSocket"), prefab.transform.Find("PowerSocket") })
            {
                if (socket != null)
                    socket.localPosition = new Vector3(socket.localPosition.x * Scale, socket.localPosition.y, socket.localPosition.z * Scale);
            }
        }
    }

    /// <summary>A warp made by a mini Stellar Core goes at its own, slower speed.</summary>
    [HarmonyPatch(typeof(ShipWarpPhysics), "CalculateWarpTargetVector")]
    internal static class MiniWarpSpeedPatch
    {
        private static readonly Dictionary<(uint, ushort), bool> Mini = new Dictionary<(uint, ushort), bool>();

        private static void Postfix(WarpSpace warpSpace, TrackedShipServer ____ship, ref Vector3 __result)
        {
            if (____ship == null || warpSpace == null)
                return;
            var key = (____ship.Id, warpSpace.SourcePartId);
            if (!Mini.TryGetValue(key, out bool mini))
            {
                mini = false;
                foreach (StatefulPart part in ____ship.StatefulParts)
                {
                    if (part != null && part.Id == warpSpace.SourcePartId)
                    {
                        mini = part.Settings != null && part.Settings.id == MiniWarpCore.Id;
                        break;
                    }
                }
                if (Mini.Count > 256)
                    Mini.Clear();
                Mini[key] = mini;
            }
            if (mini)
                __result *= MiniWarpCore.SpeedFactor;
        }
    }
}
