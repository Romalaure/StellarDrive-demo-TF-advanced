using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using Planets.Interface.Model;
using Planets.Interface.Services;
using Ships;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Settings;
using Ships.Services;
using Stars.Interface.Model;
using Stars.Interface.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Construction;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// Solar panel support (host simulation): its two wings give power to the networks on its
    /// power sockets in proportion to the sunlight they catch: full when they face the star, none
    /// when they face away or a planet hides the star (night). Its signal: the watts it gives.
    /// </summary>
    internal static class Solar
    {
        private sealed class Runtime
        {
            public float Light;
            public float NextLight;
            public float LastWrite = -10f;
            public double Written = double.NaN;
        }

        private static readonly Dictionary<(uint, ushort), Runtime> Runtimes = new Dictionary<(uint, ushort), Runtime>();
        private static readonly List<(TrackedShipServer Ship, StatefulPart Part, Runtime Runtime, float Watts)> Panels = new List<(TrackedShipServer, StatefulPart, Runtime, float)>();

        public static void Forget()
        {
            Runtimes.Clear();
            Panels.Clear();
        }

        public static void BeforeTransfers(PowerServerTracker power, ShipsServerTracker ships, float deltaTime)
        {
            Panels.Clear();
            float now = Time.unscaledTime;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.GetAllStatefulParts<ConstantState>())
                {
                    if (part.Settings.id != StationModules.SolarId)
                        continue;
                    if (!Runtimes.TryGetValue((ship.Id, part.Id), out Runtime rt))
                        Runtimes[(ship.Id, part.Id)] = rt = new Runtime();
                    // Sunlight changes slowly: measured twice a second
                    if (now >= rt.NextLight)
                    {
                        rt.NextLight = now + 0.5f;
                        rt.Light = Sunlight(ship, part);
                    }
                    float watts = Mathf.Max(0f, PowerCatalog.SolarWatts.Value) * rt.Light;
                    Panels.Add((ship, part, rt, watts));
                    if (watts <= 0f)
                        continue;
                    power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerA), out PowerGroupServerState a);
                    power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, PowerCatalog.MachinePowerB), out PowerGroupServerState b);
                    float output = watts * deltaTime;
                    if (a != null && b != null && a != b)
                    {
                        a.TotalPowerProductionCapacity += output * 0.5f;
                        b.TotalPowerProductionCapacity += output * 0.5f;
                    }
                    else if (a != null || b != null)
                        (a ?? b).TotalPowerProductionCapacity += output;
                }
            }
        }

        public static void AfterTransfers()
        {
            float now = Time.unscaledTime;
            foreach ((TrackedShipServer ship, StatefulPart part, Runtime rt, float watts) in Panels)
            {
                double value = System.Math.Round(watts);
                if (System.Math.Abs(value - rt.Written) < 50 && now - rt.LastWrite < 5f || now - rt.LastWrite < 1f)
                    continue;
                rt.LastWrite = now;
                rt.Written = value;
                ship.UpdatePartState(part.Id, ((ConstantState)part.State) with { ConstantValue = value });
            }
        }

        /// <summary>0 to 1: how squarely the wings face the star, 0 in a planet's shadow.</summary>
        private static float Sunlight(TrackedShipServer ship, StatefulPart part)
        {
            var stars = ServiceLocator.GetService<IStarsProvider>();
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            IStarProperties star = stars?.GetCurrentStar();
            if (star == null || transpose == null || spaces == null || !spaces.TryGetSpace(ship.SpaceId, out ISpace here))
                return 0f;
            Vector3 panel = ship.Position + ship.Rotation * part.LocalPosition;
            Vector3 normal = ship.Rotation * (part.LocalRotation * Vector3.up);
            Vector3 toStar = transpose.Position((ISpace)star.Space, ship.SpaceId, Vector3.zero) - panel;
            float facing = Mathf.Clamp01(Vector3.Dot(normal, toStar.normalized));
            if (facing <= 0f)
                return 0f;
            // Shadow: a planet between the panel and the star
            PreciseWorldPosition at = transpose.ToPreciseWorldPos(here) + (Vector3Double)panel;
            var starAt = new PreciseWorldPosition(star.WorldPos, Vector3.zero);
            Vector3Double ray = starAt.DeltaDouble(at);
            double length = ray.Magnitude;
            var planets = ServiceLocator.GetService<IPlanetsServerProvider>();
            foreach (IPlanetProperties planet in planets?.Planets ?? Enumerable.Empty<IPlanetProperties>())
            {
                Vector3Double toPlanet = new PreciseWorldPosition(planet.WorldCell, Vector3.zero).DeltaDouble(at);
                double along = Vector3Double.Dot(toPlanet, ray) / length;
                if (along <= 0 || along >= length)
                    continue;
                double closest2 = toPlanet.Magnitude * toPlanet.Magnitude - along * along;
                double radius = planet.Radius * 0.99;
                if (closest2 < radius * radius)
                    return 0f;
            }
            return facing;
        }

        // ---- The part

        public static void Configure(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, "Support de panneaux solaires",
                $"Un mât et deux ailes de cellules photovoltaïques : jusqu'à {PowerCatalog.SolarWatts.Value:0} W sur ses prises électriques quand les ailes font face à l'étoile, rien la nuit ou dans l'ombre d'une planète. Posez-le sur le toit de la base, ailes vers le ciel. Sa prise signal donne la puissance produite (W).",
                120f, new[] { (102u, 12), (112u, 10), (104u, 6), (101u, 6) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float wing = 3f, chord = 1.6f, mast = 1.6f;
            Transform visuals = PartFactory.Blank(prefab, new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.3f, 0.8f));
            var model = new MachineModels.Model();
            var cells = new MeshBuilder();
            model.Armor.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.8f, 0.12f, 0.8f));
            model.Armor.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.16f, 0.5f));
            model.Armor.Cylinder(new Vector3(0f, 0.28f, 0f), new Vector3(0f, mast, 0f), 0.09f, 0.07f, 10);
            model.Armor.Box(new Vector3(0f, mast + 0.05f, 0f), new Vector3(0.4f, 0.12f, 0.3f));
            model.Light.Box(new Vector3(0f, 0.13f, 0.41f), new Vector3(0.7f, 0.012f, 0.012f));
            var colliders = new List<Bounds> { new Bounds(new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.3f, 0.8f)), new Bounds(new Vector3(0f, mast * 0.5f, 0f), new Vector3(0.2f, mast, 0.2f)) };
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = side * (0.25f + wing * 0.5f);
                // A beam to the wing, its frame, then a grid of dark blue cells
                model.Armor.Box(new Vector3(side * 0.2f, mast + 0.05f, 0f), new Vector3(0.4f, 0.06f, 0.08f));
                model.Armor.Box(new Vector3(cx, mast + 0.08f, 0f), new Vector3(wing, 0.04f, chord));
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float x = cx - wing * 0.5f + (i + 0.5f) * wing / 6f;
                        float z = -chord * 0.5f + (j + 0.5f) * chord / 3f;
                        cells.Box(new Vector3(x, mast + 0.105f, z), new Vector3(wing / 6f - 0.03f, 0.01f, chord / 3f - 0.03f));
                    }
                }
                colliders.Add(new Bounds(new Vector3(cx, mast + 0.08f, 0f), new Vector3(wing, 0.06f, chord)));
            }
            MachineModels.Attach(visuals, model, "Solar", MachineModels.Cyan, MachineModels.Cyan);
            var cellObject = new GameObject("TF_NoPaint_SolarCells") { layer = visuals.gameObject.layer };
            cellObject.transform.SetParent(visuals, false);
            cellObject.AddComponent<MeshFilter>().sharedMesh = cells.Build("TF_NoPaint_SolarCells");
            cellObject.AddComponent<MeshRenderer>().sharedMaterial = LitMaterials.Get("TFA_SolarCell", new Color(0.08f, 0.14f, 0.35f), 0.6f, 0.9f, new Color(0.01f, 0.02f, 0.06f));
            // Colliders for the mast and the wings only, so the area around the base stays free
            var root = new GameObject("TFA_ModuleColliders");
            root.transform.SetParent(prefab.transform, false);
            int layer = LayerMask.NameToLayer("Ship");
            root.layer = layer;
            for (int i = 0; i < colliders.Count; i++)
            {
                var child = new GameObject("Box" + i) { layer = layer, tag = "ShipFloatingPart" };
                child.transform.SetParent(root.transform, false);
                var box = child.AddComponent<BoxCollider>();
                box.center = colliders[i].center;
                box.size = colliders[i].size;
            }
            root.AddComponent<ModuleColliders>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftSeated(prefab, bounds, PowerCatalog.MachinePowerA, CableType.Power, new Vector3(-0.4f, 0.06f, 0f), Vector3.left);
            PowerParts.GraftSeated(prefab, bounds, PowerCatalog.MachinePowerB, CableType.Power, new Vector3(0.4f, 0.06f, 0f), Vector3.right);
            PowerParts.GraftSeated(prefab, bounds, PowerCatalog.MachineSignal, CableType.Signal, new Vector3(0f, 0.06f, -0.4f), Vector3.back);
        }
    }
}
