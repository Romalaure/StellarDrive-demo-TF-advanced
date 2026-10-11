using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common.Model.Reactions;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// Space station modules: rooms you walk into, built as one placed part each. The game gives a
    /// placed part one solid box; these carry their own colliders instead (floor, ceiling, walls
    /// around their doorways, through the game's dynamic collider hook, like its doors), and a
    /// placement box reduced to their floor so furniture and machines can be placed inside.
    /// Doorways are 1.6 m wide and 2.2 m high, centered on the module's ends and sides, so modules
    /// set end to end line up. A module is pressurized when any module of the station is piped
    /// to an oxygen line (see <see cref="StationAir"/>). Only call when SDModKit is loaded.
    /// Part space: +Y up out of the floor, the module's length along Z.
    /// </summary>
    internal static class StationModules
    {
        public const ushort HabitationId = 7371, CorridorId = 7372, HubId = 7373, StorageId = 7374, SolarId = 7375, DockingId = 7376;
        public const ushort LaboratoryId = 7377, InfirmaryId = 7378, LifeSupportId = 7379, ObservatoryId = 7380;
        public const float Wall = 0.15f, DoorWidth = 1.6f, DoorHeight = 2.2f;

        [Flags]
        internal enum Side { None = 0, Front = 1, Back = 2, Left = 4, Right = 8 }

        internal sealed class Spec
        {
            public ushort Id;
            public string Label;
            public string Description;
            public float Mass;
            public (uint Item, int Count)[] Cost;
            public Vector3 Size;
            public Side Openings;
            /// <summary>Windows along the left and right walls.</summary>
            public bool Windows;
            public Action<Shell> Interior;
            public string Donor = "SignalDisplay";
            public string StateClass;
            /// <summary>Socket of the oxygen supply.</summary>
            public byte AirSocket;
            /// <summary>Where the module's own frame (floor at 0, centered) sits in part space.</summary>
            public Vector3 Offset;
            /// <summary>The donor's own machine stays, working, inside the room: where its base goes, and its turn about the vertical.</summary>
            public bool KeepDonor;
            public Vector3 DonorAt;
            public float DonorTurn;
            /// <summary>A glass ceiling between the roof beams.</summary>
            public bool GlassRoof;
            /// <summary>Build menu row, under the first.</summary>
            public int Row;
        }

        private const uint Iron = 101, Glass = 102, Aluminum = 104, Copper = 112;

        public static readonly Spec[] All =
        {
            new Spec
            {
                Id = HabitationId, Label = "Module d'habitation", Mass = 900f, Size = new Vector3(4f, 3f, 6f), Openings = Side.Front | Side.Back, Windows = true,
                Cost = new[] { (Iron, 40), (Glass, 12), (Aluminum, 10), (Copper, 6) },
                Description = "Une pièce de station de 4 × 6 m, 3 m sous plafond, avec deux couchettes, une table, des casiers et des hublots. Portes aux deux bouts. Pressurisée si un module de la station est relié à une conduite d'oxygène (prise de fluide sous le plancher, à l'arrière).",
                Interior = Habitation
            },
            new Spec
            {
                Id = CorridorId, Label = "Couloir pressurisé", Mass = 380f, Size = new Vector3(2.4f, 2.6f, 4f), Openings = Side.Front | Side.Back,
                Cost = new[] { (Iron, 16), (Glass, 2), (Aluminum, 4) },
                Description = "Couloir de 4 m entre deux modules, avec mains courantes, caillebotis et éclairage. Pressurisé comme les autres modules de la station.",
                Interior = Corridor
            },
            new Spec
            {
                Id = HubId, Label = "Hub de connexion", Mass = 700f, Size = new Vector3(4f, 3f, 4f), Openings = Side.Front | Side.Back | Side.Left | Side.Right, Windows = false,
                Cost = new[] { (Iron, 30), (Glass, 4), (Aluminum, 8), (Copper, 8) },
                Description = "Carrefour de 4 × 4 m avec une porte sur chaque face pour relier jusqu'à quatre modules, et une console centrale.",
                Interior = Hub
            },
            new Spec
            {
                Id = StorageId, Label = "Module de stockage", Mass = 650f, Size = new Vector3(4f, 3f, 4f), Openings = Side.Back, Donor = "StorageChest", StateClass = "StorageState", AirSocket = 0,
                Cost = new[] { (Iron, 30), (Aluminum, 10), (Copper, 4) },
                Description = "Soute de station avec ses étagères et un terminal d'inventaire au fond (dix emplacements, comme un coffre). Une seule porte, à l'arrière.",
                Interior = Storage
            },
            new Spec
            {
                Id = DockingId, Label = "Module d'amarrage", Mass = 520f, Size = new Vector3(3f, 2.6f, 4f), Openings = Side.Back | Side.Front, StateClass = "DockingDoorState", AirSocket = 1,
                // The game docks at a fixed point of a docking door's frame, (0.5, 0.5, 0.25): the collar is put there
                Offset = new Vector3(0.5f, -0.5f, 0.25f - 2f),
                Cost = new[] { (Iron, 24), (Aluminum, 8), (Copper, 6), (Glass, 2) },
                Description = "Sas avec un collier d'amarrage magnétique au bout : un vaisseau s'y amarre par sa porte d'amarrage, comme sur la porte du jeu (signal > 0 sur la prise de commande pour l'aimant). Posez-en plusieurs autour d'un hub pour accueillir plusieurs vaisseaux.",
                Interior = Docking
            },
            // Modules built around one of the game's machines, which keeps working inside
            new Spec
            {
                Id = LaboratoryId, Label = "Laboratoire", Mass = 700f, Size = new Vector3(4f, 3f, 4f), Openings = Side.Back, Windows = true, Row = 1,
                Donor = "ResearchTable", KeepDonor = true, DonorAt = new Vector3(0f, Wall, 1.1f), DonorTurn = 180f, AirSocket = 0,
                Cost = new[] { (Iron, 30), (Glass, 10), (Aluminum, 8), (Copper, 12) },
                Description = "Un laboratoire de station autour d'une vraie table de recherche (on y fait sa recherche comme sur la table du jeu), avec paillasses, flacons et écrans. Une porte à l'arrière.",
                Interior = Laboratory
            },
            new Spec
            {
                Id = InfirmaryId, Label = "Infirmerie de clonage", Mass = 750f, Size = new Vector3(4f, 3f, 4f), Openings = Side.Back, Row = 1,
                Donor = "ClonePod", KeepDonor = true, DonorAt = new Vector3(0f, Wall, 1.25f), DonorTurn = -90f, AirSocket = 0,
                Cost = new[] { (Iron, 30), (Glass, 8), (Aluminum, 10), (Copper, 10) },
                Description = "Une infirmerie avec une vraie capsule de clonage au fond : choisissez-la comme point de réapparition, comme la capsule du jeu. Deux lits médicaux et une armoire à pharmacie. Une porte à l'arrière.",
                Interior = Infirmary
            },
            new Spec
            {
                Id = LifeSupportId, Label = "Module de support vital", Mass = 800f, Size = new Vector3(4f, 3f, 4f), Openings = Side.Back | Side.Front, Row = 1,
                StateClass = PowerCatalog.MachineStateClass, AirSocket = PowerCatalog.MachineFluid,
                Cost = new[] { (Iron, 34), (Glass, 6), (Aluminum, 10), (Copper, 14) },
                Description = "Le cœur de l'air d'une station : une usine à oxygène qui consomme 2 500 W (prises électriques sous le plancher, à l'arrière) et fabrique 0,1 unité d'oxygène par seconde dans la conduite de sa prise d'air. Branchez-y un réservoir : tous les modules de la station respirent sur cette conduite. Sa prise signal donne l'oxygène fabriqué (millièmes d'unité/s). Portes aux deux bouts.",
                Interior = LifeSupport
            },
            new Spec
            {
                Id = ObservatoryId, Label = "Observatoire", Mass = 600f, Size = new Vector3(4f, 3f, 4f), Openings = Side.Back, Windows = true, GlassRoof = true, Row = 1,
                Donor = "StarMap", KeepDonor = true, DonorAt = new Vector3(0f, Wall + 0.74f, 0.4f), DonorTurn = 180f, AirSocket = 3,
                Cost = new[] { (Iron, 24), (Glass, 24), (Aluminum, 8), (Copper, 8) },
                Description = "Une coupole vitrée pour regarder les étoiles, avec une vraie carte stellaire posée sur un pupitre (elle choisit la cible comme celle du jeu), deux fauteuils et une lunette. Une porte à l'arrière.",
                Interior = Observatory
            },
        };

        public static readonly Dictionary<ushort, Spec> ById = All.ToDictionary(s => s.Id);

        public static void Register()
        {
            foreach (Spec module in All)
            {
                Spec spec = module;
                PartFactory.Register(spec.Id, spec.Donor, AdvancedTab.StationName, AdvancedTab.StationRow + spec.Row, (settings, prefab) => Configure(spec, settings, prefab));
            }
            PartFactory.Register(SolarId, "SignalDisplay", AdvancedTab.StationName, AdvancedTab.StationRow, Solar.Configure);
            AdvancedMod.Log.Msg($"registered {All.Length + 1} station modules");
        }

        /// <summary>The module's air volume in part space (inside its walls, door to door).</summary>
        public static Bounds Interior(Spec spec)
        {
            Vector3 s = spec.Size;
            return new Bounds(spec.Offset + new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x - 2f * Wall, s.y - 2f * Wall, s.z + 0.4f));
        }

        private static void Configure(Spec spec, PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, spec.Label, spec.Description, spec.Mass, spec.Cost);
            if (!spec.KeepDonor)
                settings.internalStateClass = spec.StateClass;
            // Upright on floors; against other modules and on their roofs through FloatingSnap
            settings.snappingStyle = Ships.Interface.Model.SnappingStyle.PreciseOnFloor;
            Vector3 size = spec.Size;
            var shell = new Shell(size);
            // Placement and the "look at it" box: the floor only, so the inside stays free
            Transform visuals = spec.KeepDonor
                ? KeepDonor(spec, prefab, shell)
                : PartFactory.Blank(prefab, spec.Offset + new Vector3(0f, Wall * 0.5f, 0f), new Vector3(size.x, Wall, size.z));
            shell.Build(spec);
            spec.Interior?.Invoke(shell);
            shell.Attach(visuals, "Module" + spec.Id, spec.Offset);
            shell.AttachColliders(prefab, spec.Offset);
            if (spec.Id == StorageId)
            {
                // The inventory opens from the terminal at the far end
                Transform interactions = prefab.transform.Find("Interactions");
                foreach (BoxCollider box in interactions != null ? interactions.GetComponentsInChildren<BoxCollider>(true) : new BoxCollider[0])
                {
                    box.transform.localPosition = spec.Offset + new Vector3(0f, 1.25f, size.z * 0.5f - Wall - 0.12f);
                    box.size = new Vector3(0.8f, 0.6f, 0.2f);
                }
            }
            var bounds = prefab.GetComponent<ShipPartBounds>();
            // The oxygen supply under the floor at the back; the docking magnet's command beside it
            PowerParts.GraftSeated(prefab, bounds, spec.AirSocket, CableType.Fluid, spec.Offset + new Vector3(0.4f, Wall * 0.5f, -size.z * 0.5f), Vector3.back);
            if (spec.Id == DockingId)
                PowerParts.GraftSeated(prefab, bounds, 0, CableType.Signal, spec.Offset + new Vector3(-0.4f, Wall * 0.5f, -size.z * 0.5f), Vector3.back);
            if (spec.Id == LifeSupportId)
            {
                // The oxygen plant's power and signal beside the air socket
                PowerParts.GraftSeated(prefab, bounds, PowerCatalog.MachinePowerA, CableType.Power, spec.Offset + new Vector3(1.0f, Wall * 0.5f, -size.z * 0.5f), Vector3.back);
                PowerParts.GraftSeated(prefab, bounds, PowerCatalog.MachinePowerB, CableType.Power, spec.Offset + new Vector3(-1.0f, Wall * 0.5f, -size.z * 0.5f), Vector3.back);
                PowerParts.GraftSeated(prefab, bounds, PowerCatalog.MachineSignal, CableType.Signal, spec.Offset + new Vector3(-0.4f, Wall * 0.5f, -size.z * 0.5f), Vector3.back);
            }
        }

        /// <summary>
        /// Moves the donor's machine (model, interaction boxes, sockets) to its place in the room
        /// and makes the part's placement box the floor; returns the visual object to build under.
        /// </summary>
        private static Transform KeepDonor(Spec spec, GameObject prefab, Shell shell)
        {
            GameSkin.Capture(prefab);
            Transform root = prefab.transform;
            Transform visuals = prefab.GetComponentInChildren<IShipPartVisual>(true)?.GameObject.transform ?? root.Find("Visuals");
            Transform interactions = root.Find("Interactions");
            Quaternion turn = Quaternion.Euler(0f, spec.DonorTurn, 0f);
            Vector3 at = spec.Offset + spec.DonorAt;
            var bounds = prefab.GetComponent<ShipPartBounds>();
            if (bounds != null)
            {
                // The machine stays solid where it now stands
                Vector3 turned = turn * bounds.bounds;
                shell.Solid(spec.DonorAt + turn * bounds.center, new Vector3(Mathf.Abs(turned.x), Mathf.Abs(turned.y), Mathf.Abs(turned.z)));
            }
            void Move(Transform t)
            {
                Vector3 p = root.InverseTransformPoint(t.position);
                Quaternion r = Quaternion.Inverse(root.rotation) * t.rotation;
                t.position = root.TransformPoint(turn * p + at);
                t.rotation = root.rotation * turn * r;
            }
            var moved = new List<Transform>();
            if (visuals != null && visuals != root)
                moved.AddRange(visuals.Cast<Transform>());
            if (interactions != null)
                moved.AddRange(interactions.Cast<Transform>());
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true))
            {
                if (!moved.Any(m => socket.transform.IsChildOf(m)))
                    moved.Add(socket.transform);
            }
            foreach (Transform t in moved.Distinct().ToList())
                Move(t);
            if (bounds != null)
            {
                bounds.center = spec.Offset + new Vector3(0f, Wall * 0.5f, 0f);
                bounds.bounds = new Vector3(spec.Size.x, Wall, spec.Size.z);
            }
            return visuals;
        }

        // ---- Interiors

        private static void Habitation(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f;
            // Two bunks along the left wall
            foreach (float y in new[] { 0.45f, 1.45f })
            {
                s.Armor.Box(new Vector3(-x + 0.45f, y, 0.4f), new Vector3(0.9f, 0.12f, 2f));
                s.Fabric.Box(new Vector3(-x + 0.45f, y + 0.1f, 0.4f), new Vector3(0.82f, 0.1f, 1.9f));
                s.Fabric.Box(new Vector3(-x + 0.45f, y + 0.18f, 1.2f), new Vector3(0.6f, 0.08f, 0.3f));
            }
            s.Armor.Box(new Vector3(-x + 0.45f, 1f, -0.62f), new Vector3(0.9f, 2f, 0.06f));
            s.Armor.Box(new Vector3(-x + 0.45f, 1f, 1.42f), new Vector3(0.9f, 2f, 0.06f));
            s.Solid(new Vector3(-x + 0.45f, 0.45f, 0.4f), new Vector3(0.9f, 0.3f, 2f));
            // A table and two stools on the right
            s.Armor.Box(new Vector3(x - 0.6f, 0.74f, 0.2f), new Vector3(0.9f, 0.05f, 1.2f));
            s.Dark.Cylinder(new Vector3(x - 0.6f, 0.15f, 0.2f), new Vector3(x - 0.6f, 0.72f, 0.2f), 0.06f, 0.06f, 8);
            foreach (float dz in new[] { -0.75f, 1.15f })
            {
                s.Armor.Cylinder(new Vector3(x - 0.6f, 0.15f, dz), new Vector3(x - 0.6f, 0.45f, dz), 0.05f, 0.05f, 8);
                s.Fabric.Cylinder(new Vector3(x - 0.6f, 0.45f, dz), new Vector3(x - 0.6f, 0.5f, dz), 0.2f, 0.2f, 12);
            }
            // Lockers at the front corner, a screen on the right wall
            for (int i = 0; i < 3; i++)
            {
                s.Armor.Box(new Vector3(x - 0.3f, 1f, -z + 0.9f + i * 0.45f), new Vector3(0.5f, 2f, 0.42f));
                s.Dark.Box(new Vector3(x - 0.56f, 1.5f, -z + 0.9f + i * 0.45f), new Vector3(0.012f, 0.25f, 0.06f));
            }
            s.Solid(new Vector3(x - 0.3f, 1f, -z + 1.35f), new Vector3(0.5f, 2f, 1.3f));
            s.Dark.Box(new Vector3(x - 0.02f, 1.5f, 1.4f), new Vector3(0.03f, 0.55f, 0.9f));
            s.Glow.Box(new Vector3(x - 0.04f, 1.5f, 1.4f), new Vector3(0.01f, 0.45f, 0.8f));
        }

        private static void Corridor(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f;
            foreach (float side in new[] { -1f, 1f })
                s.Armor.Cylinder(new Vector3(side * (x - 0.08f), 1f, -z + 0.1f), new Vector3(side * (x - 0.08f), 1f, z - 0.1f), 0.025f, 0.025f, 8);
            // Grating down the middle
            for (int i = 0; i < 16; i++)
                s.Dark.Box(new Vector3(0f, Wall + 0.004f, -z + 0.15f + i * (s.Size.z - 0.3f) / 15f), new Vector3(1.2f, 0.008f, 0.03f));
        }

        private static void Hub(Shell s)
        {
            // A central column with a ring console
            s.Armor.Cylinder(new Vector3(0f, Wall, 0f), new Vector3(0f, s.Size.y - Wall, 0f), 0.25f, 0.25f, 12);
            s.Armor.Cylinder(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 1f, 0f), 0.6f, 0.55f, 12);
            s.Dark.Cylinder(new Vector3(0f, 1f, 0f), new Vector3(0f, 1.02f, 0f), 0.55f, 0.55f, 12);
            s.Glow.Cylinder(new Vector3(0f, 1.02f, 0f), new Vector3(0f, 1.03f, 0f), 0.5f, 0.3f, 12, capA: false);
            s.Solid(new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 0.9f, 1.1f));
            s.Solid(new Vector3(0f, s.Size.y * 0.5f, 0f), new Vector3(0.5f, s.Size.y, 0.5f));
        }

        private static void Storage(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                // Racks along the side walls with crates on them
                for (int level = 0; level < 3; level++)
                {
                    float y = 0.3f + level * 0.8f;
                    s.Armor.Box(new Vector3(side * (x - 0.35f), y, 0.1f), new Vector3(0.7f, 0.05f, s.Size.z - 1.2f));
                    for (int k = 0; k < 3; k++)
                        s.Crate.Box(new Vector3(side * (x - 0.35f), y + 0.22f, -0.9f + k * 0.95f + level * 0.1f), new Vector3(0.55f, 0.38f, 0.6f));
                }
                foreach (float dz in new[] { -z + 0.6f, z - 0.6f })
                    s.Armor.Box(new Vector3(side * (x - 0.35f), 1.2f, dz), new Vector3(0.7f, 2.4f, 0.06f));
                s.Solid(new Vector3(side * (x - 0.35f), 1.2f, 0.1f), new Vector3(0.7f, 2.4f, s.Size.z - 1.2f));
            }
            // The inventory terminal on the far wall
            s.Armor.Box(new Vector3(0f, 1.25f, z - Wall - 0.06f), new Vector3(0.8f, 0.6f, 0.12f));
            s.Dark.Box(new Vector3(0f, 1.27f, z - Wall - 0.125f), new Vector3(0.66f, 0.42f, 0.01f));
            s.Glow.Box(new Vector3(0f, 1.27f, z - Wall - 0.132f), new Vector3(0.56f, 0.32f, 0.005f));
        }

        private static void Laboratory(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                // A bench along each side wall: cabinet, worktop, flasks glowing on it
                s.Armor.Box(new Vector3(side * (x - 0.35f), 0.52f, -0.3f), new Vector3(0.65f, 0.75f, 2f));
                s.Dark.Box(new Vector3(side * (x - 0.35f), 0.92f, -0.3f), new Vector3(0.7f, 0.04f, 2.1f));
                for (int i = 0; i < 4; i++)
                {
                    float fz = -1.1f + i * 0.5f;
                    s.Glass.Cylinder(new Vector3(side * (x - 0.3f), 0.94f, fz), new Vector3(side * (x - 0.3f), 1.14f, fz), 0.06f, 0.03f, 8);
                    (i % 2 == 0 ? s.Glow : s.Amber).Cylinder(new Vector3(side * (x - 0.3f), 0.95f, fz), new Vector3(side * (x - 0.3f), 1.03f, fz), 0.05f, 0.045f, 8);
                }
                for (int d = 0; d < 3; d++)
                    s.Dark.Box(new Vector3(side * (x - 0.68f), 0.5f, -1.0f + d * 0.65f), new Vector3(0.01f, 0.55f, 0.55f));
                s.Solid(new Vector3(side * (x - 0.35f), 0.52f, -0.3f), new Vector3(0.65f, 0.8f, 2f));
            }
            // Shelves on the back wall either side of the table
            foreach (float side in new[] { -1f, 1f })
                for (int level = 0; level < 3; level++)
                    s.Armor.Box(new Vector3(side * 1.3f, 1.2f + level * 0.45f, z - Wall - 0.15f), new Vector3(0.8f, 0.04f, 0.3f));
        }

        private static void Infirmary(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                // A medical bed with its monitor
                s.Armor.Box(new Vector3(side * (x - 0.5f), 0.4f, -0.5f), new Vector3(0.8f, 0.12f, 1.9f));
                s.Fabric.Box(new Vector3(side * (x - 0.5f), 0.5f, -0.5f), new Vector3(0.74f, 0.08f, 1.8f));
                s.Fabric.Box(new Vector3(side * (x - 0.5f), 0.57f, 0.25f), new Vector3(0.5f, 0.07f, 0.25f));
                s.Dark.Cylinder(new Vector3(side * (x - 0.5f), Wall, -0.5f), new Vector3(side * (x - 0.5f), 0.34f, -0.5f), 0.05f, 0.05f, 8);
                s.Solid(new Vector3(side * (x - 0.5f), 0.4f, -0.5f), new Vector3(0.8f, 0.4f, 1.9f));
                s.Dark.Box(new Vector3(side * (x - 0.03f), 1.4f, 0.3f), new Vector3(0.04f, 0.35f, 0.5f));
                s.Glow.Box(new Vector3(side * (x - 0.055f), 1.4f, 0.3f), new Vector3(0.01f, 0.28f, 0.42f));
            }
            // A white medicine cabinet with a red cross by the door
            s.Light.Box(new Vector3(x - 0.2f, 1.6f, -z + 0.7f), new Vector3(0.3f, 0.6f, 0.5f));
            s.Red.Box(new Vector3(x - 0.36f, 1.6f, -z + 0.7f), new Vector3(0.01f, 0.25f, 0.07f));
            s.Red.Box(new Vector3(x - 0.36f, 1.6f, -z + 0.7f), new Vector3(0.01f, 0.07f, 0.25f));
        }

        private static void LifeSupport(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall;
            // Three tall tanks along the left wall, pipes running up to the ceiling
            for (int i = 0; i < 3; i++)
            {
                float tz = -1f + i;
                Vector3 b = new Vector3(-x + 0.4f, Wall, tz);
                s.Armor.Cylinder(b, b + Vector3.up * 1.9f, 0.32f, 0.32f, 14);
                s.Glass.Box(b + new Vector3(0.31f, 1f, 0f), new Vector3(0.04f, 1.2f, 0.16f));
                (i == 1 ? s.Amber : s.Glow).Box(b + new Vector3(0.32f, 0.7f, 0f), new Vector3(0.03f, 0.6f, 0.12f));
                s.Dark.Cylinder(b + Vector3.up * 1.9f, new Vector3(b.x, s.Size.y - Wall, tz), 0.05f, 0.05f, 8);
                s.Solid(b + Vector3.up * 0.95f, new Vector3(0.64f, 1.9f, 0.64f));
            }
            // Pipes along the ceiling and a control panel on the right wall
            float top = s.Size.y - Wall - 0.1f;
            s.Dark.Cylinder(new Vector3(-x + 0.4f, top, -1.2f), new Vector3(-x + 0.4f, top, 1.2f), 0.06f, 0.06f, 8);
            s.Dark.Cylinder(new Vector3(-x + 0.4f, top, 1.2f), new Vector3(x - 0.3f, top, 1.2f), 0.06f, 0.06f, 8);
            // The oxygen plant against the right wall: a housing with glowing vents, a column and a dome
            Vector3 plant = new Vector3(x - 0.45f, Wall, 0.6f);
            s.Armor.Box(plant + new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 1.1f));
            for (int i = 0; i < 4; i++)
                s.Glow.Box(plant + new Vector3(-0.405f, 0.25f + i * 0.14f, 0f), new Vector3(0.01f, 0.06f, 0.8f));
            s.Armor.Cylinder(plant + new Vector3(0f, 0.9f, 0f), plant + new Vector3(0f, 1.5f, 0f), 0.25f, 0.2f, 14);
            s.Glass.Cylinder(plant + new Vector3(0f, 1.5f, 0f), plant + new Vector3(0f, 1.8f, 0f), 0.2f, 0.05f, 14);
            s.Glow.Cylinder(plant + new Vector3(0f, 1.52f, 0f), plant + new Vector3(0f, 1.66f, 0f), 0.15f, 0.08f, 14);
            s.Dark.Cylinder(plant + new Vector3(0f, 1.8f, 0f), new Vector3(plant.x, s.Size.y - Wall, plant.z), 0.05f, 0.05f, 8);
            s.Solid(plant + new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 1.1f));
            s.Armor.Box(new Vector3(x - 0.1f, 1.3f, -0.9f), new Vector3(0.2f, 0.9f, 0.7f));
            s.Dark.Box(new Vector3(x - 0.205f, 1.4f, -0.9f), new Vector3(0.01f, 0.45f, 0.55f));
            s.Glow.Box(new Vector3(x - 0.21f, 1.4f, -0.9f), new Vector3(0.005f, 0.38f, 0.48f));
            for (int i = 0; i < 4; i++)
                (i % 2 == 0 ? s.Amber : s.Glow).Box(new Vector3(x - 0.21f, 1.0f, -1.12f + i * 0.15f), new Vector3(0.01f, 0.05f, 0.08f));
        }

        private static void Observatory(Shell s)
        {
            // A desk for the star map, two armchairs facing the sky
            s.Armor.Box(new Vector3(0f, Wall + 0.37f, 0.4f), new Vector3(0.9f, 0.74f, 0.8f));
            s.Light.Box(new Vector3(0f, Wall + 0.745f, -0.004f), new Vector3(0.8f, 0.01f, 0.012f));
            s.Solid(new Vector3(0f, Wall + 0.37f, 0.4f), new Vector3(0.9f, 0.74f, 0.8f));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(side * 1.1f, Wall, -0.6f);
                s.Dark.Cylinder(c, c + Vector3.up * 0.35f, 0.06f, 0.06f, 8);
                s.Fabric.Box(c + new Vector3(0f, 0.42f, 0f), new Vector3(0.6f, 0.12f, 0.6f));
                s.Fabric.Box(c + new Vector3(0f, 0.75f, -0.32f), new Vector3(0.6f, 0.6f, 0.1f), Quaternion.Euler(-25f, 0f, 0f));
            }
            // A telescope in the corner
            s.Dark.Cylinder(new Vector3(1.4f, Wall, 1.3f), new Vector3(1.4f, 1.1f, 1.3f), 0.04f, 0.04f, 8);
            s.Armor.Cylinder(new Vector3(1.25f, 1.0f, 1.15f), new Vector3(1.6f, 1.6f, 1.5f), 0.08f, 0.11f, 12);
            s.Glow.Cylinder(new Vector3(1.6f, 1.6f, 1.5f), new Vector3(1.61f, 1.62f, 1.51f), 0.1f, 0.1f, 12);
        }

        private static void Docking(Shell s)
        {
            float z = s.Size.z * 0.5f;
            // The magnetic collar around the front doorway (2 x 2 m like the game's docking door), amber lights on it
            float rx = 1.1f, top = 2.1f;
            s.Armor.Box(new Vector3(-rx, top * 0.5f, z - 0.125f), new Vector3(0.25f, top, 0.25f));
            s.Armor.Box(new Vector3(rx, top * 0.5f, z - 0.125f), new Vector3(0.25f, top, 0.25f));
            s.Armor.Box(new Vector3(0f, top, z - 0.125f), new Vector3(2f * rx + 0.25f, 0.25f, 0.25f));
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 0.5f) / 6f;
                s.Amber.Box(new Vector3(-rx, 0.2f + t * (top - 0.4f), z + 0.01f), new Vector3(0.12f, 0.06f, 0.02f));
                s.Amber.Box(new Vector3(rx, 0.2f + t * (top - 0.4f), z + 0.01f), new Vector3(0.12f, 0.06f, 0.02f));
            }
            s.Solid(new Vector3(-rx, top * 0.5f, z - 0.125f), new Vector3(0.25f, top, 0.25f));
            s.Solid(new Vector3(rx, top * 0.5f, z - 0.125f), new Vector3(0.25f, top, 0.25f));
        }

        /// <summary>
        /// A module's hull: floor, ceiling, walls with doorways and windows, ribs outside and lights
        /// inside. Gathers the model and the collider boxes.
        /// </summary>
        internal sealed class Shell
        {
            public readonly Vector3 Size;
            public readonly MeshBuilder Armor = new MeshBuilder { Panel = GameSkin.PlainPanel };
            public readonly MeshBuilder Hull = new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(0.62f, 0.64f, 0.67f, 1f) };
            public readonly MeshBuilder Inner = new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(0.85f, 0.86f, 0.88f, 1f) };
            public readonly MeshBuilder Dark = new MeshBuilder();
            public readonly MeshBuilder Glow = new MeshBuilder();
            public readonly MeshBuilder Light = new MeshBuilder();
            public readonly MeshBuilder Amber = new MeshBuilder();
            public readonly MeshBuilder Red = new MeshBuilder();
            public readonly MeshBuilder Fabric = new MeshBuilder();
            public readonly MeshBuilder Crate = new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(0.8f, 0.65f, 0.48f, 0f) };
            public readonly MeshBuilder Glass = new MeshBuilder();
            private readonly List<Bounds> _colliders = new List<Bounds>();

            public Shell(Vector3 size)
            {
                Size = size;
            }

            /// <summary>A box players and ships collide with.</summary>
            public void Solid(Vector3 center, Vector3 size) => _colliders.Add(new Bounds(center, size));

            private void Slab(MeshBuilder m, Vector3 center, Vector3 size)
            {
                m.Box(center, size);
                Solid(center, size);
            }

            public void Build(Spec spec)
            {
                float w = Size.x, h = Size.y, l = Size.z;
                // Floor and ceiling, the inside lined with light panels
                Slab(Hull, new Vector3(0f, Wall * 0.5f, 0f), new Vector3(w, Wall, l));
                Inner.Box(new Vector3(0f, Wall + 0.003f, 0f), new Vector3(w - 2f * Wall, 0.006f, l - 0.02f));
                if (spec.GlassRoof)
                {
                    // A glass ceiling held by a rim and crossed beams
                    Slab(Hull, new Vector3(0f, h - Wall * 0.5f, -l * 0.5f + 0.2f), new Vector3(w, Wall, 0.4f));
                    Slab(Hull, new Vector3(0f, h - Wall * 0.5f, l * 0.5f - 0.2f), new Vector3(w, Wall, 0.4f));
                    Slab(Hull, new Vector3(-w * 0.5f + 0.2f, h - Wall * 0.5f, 0f), new Vector3(0.4f, Wall, l - 0.8f));
                    Slab(Hull, new Vector3(w * 0.5f - 0.2f, h - Wall * 0.5f, 0f), new Vector3(0.4f, Wall, l - 0.8f));
                    Armor.Box(new Vector3(0f, h - Wall * 0.5f, 0f), new Vector3(0.1f, Wall, l - 0.8f));
                    Armor.Box(new Vector3(0f, h - Wall * 0.5f, 0f), new Vector3(w - 0.8f, Wall, 0.1f));
                    Glass.Box(new Vector3(0f, h - Wall * 0.5f, 0f), new Vector3(w - 0.8f, 0.03f, l - 0.8f));
                    Solid(new Vector3(0f, h - Wall * 0.5f, 0f), new Vector3(w - 0.8f, Wall, l - 0.8f));
                }
                else
                {
                    Slab(Hull, new Vector3(0f, h - Wall * 0.5f, 0f), new Vector3(w, Wall, l));
                    Inner.Box(new Vector3(0f, h - Wall - 0.003f, 0f), new Vector3(w - 2f * Wall, 0.006f, l - 0.02f));
                }
                // Side walls (x), ends (z)
                SideWall(spec, -1f, (spec.Openings & Side.Left) != 0);
                SideWall(spec, 1f, (spec.Openings & Side.Right) != 0);
                EndWall(-1f, (spec.Openings & Side.Back) != 0);
                EndWall(1f, (spec.Openings & Side.Front) != 0);
                // Ribs around the outside, every 1.5 m or so, with a white strip
                int ribs = Mathf.Max(2, Mathf.RoundToInt(l / 1.5f) + 1);
                for (int i = 0; i < ribs; i++)
                {
                    float z = -l * 0.5f + 0.06f + (l - 0.12f) * i / (ribs - 1);
                    Armor.Box(new Vector3(0f, h + 0.04f, z), new Vector3(w + 0.16f, 0.08f, 0.12f));
                    Armor.Box(new Vector3(-w * 0.5f - 0.04f, h * 0.5f, z), new Vector3(0.08f, h + 0.16f, 0.12f));
                    Armor.Box(new Vector3(w * 0.5f + 0.04f, h * 0.5f, z), new Vector3(0.08f, h + 0.16f, 0.12f));
                    Light.Box(new Vector3(0f, h + 0.085f, z), new Vector3(w * 0.6f, 0.01f, 0.03f));
                }
                // Light strips along the ceiling's inner edges, cyan floor edge lights
                foreach (float side in new[] { -1f, 1f })
                {
                    Light.Box(new Vector3(side * (w * 0.5f - Wall - 0.05f), h - Wall - 0.03f, 0f), new Vector3(0.06f, 0.03f, l * 0.9f));
                    Glow.Box(new Vector3(side * (w * 0.5f - Wall - 0.01f), Wall + 0.05f, 0f), new Vector3(0.012f, 0.02f, l * 0.85f));
                }
            }

            private void SideWall(Spec spec, float side, bool door)
            {
                float w = Size.x, h = Size.y, l = Size.z;
                float x = side * (w * 0.5f - Wall * 0.5f);
                float inner = side * (w * 0.5f - Wall - 0.003f);
                if (door)
                {
                    float rest = (l - DoorWidth) * 0.5f;
                    Slab(Hull, new Vector3(x, h * 0.5f, -l * 0.5f + rest * 0.5f), new Vector3(Wall, h, rest));
                    Slab(Hull, new Vector3(x, h * 0.5f, l * 0.5f - rest * 0.5f), new Vector3(Wall, h, rest));
                    Slab(Hull, new Vector3(x, (DoorHeight + h) * 0.5f, 0f), new Vector3(Wall, h - DoorHeight, DoorWidth));
                    DoorFrame(new Vector3(x, 0f, 0f), Vector3.forward, Vector3.right * side);
                    return;
                }
                if (spec.Windows)
                {
                    // Lower wall, a band of windows, upper wall
                    const float low = 1.2f, high = 1.85f;
                    Slab(Hull, new Vector3(x, low * 0.5f, 0f), new Vector3(Wall, low, l));
                    Slab(Hull, new Vector3(x, (high + h) * 0.5f, 0f), new Vector3(Wall, h - high, l));
                    int panes = Mathf.Max(1, Mathf.RoundToInt(l / 1.5f));
                    for (int i = 0; i <= panes; i++)
                    {
                        float z = -l * 0.5f + l * i / panes;
                        Slab(Armor, new Vector3(x, (low + high) * 0.5f, Mathf.Clamp(z, -l * 0.5f + 0.06f, l * 0.5f - 0.06f)), new Vector3(Wall + 0.02f, high - low, 0.12f));
                    }
                    Glass.Box(new Vector3(x, (low + high) * 0.5f, 0f), new Vector3(0.03f, high - low, l - 0.1f));
                    Solid(new Vector3(x, (low + high) * 0.5f, 0f), new Vector3(Wall, high - low, l));
                    Inner.Box(new Vector3(inner, low * 0.5f, 0f), new Vector3(0.006f, low - 0.02f, l - 0.02f));
                    return;
                }
                Slab(Hull, new Vector3(x, h * 0.5f, 0f), new Vector3(Wall, h, l));
                Inner.Box(new Vector3(inner, h * 0.5f, 0f), new Vector3(0.006f, h - 2f * Wall, l - 0.02f));
            }

            private void EndWall(float end, bool door)
            {
                float w = Size.x, h = Size.y, l = Size.z;
                float z = end * (l * 0.5f - Wall * 0.5f);
                if (!door)
                {
                    Slab(Hull, new Vector3(0f, h * 0.5f, z), new Vector3(w - 2f * Wall, h, Wall));
                    Inner.Box(new Vector3(0f, h * 0.5f, end * (l * 0.5f - Wall - 0.003f)), new Vector3(w - 2f * Wall - 0.02f, h - 2f * Wall, 0.006f));
                    return;
                }
                float rest = (w - 2f * Wall - DoorWidth) * 0.5f;
                if (rest > 0.01f)
                {
                    Slab(Hull, new Vector3(-(DoorWidth * 0.5f + rest * 0.5f), h * 0.5f, z), new Vector3(rest, h, Wall));
                    Slab(Hull, new Vector3(DoorWidth * 0.5f + rest * 0.5f, h * 0.5f, z), new Vector3(rest, h, Wall));
                }
                Slab(Hull, new Vector3(0f, (DoorHeight + h) * 0.5f, z), new Vector3(DoorWidth, h - DoorHeight, Wall));
                DoorFrame(new Vector3(0f, 0f, z), Vector3.right, Vector3.forward * end);
            }

            // A white-edged frame and a yellow-black threshold around a doorway
            private void DoorFrame(Vector3 at, Vector3 along, Vector3 outward)
            {
                Vector3 across = Vector3.Cross(Vector3.up, along);
                Vector3 Size3(float a, float up, float o) => along * a + Vector3.up * up + new Vector3(Mathf.Abs(across.x), 0f, Mathf.Abs(across.z)) * o;
                Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
                foreach (float s in new[] { -1f, 1f })
                {
                    Armor.Box(at + along * s * (DoorWidth * 0.5f + 0.04f) + Vector3.up * DoorHeight * 0.5f, Abs(Size3(0.08f, DoorHeight, Wall + 0.06f)));
                    Light.Box(at + along * s * (DoorWidth * 0.5f) + Vector3.up * DoorHeight * 0.5f + outward * 0.0f, Abs(Size3(0.012f, DoorHeight - 0.1f, Wall + 0.08f)));
                }
                Armor.Box(at + Vector3.up * (DoorHeight + 0.04f), Abs(Size3(DoorWidth + 0.16f, 0.08f, Wall + 0.06f)));
                for (int i = 0; i < 6; i++)
                {
                    MeshBuilder stripe = i % 2 == 0 ? Amber : Dark;
                    stripe.Box(at + along * (-DoorWidth * 0.5f + DoorWidth * (i + 0.5f) / 6f) + Vector3.up * (Wall + 0.004f), Abs(Size3(DoorWidth / 6f, 0.008f, Wall)));
                }
            }

            public void Attach(Transform visuals, string name, Vector3 offset)
            {
                int layer = visuals.gameObject.layer;
                if (offset != Vector3.zero)
                {
                    var holder = new GameObject("TFA_" + name + "Frame") { layer = layer };
                    holder.transform.SetParent(visuals, false);
                    holder.transform.localPosition = offset;
                    visuals = holder.transform;
                }
                Material hull = HullLook.Material();
                Add(visuals, layer, "TFA_" + name + "Hull", Hull, hull, new Vector2(0.125f, 0.5f));
                Add(visuals, layer, "TFA_" + name + "Inner", Inner, hull, new Vector2(0.125f, 0.5f));
                Add(visuals, layer, "TFA_" + name + "Crates", Crate, hull, new Vector2(0.125f, 0.5f));
                GameSkin.AddMesh(visuals, layer, "TFA_" + name + "Armor", Armor, LitMaterials.Get("TFA_MachineArmor", new Color(0.42f, 0.43f, 0.46f), 0.55f, 0.45f));
                Add(visuals, layer, "TF_NoPaint_" + name + "Dark", Dark, LitMaterials.Get("TFA_MachineDark", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0.5f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Glow", Glow, LitMaterials.Get("TFA_ModuleGlow", MachineModels.Cyan, 0f, 0.8f, MachineModels.Cyan * 1.2f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Light", Light, LitMaterials.Get("TFA_MachineLight", Color.white, 0f, 0.8f, Color.white * 1.2f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Red", Red, LitMaterials.Get("TFA_ModuleRed", new Color(0.9f, 0.1f, 0.1f), 0f, 0.5f, new Color(0.4f, 0.02f, 0.02f)), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Amber", Amber, LitMaterials.Get("TFA_ModuleAmber", new Color(1f, 0.7f, 0.15f), 0f, 0.6f, new Color(0.6f, 0.35f, 0.05f)), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Fabric", Fabric, LitMaterials.Get("TFA_ModuleFabric", new Color(0.25f, 0.35f, 0.55f), 0f, 0.15f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Glass", Glass, HullLook.Glass(), new Vector2(0.125f, 0.5f), ShadowCastingMode.Off);
            }

            private static void Add(Transform parent, int layer, string name, MeshBuilder builder, Material material, Vector2? uv1, ShadowCastingMode shadows = ShadowCastingMode.On)
            {
                if (!builder.HasGeometry || material == null)
                    return;
                var child = new GameObject(name) { layer = layer };
                child.transform.SetParent(parent, false);
                child.AddComponent<MeshFilter>().sharedMesh = builder.Build(name, uv1);
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = shadows;
            }

            /// <summary>The colliders, as the game's dynamic collider object of the part.</summary>
            public void AttachColliders(GameObject prefab, Vector3 offset)
            {
                var root = new GameObject("TFA_ModuleColliders");
                root.transform.SetParent(prefab.transform, false);
                int ship = LayerMask.NameToLayer("Ship");
                root.layer = ship;
                for (int i = 0; i < _colliders.Count; i++)
                {
                    var child = new GameObject("Box" + i) { layer = ship, tag = "ShipFloatingPart" };
                    child.transform.SetParent(root.transform, false);
                    var box = child.AddComponent<BoxCollider>();
                    box.center = _colliders[i].center + offset;
                    box.size = _colliders[i].size;
                }
                root.AddComponent<ModuleColliders>();
            }
        }
    }

    /// <summary>A station module's own colliders (walls around its doorways), handed to the game as a dynamic collider.</summary>
    internal sealed class ModuleColliders : MonoBehaviour, IShipPartDynamicCollider
    {
        public GameObject GameObject => gameObject;

        public void SetNetworkState(IStatefulPartState state)
        {
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
        }
    }

    /// <summary>The hull's own materials (texture array chosen per vertex, and its glass) for mod models.</summary>
    internal static class HullLook
    {
        private static Material _hull, _glass;

        public static Material Material()
        {
            if (_hull != null)
                return _hull;
            UnityEngine.Material source = Resources.FindObjectsOfTypeAll<UnityEngine.Material>().FirstOrDefault(m => m != null && m.name == "HullMetalFlat");
            if (source == null)
                return LitMaterials.Get("TFA_HullFallback", new Color(0.6f, 0.62f, 0.65f), 0.6f, 0.4f);
            _hull = new UnityEngine.Material(source) { name = "TFA_HullLook" };
            if (_hull.HasProperty("_UseArray"))
                _hull.SetInt("_UseArray", 1);
            return _hull;
        }

        public static Material Glass()
        {
            if (_glass != null)
                return _glass;
            UnityEngine.Material source = Resources.FindObjectsOfTypeAll<UnityEngine.Material>().FirstOrDefault(m => m != null && m.name == "HullGlass");
            _glass = source != null ? new UnityEngine.Material(source) { name = "TFA_Glass" } : LitMaterials.Get("TFA_GlassFallback", new Color(0.6f, 0.85f, 0.95f), 0f, 0.95f);
            return _glass;
        }
    }
}
