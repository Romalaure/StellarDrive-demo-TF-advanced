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
        public const ushort AirlockId = 7382, CornerId = 7383, TeeId = 7384, UTurnId = 7385, StairsId = 7386, LongCorridorId = 7387;
        public const ushort ControlSeatId = 7388, DomeId = 7389, TubeId = 7390;

        /// <summary>A module's outline: a box, a round room under a dome, or a round tube.</summary>
        internal enum Shape { Box, Dome, Tube }
        public const float Wall = 0.15f, DoorWidth = 1.6f, DoorHeight = 2.2f;
        /// <summary>The habitation console, on the front wall between the doorway and the right wall.</summary>
        public const float ConsoleX = 1.33f;

        [Flags]
        internal enum Side { None = 0, Front = 1, Back = 2, Left = 4, Right = 8 }

        /// <summary>A doorway: its wall, its center along that wall (meters from the wall's middle, toward +X or +Z) and its floor level.</summary>
        internal struct Door
        {
            public Side Side;
            public float Offset;
            public int Level;

            public Door(Side side, float offset = 0f, int level = 0)
            {
                Side = side;
                Offset = offset;
                Level = level;
            }
        }

        /// <summary>Outward normal of a wall in the module's frame.</summary>
        public static Vector3 Normal(Side side) =>
            side == Side.Front ? Vector3.forward : side == Side.Back ? Vector3.back : side == Side.Left ? Vector3.left : Vector3.right;

        /// <summary>The axis a wall's door offsets are measured along.</summary>
        public static Vector3 Along(Side side) => side == Side.Front || side == Side.Back ? Vector3.right : Vector3.forward;

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
            /// <summary>Doorways, when not one centered on each side of <see cref="Openings"/>.</summary>
            public Door[] Doors;
            /// <summary>Floors stacked in the module (a stairwell has two), each Size.y / Levels high.</summary>
            public int Levels = 1;
            /// <summary>Windows in the end walls too, and the window band's bottom and top above each floor.</summary>
            public bool EndWindows;
            public float WindowLow = 1.2f, WindowHigh = 1.85f;
            public Shape Shape;

            public float LevelHeight => Size.y / Levels;

            public IEnumerable<Door> AllDoors => Doors ?? new[] { Side.Back, Side.Front, Side.Left, Side.Right }.Where(d => (Openings & d) != 0).Select(d => new Door(d));
        }

        private const uint Iron = 101, Glass = 102, Aluminum = 104, Copper = 112;

        public static readonly Spec[] All =
        {
            new Spec
            {
                Id = HabitationId, Label = "Module d'habitation", Mass = 900f, Size = new Vector3(4f, 3f, 6f), Openings = Side.Front | Side.Back, Windows = true,
                Donor = "StorageChest", StateClass = "StorageState", AirSocket = 0,
                Cost = new[] { (Iron, 40), (Glass, 12), (Aluminum, 10), (Copper, 6) },
                Description = "Une pièce de station de 4 × 6 m, 3 m sous plafond, avec hublots et portes aux deux bouts. Tout y sert : la couchette du bas est un vrai lit (dormir pour passer le temps, comme le lit du jeu), l'armoire à casiers est un rangement de dix emplacements, et la console murale ouvre le wiki du mod. Pressurisée si un module de la station est relié à une conduite d'oxygène (prise de fluide sous le plancher, à l'arrière).",
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
                Description = "Carrefour de 4 × 4 m avec une porte sur chaque face pour relier jusqu'à quatre modules, et une console centrale qui ouvre le wiki du mod.",
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
                EndWindows = true, WindowLow = 0.7f, WindowHigh = 2.55f,
                Donor = "StarMap", KeepDonor = true, DonorAt = new Vector3(0f, Wall + 0.42f, 0.4f), DonorTurn = 180f, AirSocket = 3,
                Cost = new[] { (Iron, 24), (Glass, 24), (Aluminum, 8), (Copper, 8) },
                Description = "Une coupole vitrée pour regarder les étoiles : plafond de verre et grandes baies sur trois côtés, une vraie carte stellaire sur un pupitre bas (elle choisit la cible comme celle du jeu, et son menu passe d'une galaxie à l'autre), deux fauteuils et une lunette. Une porte à l'arrière.",
                Interior = Observatory
            },
            // Ways in and between: airlock, corridors that turn, a stairwell to a second floor
            new Spec
            {
                Id = AirlockId, Label = "Sas de dépressurisation", Mass = 600f, Size = new Vector3(3f, 2.6f, 3f), Openings = Side.Back | Side.Front, Row = 2,
                Donor = "Lever", KeepDonor = true, DonorAt = new Vector3(1.05f, 1.05f, -0.75f), DonorTurn = -90f, AirSocket = 1,
                Cost = new[] { (Iron, 26), (Glass, 4), (Aluminum, 8), (Copper, 8) },
                Description = "L'entrée de la base : deux portes étanches commandées par le levier du sas. Levier en bas : la porte intérieure (arrière) est ouverte, l'extérieure fermée et le sas est pressurisé ; levier en haut : la porte intérieure se ferme, l'extérieure (avant) s'ouvre et le sas se vide de son air. Jamais les deux ouvertes. La prise signal du levier donne l'état du sas.",
                Interior = Airlock
            },
            new Spec
            {
                Id = CornerId, Label = "Couloir d'angle", Mass = 420f, Size = new Vector3(4.4f, 3f, 4.4f), Row = 2,
                Doors = new[] { new Door(Side.Back), new Door(Side.Right) },
                Cost = new[] { (Iron, 12), (Aluminum, 3) },
                Description = "Coude à 90° de 4,4 × 4,4 m : une porte à l'arrière, une à droite ; assez grand pour que les deux modules qu'il relie ne se touchent pas. Tournez-le en le posant pour tourner à gauche.",
                Interior = SmallCorridor
            },
            new Spec
            {
                Id = TeeId, Label = "Couloir en T", Mass = 480f, Size = new Vector3(4.4f, 3f, 4.4f), Row = 2,
                Doors = new[] { new Door(Side.Back), new Door(Side.Left), new Door(Side.Right) },
                Cost = new[] { (Iron, 14), (Aluminum, 4) },
                Description = "Embranchement en T de 4,4 × 4,4 m : une porte à l'arrière, une de chaque côté, chacune assez loin des autres pour y accrocher un module de 4 m de large.",
                Interior = SmallCorridor
            },
            new Spec
            {
                Id = UTurnId, Label = "Couloir en U (demi-tour)", Mass = 620f, Size = new Vector3(8.6f, 3f, 2.8f), Row = 2,
                Doors = new[] { new Door(Side.Back, -2.15f), new Door(Side.Back, 2.15f) },
                Cost = new[] { (Iron, 18), (Aluminum, 5) },
                Description = "Demi-tour à 180° : deux portes sur la même face, à 4,3 m l'une de l'autre, pour accrocher deux modules de 4 m côte à côte et revenir en sens inverse.",
                Interior = UTurn
            },
            new Spec
            {
                Id = LongCorridorId, Label = "Couloir long", Mass = 700f, Size = new Vector3(2.4f, 2.6f, 8f), Openings = Side.Front | Side.Back, Windows = true, Row = 2,
                WindowLow = 1.0f, WindowHigh = 1.9f,
                Cost = new[] { (Iron, 28), (Glass, 6), (Aluminum, 8) },
                Description = "Couloir vitré de 8 m pour éloigner deux parties de la station.",
                Interior = Corridor
            },
            new Spec
            {
                Id = StairsId, Label = "Cage d'escalier", Mass = 1400f, Size = new Vector3(4f, 6f, 8f), Row = 2, Levels = 2, Windows = true,
                Doors = new[] { new Door(Side.Back, 0f, 0), new Door(Side.Front, 0f, 1) },
                Cost = new[] { (Iron, 50), (Glass, 8), (Aluminum, 12) },
                Description = "Deux étages reliés par un escalier en pente douce (31°) le long du mur gauche : une porte en bas à l'arrière, une en haut à l'avant (palier à 3 m). En entrant par le bas, l'escalier part à gauche. Les modules posés contre la porte du haut se mettent à sa hauteur.",
                Interior = Stairs
            },
            // Round modules
            new Spec
            {
                Id = DomeId, Label = "Dôme d'habitation", Mass = 1300f, Size = new Vector3(6f, 5.6f, 6f), Openings = Side.Back | Side.Front, Row = 3, Shape = Shape.Dome,
                Cost = new[] { (Iron, 50), (Glass, 16), (Aluminum, 14), (Copper, 6) },
                Description = "Une grande salle ronde de 6 m sous un dôme : mur circulaire de 2,6 m, coupole jusqu'à 5,6 m, une table ronde, des banquettes et un puits de lumière. Portes à l'arrière et à l'avant. Avec le matériau Verre du pistolet à peinture, la coupole devient transparente.",
                Interior = DomeRoom
            },
            new Spec
            {
                Id = TubeId, Label = "Couloir cylindrique", Mass = 420f, Size = new Vector3(3.4f, 3.25f, 4f), Openings = Side.Back | Side.Front, Row = 3, Shape = Shape.Tube,
                Cost = new[] { (Iron, 18), (Aluminum, 5) },
                Description = "Un couloir en tube de 4 m, rond comme un module de station orbitale, avec des arceaux lumineux.",
                Interior = TubeCorridor
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
            PartFactory.Register(ControlSeatId, "MainControlAdvanced", AdvancedTab.StationName, AdvancedTab.StationRow + 3, ControlSeat.Configure);
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
            // Furniture takes the interior color of the module's paint
            shell.Inside = true;
            spec.Interior?.Invoke(shell);
            shell.Attach(visuals, "Module" + spec.Id, spec.Offset);
            shell.AttachColliders(prefab, spec.Offset);
            if (spec.Id == HabitationId)
                Usable.Habitation(spec, prefab);
            if (spec.Id == HubId)
                Usable.Console(prefab, spec.Offset + new Vector3(0f, 1.05f, 0f), new Vector3(1.2f, 0.4f, 1.2f));
            if (spec.Id == AirlockId)
                prefab.transform.Find("TFA_ModuleColliders")?.gameObject.AddComponent<AirlockColliders>();
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
            // The console: a screen with its keyboard shelf on the front wall, beside the door
            float wallZ = z - Wall;
            s.Dark.Box(new Vector3(ConsoleX, 1.5f, wallZ - 0.015f), new Vector3(0.8f, 0.5f, 0.03f));
            s.Glow.Box(new Vector3(ConsoleX, 1.5f, wallZ - 0.035f), new Vector3(0.7f, 0.42f, 0.01f));
            s.Armor.Box(new Vector3(ConsoleX, 1.05f, wallZ - 0.15f), new Vector3(0.8f, 0.04f, 0.3f));
            s.Text(new Vector3(ConsoleX, 1.5f, wallZ - 0.045f), Quaternion.LookRotation(Vector3.forward, Vector3.up), "TERMINAL\n<size=60%>Wiki de la station</size>", 0.09f);
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
            s.Armor.Box(new Vector3(0f, Wall + 0.21f, 0.4f), new Vector3(0.9f, 0.42f, 0.8f));
            s.Light.Box(new Vector3(0f, Wall + 0.425f, -0.004f), new Vector3(0.8f, 0.01f, 0.012f));
            s.Solid(new Vector3(0f, Wall + 0.21f, 0.4f), new Vector3(0.9f, 0.42f, 0.8f));
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

        private static void SmallCorridor(Shell s)
        {
            float half = s.Size.x * 0.5f - Wall - 0.2f;
            // A grating square in the middle, lights above the doors
            for (int i = 0; i < 9; i++)
            {
                float t = -half + 2f * half * i / 8f;
                s.Dark.Box(new Vector3(t, Wall + 0.004f, 0f), new Vector3(0.03f, 0.008f, 2f * half));
                s.Dark.Box(new Vector3(0f, Wall + 0.004f, t), new Vector3(2f * half, 0.008f, 0.03f));
            }
            s.Light.Box(new Vector3(0f, s.Size.y - Wall - 0.02f, 0f), new Vector3(0.8f, 0.03f, 0.8f));
        }

        private static void UTurn(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f - Wall;
            // A partition between the two doorways, a rail around the far wall
            s.Armor.Box(new Vector3(0f, 1.1f, -z + 0.6f), new Vector3(0.12f, 2.2f, 1.2f));
            s.Solid(new Vector3(0f, 1.1f, -z + 0.6f), new Vector3(0.12f, 2.2f, 1.2f));
            s.Armor.Cylinder(new Vector3(-x + 0.08f, 1f, z - 0.08f), new Vector3(x - 0.08f, 1f, z - 0.08f), 0.025f, 0.025f, 8);
            for (int i = 0; i < 20; i++)
                s.Dark.Box(new Vector3(-x + 0.2f + i * (2f * x - 0.4f) / 19f, Wall + 0.004f, 0.3f), new Vector3(0.03f, 0.008f, 1.2f));
            s.Amber.Box(new Vector3(0f, 2.3f, -z + 0.02f), new Vector3(0.5f, 0.12f, 0.02f));
        }

        private static void Airlock(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall;
            // The lever's control box on the right wall, warning stripes, a red and a green lamp
            s.Armor.Box(new Vector3(x - 0.25f, 0.6f, -0.75f), new Vector3(0.5f, 0.9f, 0.6f));
            s.Solid(new Vector3(x - 0.25f, 0.6f, -0.75f), new Vector3(0.5f, 0.9f, 0.6f));
            s.Text(new Vector3(x - 0.51f, 0.85f, -0.75f), Quaternion.LookRotation(Vector3.right, Vector3.up), "SAS\n<size=60%>levier bas : intérieur\nlevier haut : extérieur</size>", 0.07f);
            for (int i = 0; i < 8; i++)
            {
                MeshBuilder stripe = i % 2 == 0 ? s.Amber : s.Dark;
                stripe.Box(new Vector3(-x + 0.01f, 0.3f + i * 0.25f, 0f), new Vector3(0.02f, 0.25f, 0.3f), Quaternion.Euler(45f, 0f, 0f));
            }
            s.Red.Cylinder(new Vector3(0f, 2.4f, s.Size.z * 0.5f - Wall - 0.03f), new Vector3(0f, 2.4f, s.Size.z * 0.5f - Wall - 0.1f), 0.06f, 0.06f, 10);
            s.Glow.Cylinder(new Vector3(0f, 2.4f, -s.Size.z * 0.5f + Wall + 0.03f), new Vector3(0f, 2.4f, -s.Size.z * 0.5f + Wall + 0.1f), 0.06f, 0.06f, 10);
            // The two doors (moved by AirlockDoors), and their collision boxes (by AirlockColliders)
            foreach ((Side side, string name) in new[] { (Side.Front, AirlockDoors.Outer), (Side.Back, AirlockDoors.Inner) })
            {
                float dz = (side == Side.Front ? 1f : -1f) * (s.Size.z * 0.5f - Wall * 0.5f);
                // Two leaves that slide apart into the wall
                foreach ((float half, string suffix) in new[] { (-1f, "L"), (1f, "R") })
                {
                    var leaf = new MeshBuilder { Panel = GameSkin.PlainPanel, DoubleSided = true };
                    leaf.Box(new Vector3(0f, Wall + (DoorHeight - Wall) * 0.5f, 0f), new Vector3(DoorWidth * 0.5f - 0.01f, DoorHeight - Wall, 0.06f));
                    leaf.Box(new Vector3(-half * (DoorWidth * 0.25f - 0.03f), Wall + (DoorHeight - Wall) * 0.5f, 0f), new Vector3(0.04f, DoorHeight - Wall - 0.1f, 0.08f));
                    s.Leaves.Add((name + suffix, new Vector3(half * DoorWidth * 0.25f, 0f, dz), leaf));
                }
                s.Solid(new Vector3(0f, Wall + (DoorHeight - Wall) * 0.5f, dz), new Vector3(DoorWidth, DoorHeight - Wall, Wall), Quaternion.identity, name);
            }
        }

        private static void Stairs(Shell s)
        {
            float x = s.Size.x * 0.5f - Wall, z = s.Size.z * 0.5f - Wall, level = s.Spec.LevelHeight;
            // The landing of the upper floor, over the front of the well
            // The stairs run from 1.45 m inside the back wall (room to step on them) to the landing
            float landing = z - 1.25f;
            Vector3 landingCenter = new Vector3(0f, level + Wall * 0.5f, (landing + z) * 0.5f);
            Vector3 landingSize = new Vector3(2f * x, Wall, z - landing);
            s.Hull.Box(landingCenter, landingSize);
            s.Solid(landingCenter, landingSize);
            s.Inner.Box(landingCenter + Vector3.up * (Wall * 0.5f + 0.004f), new Vector3(landingSize.x, 0.008f, landingSize.z));
            // The stairs along the left wall, up from the back to the landing's edge
            float width = 1.0f, sx = -x + width * 0.5f;
            Vector3 bottom = new Vector3(sx, Wall, -z + 1.45f), top = new Vector3(sx, level + Wall, landing);
            int steps = 18;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                Vector3 a = Vector3.Lerp(bottom, top, t0), b = Vector3.Lerp(bottom, top, t1);
                s.Armor.Box(new Vector3(sx, (a.y + b.y) * 0.5f - 0.05f, (a.z + b.z) * 0.5f), new Vector3(width, b.y - a.y + 0.1f, b.z - a.z));
                s.Amber.Box(new Vector3(sx, b.y - 0.045f, b.z - 0.01f), new Vector3(width - 0.1f, 0.01f, 0.02f));
            }
            // A ramp to walk on under the steps
            Vector3 run = top - bottom;
            float slope = Mathf.Atan2(run.y, run.z) * Mathf.Rad2Deg;
            s.Solid((bottom + top) * 0.5f - Vector3.up * 0.05f, new Vector3(width, 0.1f, run.magnitude), Quaternion.Euler(-slope, 0f, 0f));
            // Railings: along the stairs' open side and the landing's edge
            Vector3 railBottom = bottom + new Vector3(width * 0.5f, 0.9f, 0f), railTop = top + new Vector3(width * 0.5f, 0.9f, 0f);
            s.Armor.Cylinder(railBottom, railTop, 0.025f, 0.025f, 8);
            for (int i = 0; i <= 4; i++)
            {
                Vector3 post = Vector3.Lerp(bottom, top, i / 4f) + new Vector3(width * 0.5f, 0f, 0f);
                s.Armor.Cylinder(post, post + Vector3.up * 0.9f, 0.02f, 0.02f, 6);
            }
            float edge = landing + 0.03f;
            s.Armor.Cylinder(new Vector3(sx + width * 0.5f, level + Wall + 1f, edge), new Vector3(x - 0.05f, level + Wall + 1f, edge), 0.025f, 0.025f, 8);
            for (int i = 0; i <= 4; i++)
            {
                float px = Mathf.Lerp(sx + width * 0.5f, x - 0.05f, i / 4f);
                s.Armor.Cylinder(new Vector3(px, level + Wall, edge), new Vector3(px, level + Wall + 1f, edge), 0.02f, 0.02f, 6);
            }
            s.Solid(new Vector3((sx + width * 0.5f + x) * 0.5f, level + Wall + 0.5f, edge), new Vector3(x - sx - width * 0.5f, 1f, 0.06f));
            // Light under the landing and at the top
            s.Light.Box(new Vector3(0f, level - 0.02f, (landing + z) * 0.5f), new Vector3(1.2f, 0.03f, 0.3f));
            s.Light.Box(new Vector3(0f, s.Size.y - Wall - 0.02f, 0f), new Vector3(1.2f, 0.03f, 0.3f));
        }

        private static void DomeRoom(Shell s)
        {
            // A round table in the middle, benches round it, planters with glowing plants
            s.Armor.Cylinder(new Vector3(0f, Wall, 0f), new Vector3(0f, 0.72f, 0f), 0.12f, 0.12f, 12);
            s.Armor.Cylinder(new Vector3(0f, 0.72f, 0f), new Vector3(0f, 0.78f, 0f), 0.9f, 0.9f, 24);
            s.Glow.Cylinder(new Vector3(0f, 0.78f, 0f), new Vector3(0f, 0.785f, 0f), 0.85f, 0.85f, 24, capA: false, capB: false);
            s.Solid(new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.66f, 1.6f));
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Quaternion turn = Quaternion.LookRotation(dir, Vector3.up);
                // A bench facing the table, and a planter behind it against the wall
                s.Fabric.Box(dir * 1.6f + Vector3.up * 0.42f, new Vector3(1.4f, 0.1f, 0.5f), turn);
                s.Armor.Box(dir * 1.6f + Vector3.up * 0.25f, new Vector3(1.3f, 0.3f, 0.4f), turn);
                s.Fabric.Box(dir * 1.86f + Vector3.up * 0.75f, new Vector3(1.4f, 0.5f, 0.1f), turn);
                s.Solid(dir * 1.6f + Vector3.up * 0.3f, new Vector3(1.4f, 0.4f, 0.5f), turn);
                Vector3 pot = dir * 2.45f;
                s.Armor.Cylinder(pot + Vector3.up * Wall, pot + Vector3.up * 0.55f, 0.25f, 0.3f, 12);
                s.Glow.Cylinder(pot + Vector3.up * 0.55f, pot + Vector3.up * 1.2f, 0.25f, 0.02f, 8);
            }
        }

        private static void TubeCorridor(Shell s)
        {
            float r = s.Size.x * 0.5f, z = s.Size.z * 0.5f - Wall;
            // Handrails along both sides
            foreach (float side in new[] { -1f, 1f })
                s.Armor.Cylinder(new Vector3(side * (r - 0.45f), 1.0f, -z + 0.1f), new Vector3(side * (r - 0.45f), 1.0f, z - 0.1f), 0.025f, 0.025f, 8);
            for (int i = 0; i < 12; i++)
                s.Dark.Box(new Vector3(0f, Wall + 0.004f, -z + 0.15f + i * (2f * z - 0.3f) / 11f), new Vector3(1.4f, 0.008f, 0.03f));
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
            public Spec Spec { get; private set; }
            /// <summary>While true, Armor is the furniture's (painted with the module's interior color).</summary>
            public bool Inside;
            private readonly MeshBuilder _armorOut = new MeshBuilder { Panel = GameSkin.PlainPanel, DoubleSided = true };
            private readonly MeshBuilder _armorIn = new MeshBuilder { Panel = GameSkin.PlainPanel, DoubleSided = true };
            public MeshBuilder Armor => Inside ? _armorIn : _armorOut;
            /// <summary>Moving parts (airlock door leaves): name, position, model.</summary>
            public readonly List<(string Name, Vector3 At, MeshBuilder Model)> Leaves = new List<(string, Vector3, MeshBuilder)>();
            private readonly List<(Vector3 At, Quaternion Rotation, string Text, float Size)> _texts = new List<(Vector3, Quaternion, string, float)>();
            public readonly MeshBuilder Hull = new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(0.62f, 0.64f, 0.67f, 1f) };
            public readonly MeshBuilder Inner = new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(0.85f, 0.86f, 0.88f, 1f) };
            public readonly MeshBuilder Dark = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Glow = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Light = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Amber = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Red = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Fabric = new MeshBuilder { DoubleSided = true };
            public readonly MeshBuilder Crate = new MeshBuilder { PlanarScale = 1f, VertexColor = new Color(0.8f, 0.65f, 0.48f, 0f) };
            public readonly MeshBuilder Glass = new MeshBuilder { DoubleSided = true };
            private readonly List<(Bounds Box, Quaternion Rotation, string Name)> _colliders = new List<(Bounds, Quaternion, string)>();

            public Shell(Vector3 size)
            {
                Size = size;
            }

            /// <summary>A box players and ships collide with (turned, for ramps; named, for doors that open).</summary>
            public void Solid(Vector3 center, Vector3 size, Quaternion? rotation = null, string name = null) =>
                _colliders.Add((new Bounds(center, size), rotation ?? Quaternion.identity, name));

            /// <summary>A line of text on a screen or a sign, in the module's frame.</summary>
            public void Text(Vector3 at, Quaternion rotation, string text, float size) => _texts.Add((at, rotation, text, size));

            private void Slab(MeshBuilder m, Vector3 center, Vector3 size)
            {
                m.Box(center, size);
                Solid(center, size);
            }

            public void Build(Spec spec)
            {
                Spec = spec;
                if (spec.Shape != Shape.Box)
                {
                    BuildRound(spec);
                    return;
                }
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
                // Side walls (x), ends (z), around their doorways
                foreach (Side side in new[] { Side.Left, Side.Right, Side.Back, Side.Front })
                    Face(spec, side);
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

            // ---- Round outlines

            private void BuildRound(Spec spec)
            {
                if (spec.Shape == Shape.Dome)
                    BuildDome();
                else
                    BuildTube();
            }

            private static Vector3 Round(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            /// <summary>
            /// A round room: a disc floor, a circular wall 2.6 m high with a doorway portal at the back
            /// and the front, and a dome over it, all lined inside.
            /// </summary>
            private void BuildDome()
            {
                float r = Size.x * 0.5f;
                const float wallTop = 2.6f;
                const int sides = 24, bands = 6;
                float step = Mathf.PI * 2f / sides;

                // Floor: a disc, solid as twelve slices from the middle to the rim
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = Round(i * step) * r, b = Round((i + 1) * step) * r;
                    Hull.Face(new Vector3(0f, Wall, 0f), a + Vector3.up * Wall, b + Vector3.up * Wall, new Vector3(0f, Wall, 0f), Vector3.up);
                    Hull.Face(Vector3.zero, a, b, Vector3.zero, Vector3.down);
                    Hull.Face(a, b, b + Vector3.up * Wall, a + Vector3.up * Wall, (a + b).normalized);
                    Vector3 ai = Round(i * step) * (r - Wall), bi = Round((i + 1) * step) * (r - Wall);
                    Inner.Face(new Vector3(0f, Wall + 0.006f, 0f), ai + Vector3.up * (Wall + 0.006f), bi + Vector3.up * (Wall + 0.006f), new Vector3(0f, Wall + 0.006f, 0f), Vector3.up);
                }
                for (int i = 0; i < 12; i++)
                {
                    float angle = (i + 0.5f) * Mathf.PI * 2f / 12f;
                    Vector3 dir = Round(angle);
                    Solid(dir * r * 0.5f + Vector3.up * Wall * 0.5f, new Vector3(2f * r * Mathf.Sin(Mathf.PI / 12f) * 1.05f, Wall, r), Quaternion.LookRotation(dir, Vector3.up));
                }

                // The circular wall, open where the doorways are
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * step, a1 = (i + 1) * step;
                    Vector3 mid = Round((a0 + a1) * 0.5f);
                    if (Mathf.Abs(mid.x * r) < 0.6f)
                        continue;
                    RingPanel(r, a0, a1, 0f, wallTop);
                }
                foreach (float end in new[] { -1f, 1f })
                {
                    // A straight portal around each doorway, deep enough to meet the curve
                    float z = end * (r - 0.2f);
                    foreach (float side in new[] { -1f, 1f })
                        Slab(Hull, new Vector3(side * 0.99f, wallTop * 0.5f, z), new Vector3(0.38f, wallTop, 0.4f));
                    Slab(Hull, new Vector3(0f, (DoorHeight + wallTop) * 0.5f, z), new Vector3(1.6f, wallTop - DoorHeight, 0.4f));
                    // The portal's inside face takes the interior look
                    float inside = end * (r - 0.4f - 0.006f);
                    foreach (float side in new[] { -1f, 1f })
                        Inner.Box(new Vector3(side * 0.99f, (Wall + wallTop) * 0.5f, inside), new Vector3(0.36f, wallTop - Wall, 0.008f));
                    Inner.Box(new Vector3(0f, (DoorHeight + wallTop) * 0.5f, inside), new Vector3(1.6f, wallTop - DoorHeight, 0.008f));
                    DoorFrame(new Vector3(0f, 0f, end * (r - Wall * 0.5f)), Vector3.right, Vector3.forward * end);
                }

                // The dome: bands of panels from the wall's top to the crown
                for (int k = 0; k < bands; k++)
                {
                    float p0 = k * Mathf.PI * 0.5f / bands, p1 = (k + 1) * Mathf.PI * 0.5f / bands;
                    for (int i = 0; i < sides; i++)
                    {
                        float a0 = i * step, a1 = (i + 1) * step;
                        Vector3 Point(float a, float p, float radius) => Round(a) * Mathf.Cos(p) * radius + Vector3.up * (wallTop + Mathf.Sin(p) * radius);
                        Vector3 o00 = Point(a0, p0, r), o10 = Point(a1, p0, r), o11 = Point(a1, p1, r), o01 = Point(a0, p1, r);
                        Vector3 center = (o00 + o10 + o11 + o01) * 0.25f;
                        Vector3 normal = (center - Vector3.up * wallTop).normalized;
                        Hull.Face(o00, o10, o11, o01, normal);
                        float ri = r - Wall;
                        Inner.Face(Point(a0, p0, ri - 0.006f), Point(a1, p0, ri - 0.006f), Point(a1, p1, ri - 0.006f), Point(a0, p1, ri - 0.006f), -normal);
                        Vector3 tangent = ((o01 + o11) - (o00 + o10)).normalized;
                        float width = Mathf.Max((o10 - o00).magnitude, 0.05f), height = ((o01 + o11) - (o00 + o10)).magnitude * 0.5f;
                        Solid(center - normal * Wall * 0.5f, new Vector3(width * 1.04f, height * 1.04f, Wall), Quaternion.LookRotation(normal, tangent));
                    }
                }
                // A ring of light round the crown, a white band at the wall's top outside
                Light.Cylinder(new Vector3(0f, wallTop + r - 0.35f, 0f), new Vector3(0f, wallTop + r - 0.33f, 0f), 0.6f, 0.6f, 20, capA: false, capB: false);
                _armorOut.Cylinder(new Vector3(0f, wallTop - 0.05f, 0f), new Vector3(0f, wallTop + 0.05f, 0f), r + 0.03f, r + 0.03f, sides, capA: false, capB: false);
                Glow.Cylinder(new Vector3(0f, Wall + 0.04f, 0f), new Vector3(0f, Wall + 0.06f, 0f), r - Wall - 0.02f, r - Wall - 0.02f, sides, capA: false, capB: false);
            }

            // One panel of a vertical circular wall, lined inside, and its collision
            private void RingPanel(float r, float a0, float a1, float y0, float y1)
            {
                Vector3 p0 = Round(a0), p1 = Round(a1), mid = Round((a0 + a1) * 0.5f);
                Hull.Face(p0 * r + Vector3.up * y0, p1 * r + Vector3.up * y0, p1 * r + Vector3.up * y1, p0 * r + Vector3.up * y1, mid);
                float ri = r - Wall - 0.006f;
                float a = Mathf.Max(y0, Wall), b = Mathf.Min(y1, Size.y - Wall);
                Inner.Face(p0 * ri + Vector3.up * a, p1 * ri + Vector3.up * a, p1 * ri + Vector3.up * b, p0 * ri + Vector3.up * b, -mid);
                Hull.Face(p0 * (r - Wall) + Vector3.up * y1, p1 * (r - Wall) + Vector3.up * y1, p1 * r + Vector3.up * y1, p0 * r + Vector3.up * y1, Vector3.up);
                Solid(mid * (r - Wall * 0.5f) + Vector3.up * (y0 + y1) * 0.5f, new Vector3((p1 - p0).magnitude * r * 1.04f, y1 - y0, Wall), Quaternion.LookRotation(mid, Vector3.up));
            }

            /// <summary>
            /// A round tube along Z with a flat floor, end walls round a doorway at each end, and rings
            /// of light inside.
            /// </summary>
            private void BuildTube()
            {
                float r = Size.x * 0.5f, l = Size.z, cy = Size.y - r;
                const int sides = 20;
                float step = Mathf.PI * 2f / sides;
                Vector3 Ring(float a, float radius, float z) => new Vector3(Mathf.Cos(a) * radius, Mathf.Max(0f, cy + Mathf.Sin(a) * radius), z);

                // Floor
                float floorHalf = Mathf.Sqrt(Mathf.Max(0.1f, r * r - (cy - Wall) * (cy - Wall)));
                Slab(Hull, new Vector3(0f, Wall * 0.5f, 0f), new Vector3(2f * floorHalf, Wall, l));
                Inner.Box(new Vector3(0f, Wall + 0.004f, 0f), new Vector3(2f * floorHalf - 0.1f, 0.008f, l - 0.02f));

                // The shell, above the floor
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * step, a1 = (i + 1) * step, am = (a0 + a1) * 0.5f;
                    if (cy + Mathf.Sin(am) * r < Wall + 0.1f)
                        continue;
                    Vector3 radial = new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f);
                    Hull.Face(Ring(a0, r, -l * 0.5f), Ring(a1, r, -l * 0.5f), Ring(a1, r, l * 0.5f), Ring(a0, r, l * 0.5f), radial);
                    float ri = r - Wall - 0.006f;
                    Inner.Face(Ring(a0, ri, -l * 0.5f + Wall), Ring(a1, ri, -l * 0.5f + Wall), Ring(a1, ri, l * 0.5f - Wall), Ring(a0, ri, l * 0.5f - Wall), -radial);
                    Vector3 center = new Vector3(0f, cy, 0f) + radial * (r - Wall * 0.5f);
                    Solid(center, new Vector3(2f * r * Mathf.Sin(step * 0.5f) * 1.06f, Wall, l), Quaternion.LookRotation(Vector3.forward, radial));
                }

                // End walls: from the round shell to the doorway's edge, at both ends
                float doorHalf = DoorWidth * 0.5f, doorTop = DoorHeight;
                Vector3 doorCenter = new Vector3(0f, (Wall + doorTop) * 0.5f, 0f);
                float doorHalfHeight = (doorTop - Wall) * 0.5f;
                foreach (float end in new[] { -1f, 1f })
                {
                    float zo = end * l * 0.5f, zi = end * (l * 0.5f - Wall);
                    for (int i = 0; i < sides; i++)
                    {
                        float a0 = i * step, a1 = (i + 1) * step;
                        Vector3 r0 = Ring(a0, r, 0f), r1 = Ring(a1, r, 0f);
                        Vector3 d0 = DoorEdge(r0, doorCenter, doorHalf, doorHalfHeight), d1 = DoorEdge(r1, doorCenter, doorHalf, doorHalfHeight);
                        Hull.Face(r0 + Vector3.forward * zo, r1 + Vector3.forward * zo, d1 + Vector3.forward * zo, d0 + Vector3.forward * zo, Vector3.forward * end);
                        Inner.Face(r0 * 0.97f + Vector3.forward * zi, r1 * 0.97f + Vector3.forward * zi, d1 + Vector3.forward * zi, d0 + Vector3.forward * zi, -Vector3.forward * end);
                    }
                    float zc = end * (l * 0.5f - Wall * 0.5f);
                    foreach (float side in new[] { -1f, 1f })
                        Solid(new Vector3(side * (r + doorHalf) * 0.5f, Size.y * 0.5f, zc), new Vector3(r - doorHalf, Size.y, Wall));
                    Solid(new Vector3(0f, (doorTop + Size.y) * 0.5f, zc), new Vector3(DoorWidth, Size.y - doorTop, Wall));
                    DoorFrame(new Vector3(0f, 0f, zc), Vector3.right, Vector3.forward * end);
                }
                // Rings of light inside, ribs outside
                for (int k = 0; k < 3; k++)
                {
                    float z = -l * 0.5f + l * (k + 1) / 4f;
                    Glow.Cylinder(new Vector3(0f, cy, z), new Vector3(0f, cy, z + 0.05f), r - Wall - 0.02f, r - Wall - 0.02f, sides, capA: false, capB: false);
                    _armorOut.Cylinder(new Vector3(0f, cy, z - 0.06f), new Vector3(0f, cy, z + 0.06f), r + 0.05f, r + 0.05f, sides, capA: false, capB: false);
                }
            }

            // Where a line from the doorway's middle toward a point meets the doorway's edge
            private static Vector3 DoorEdge(Vector3 toward, Vector3 center, float halfWidth, float halfHeight)
            {
                Vector3 d = toward - center;
                float tx = Mathf.Abs(d.x) > 1e-4f ? halfWidth / Mathf.Abs(d.x) : float.MaxValue;
                float ty = Mathf.Abs(d.y) > 1e-4f ? halfHeight / Mathf.Abs(d.y) : float.MaxValue;
                return center + d * Mathf.Min(1f, Mathf.Min(tx, ty));
            }


            /// <summary>
            /// One wall, level by level: full panels (or a band of windows) between the doorways,
            /// a lintel over each doorway, the inside lined with light panels everywhere.
            /// </summary>
            private void Face(Spec spec, Side side)
            {
                float w = Size.x, h = Size.y, l = Size.z;
                bool sideWall = side == Side.Left || side == Side.Right;
                float length = sideWall ? l : w - 2f * Wall;
                float levelHeight = spec.LevelHeight;
                bool windows = sideWall ? spec.Windows : spec.EndWindows;
                var doors = spec.AllDoors.Where(d => d.Side == side).ToList();
                for (int level = 0; level < spec.Levels; level++)
                {
                    float y0 = level * levelHeight, y1 = y0 + levelHeight;
                    float u = -length * 0.5f;
                    foreach (Door door in doors.Where(d => d.Level == level).OrderBy(d => d.Offset))
                    {
                        float a = door.Offset - DoorWidth * 0.5f, b = door.Offset + DoorWidth * 0.5f;
                        Band(spec, side, u, a, y0, y1, windows);
                        Panel(side, a, b, y0 + DoorHeight, y1);
                        DoorFrame(At(side, door.Offset, y0, 0f), Along(side), Normal(side));
                        u = b;
                    }
                    Band(spec, side, u, length * 0.5f, y0, y1, windows);
                }
            }

            // A wall's point: u along the wall, v up, d inward from the wall's middle
            private Vector3 At(Side side, float u, float v, float d)
            {
                float w = Size.x, l = Size.z;
                Vector3 n = Normal(side);
                Vector3 middle = side == Side.Left || side == Side.Right ? n * (w * 0.5f - Wall * 0.5f) : n * (l * 0.5f - Wall * 0.5f);
                return middle + Along(side) * u + Vector3.up * v - n * d;
            }

            private static Vector3 Extent(Side side, float u, float v, float d)
            {
                Vector3 along = Along(side), n = Normal(side);
                Vector3 e = along * u + Vector3.up * v + n * d;
                return new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
            }

            // Between doorways: a plain wall, or a wall with a band of windows if it is wide enough
            private void Band(Spec spec, Side side, float u0, float u1, float y0, float y1, bool windows)
            {
                if (u1 - u0 < 0.02f)
                    return;
                float low = y0 + spec.WindowLow, high = Mathf.Min(y0 + spec.WindowHigh, y1 - 0.1f);
                if (!windows || u1 - u0 < 0.8f || high <= low)
                {
                    Panel(side, u0, u1, y0, y1);
                    return;
                }
                Panel(side, u0, u1, y0, low);
                Panel(side, u0, u1, high, y1);
                int panes = Mathf.Max(1, Mathf.RoundToInt((u1 - u0) / 1.5f));
                for (int i = 0; i <= panes; i++)
                {
                    float u = Mathf.Clamp(u0 + (u1 - u0) * i / panes, u0 + 0.06f, u1 - 0.06f);
                    Slab(_armorOut, At(side, u, (low + high) * 0.5f, 0f), Extent(side, 0.12f, high - low, Wall + 0.02f));
                }
                Glass.Box(At(side, (u0 + u1) * 0.5f, (low + high) * 0.5f, 0f), Extent(side, u1 - u0 - 0.1f, high - low, 0.03f));
                Solid(At(side, (u0 + u1) * 0.5f, (low + high) * 0.5f, 0f), Extent(side, u1 - u0, high - low, Wall));
            }

            // A solid piece of wall, lined inside
            private void Panel(Side side, float u0, float u1, float v0, float v1)
            {
                if (u1 - u0 < 0.02f || v1 - v0 < 0.02f)
                    return;
                Slab(Hull, At(side, (u0 + u1) * 0.5f, (v0 + v1) * 0.5f, 0f), Extent(side, u1 - u0, v1 - v0, Wall));
                // The lining stops at the floor and ceiling slabs
                float a = Mathf.Max(v0, Wall), b = Mathf.Min(v1, Size.y - Wall);
                if (b - a > 0.02f)
                    Inner.Box(At(side, (u0 + u1) * 0.5f, (a + b) * 0.5f, Wall * 0.5f + 0.006f), Extent(side, u1 - u0 - 0.01f, b - a, 0.008f));
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
                Material armor = LitMaterials.Get("TFA_MachineArmor", new Color(0.42f, 0.43f, 0.46f), 0.55f, 0.45f);
                // "TF_PaintInner" renderers take the interior color of the TF paint tool, the others the exterior one
                Variants(visuals, layer, "TFA_" + name + "Hull", Hull, false);
                Variants(visuals, layer, name + "Inner", Inner, true);
                Add(visuals, layer, "TF_NoPaint_" + name + "Crates", Crate, hull, new Vector2(0.125f, 0.5f));
                GameSkin.AddMesh(visuals, layer, "TFA_" + name + "Armor", _armorOut, armor);
                if (_armorIn.HasGeometry)
                    GameSkin.AddMesh(visuals, layer, PaintInner + name + "Furniture", _armorIn, armor);
                foreach ((string leafName, Vector3 at, MeshBuilder leaf) in Leaves)
                {
                    var holder = new GameObject(leafName) { layer = layer };
                    holder.transform.SetParent(visuals, false);
                    holder.transform.localPosition = at;
                    GameSkin.AddMesh(holder.transform, layer, PaintInner + name + leafName, leaf, armor);
                }
                if (Leaves.Count > 0)
                    visuals.gameObject.AddComponent<AirlockDoors>();
                foreach ((Vector3 at, Quaternion rotation, string text, float size) in _texts)
                {
                    var sign = new GameObject("TF_NoPaint_" + name + "Text") { layer = layer };
                    sign.transform.SetParent(visuals, false);
                    sign.transform.localPosition = at;
                    // A TextMeshPro reads from behind its forward: `rotation` looks from the reader into the screen
                    sign.transform.localRotation = rotation;
                    var tmp = sign.AddComponent<TMPro.TextMeshPro>();
                    tmp.font = ShipComputer.ComputerStyle.Font;
                    tmp.text = text;
                    tmp.fontSize = size * 10f;
                    tmp.color = new Color(0.6f, 0.95f, 1f);
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
                }
                Add(visuals, layer, "TF_NoPaint_" + name + "Dark", Dark, LitMaterials.Get("TFA_MachineDark", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0.5f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Glow", Glow, LitMaterials.Get("TFA_ModuleGlow", MachineModels.Cyan, 0f, 0.8f, MachineModels.Cyan * 1.2f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Light", Light, LitMaterials.Get("TFA_MachineLight", Color.white, 0f, 0.8f, Color.white * 1.2f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Red", Red, LitMaterials.Get("TFA_ModuleRed", new Color(0.9f, 0.1f, 0.1f), 0f, 0.5f, new Color(0.4f, 0.02f, 0.02f)), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Amber", Amber, LitMaterials.Get("TFA_ModuleAmber", new Color(1f, 0.7f, 0.15f), 0f, 0.6f, new Color(0.6f, 0.35f, 0.05f)), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Fabric", Fabric, LitMaterials.Get("TFA_ModuleFabric", new Color(0.25f, 0.35f, 0.55f), 0f, 0.15f), null);
                Add(visuals, layer, "TF_NoPaint_" + name + "Glass", Glass, HullLook.Glass(), new Vector2(0.125f, 0.5f), ShadowCastingMode.Off);
            }

            public const string PaintInner = "TF_PaintInner_";

            /// <summary>
            /// The walls in every material the TF paint tool offers ("TF_MatVariant_N_" renderers, only
            /// the chosen one drawn): 0 the module's own look, 1 light metal, 2 wood, 3 dark metal,
            /// 4 white, 5 glass.
            /// </summary>
            private static void Variants(Transform parent, int layer, string name, MeshBuilder builder, bool interior)
            {
                if (!builder.HasGeometry)
                    return;
                Material hull = HullLook.Material();
                Mesh mesh = builder.Build(name, new Vector2(0.125f, 0.5f));
                // Vertex color: the tint, and in alpha the hull texture (0 wood, 1 metal)
                var looks = new (int Variant, Color? Color, Material Material)[]
                {
                    (0, null, hull),
                    (1, new Color(0.85f, 0.87f, 0.9f, 1f), hull),
                    (2, new Color(0.78f, 0.62f, 0.45f, 0f), hull),
                    (3, new Color(0.3f, 0.31f, 0.33f, 1f), hull),
                    (4, new Color(1f, 1f, 1f, 1f), hull),
                    (5, new Color(1f, 1f, 1f, 1f), HullLook.Glass()),
                };
                foreach ((int variant, Color? color, Material material) in looks)
                {
                    Mesh look = mesh;
                    if (color.HasValue)
                    {
                        look = Object.Instantiate(mesh);
                        look.name = name + "_" + variant;
                        look.colors = Enumerable.Repeat(color.Value, mesh.vertexCount).ToArray();
                    }
                    string objectName = (interior ? PaintInner : "") + "TF_MatVariant_" + variant + "_" + name;
                    var child = new GameObject(objectName) { layer = layer };
                    child.transform.SetParent(parent, false);
                    child.AddComponent<MeshFilter>().sharedMesh = look;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = variant == 5 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.enabled = variant == 0;
                }
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
                    (Bounds bounds, Quaternion rotation, string name) = _colliders[i];
                    var child = new GameObject(name ?? "Box" + i) { layer = ship, tag = "ShipFloatingPart" };
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = bounds.center + offset;
                    child.transform.localRotation = rotation;
                    var box = child.AddComponent<BoxCollider>();
                    box.size = bounds.size;
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
            GetComponent<AirlockColliders>()?.Apply(state);
        }

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            GetComponent<AirlockColliders>()?.Apply(change.NewState);
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
