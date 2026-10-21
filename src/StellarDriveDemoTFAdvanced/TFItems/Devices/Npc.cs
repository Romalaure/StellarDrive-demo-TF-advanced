using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using Items.Model;
using Players;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The inhabitants of the trading station, service robots: an engineer (7482) who fills the
    /// tanks of your ship and a doctor (7483) who heals you and fills your suit's oxygen, for a few
    /// base resources. They stand on a small lit pad; look at one and press the use key. The host
    /// checks that the player stands near one, takes the price from their inventory and does the
    /// work. Not in the build menu: the station places them, and one there cannot be picked up nor
    /// recycled until a player signs its contract (20 stellar shards); then it can be carried away
    /// like any part, and moved freely once off the station.
    /// </summary>
    internal static class Npc
    {
        public const ushort EngineerId = 7482, DoctorId = 7483;
        private const float Reach = 8f, ShipReach = 300f;
        private static readonly Vector3 BoundsCenter = new Vector3(0f, 0.95f, 0f);
        private static readonly Vector3 BoundsSize = new Vector3(0.8f, 1.9f, 0.8f);

        internal sealed class Service
        {
            public ushort Npc;
            public string Name, Description;
            public uint Item;
            public int Count;
        }

        public static Service[] Services => new[]
        {
            new Service { Npc = DoctorId, Name = Loc.Pick("Soins complets", "Full care", "Cuidados completos"),
                Description = Loc.Pick("Toute ta santé et l'oxygène de ta combinaison.", "All your health back and your suit's oxygen full.", "Toda tu salud y el oxígeno de tu traje."), Item = 112, Count = 3 },
            new Service { Npc = EngineerId, Name = Loc.Pick("Plein des réservoirs", "Fill the tanks", "Llenar los depósitos"),
                Description = Loc.Pick("Remplit chaque réservoir de ton vaisseau le plus proche avec le fluide qu'il contient déjà (oxygène, carburant, éthanol…).", "Fills every tank of your nearest ship with the fluid it already holds (oxygen, fuel, ethanol…).", "Llena cada depósito de tu nave más cercana con el fluido que ya contiene (oxígeno, combustible, etanol…)."), Item = 101, Count = 10 },
            new Service { Npc = EngineerId, Name = Loc.Pick("Recharge des batteries", "Charge the batteries", "Cargar las baterías"),
                Description = Loc.Pick("Recharge à fond les batteries du mod de ton vaisseau le plus proche.", "Fully charges the mod's batteries on your nearest ship.", "Carga a tope las baterías del mod de tu nave más cercana."), Item = 112, Count = 6 },
            new Service { Npc = DoctorId, Name = ContractName, Description = ContractText, Item = Shard, Count = ContractPrice },
            new Service { Npc = EngineerId, Name = ContractName, Description = ContractText, Item = Shard, Count = ContractPrice }
        };

        private const uint Shard = 103;
        private const int ContractPrice = 20;
        private static string ContractName => Loc.Pick("Contrat d'embauche", "Hiring contract", "Contrato de contratación");
        private static string ContractText => Loc.Pick("Il accepte de te suivre : tu peux ensuite le prendre (comme une pièce) et l'installer dans ton vaisseau.",
            "They agree to come with you: you can then pick them up (like a part) and set them up in your ship.",
            "Acepta seguirte: luego puedes cogerlo (como una pieza) e instalarlo en tu nave.");

        /// <summary>Host: the inhabitants of a station whose contract was signed (saved with the world).</summary>
        public static readonly HashSet<PartKey> Contracts = new HashSet<PartKey>();

        public static bool IsNpc(ushort settingsId) => settingsId == EngineerId || settingsId == DoctorId;

        // A station of the mod (its beacon has a station's name): its inhabitants stay unless hired
        private static bool IsStation(TrackedShipServer ship) => ship != null
            && ship.GetAllStatefulParts<BeaconState>().Any(p => Worlds.AbandonedStations.All.Any(spec => spec.Name == ((BeaconState)p.State).Name));

        /// <summary>Host: whether this part is an inhabitant held at its station (no contract signed).</summary>
        public static bool Held(TrackedShipServer ship, StatefulPart part) => part?.Settings != null && IsNpc(part.Settings.id)
            && IsStation(ship) && !Contracts.Contains(new PartKey(ship.Id, part.Id));

        public static void Tell(NetworkConnection connection, string text)
        {
            if (connection != null)
                TFNet.SendTo(connection, TFMessageKind.NpcServiceResult, 0, 0, text);
        }

        public static string HeldMessage => Loc.Pick($"Cet habitant travaille ici : signe son contrat d'embauche ({ContractPrice} éclats stellaires) pour l'emmener.",
            $"This inhabitant works here: sign their hiring contract ({ContractPrice} stellar shards) to take them along.",
            $"Este habitante trabaja aquí: firma su contrato ({ContractPrice} fragmentos estelares) para llevártelo.");

        public static string Title(ushort npc) => npc == DoctorId ? Loc.Pick("Docteure Lyra (médecin)", "Doctor Lyra (medic)", "Doctora Lyra (médica)") : Loc.Pick("Kade (ingénieur)", "Kade (engineer)", "Kade (ingeniero)");

        public static void Register()
        {
            Lights.LampParts.RegisterDonorPart(EngineerId, 0, (s, p) => Configure(s, p, EngineerId), "");
            Lights.LampParts.RegisterDonorPart(DoctorId, 0, (s, p) => Configure(s, p, DoctorId), "");
            TFMod.Log.Msg("registered the station's inhabitants");
        }

        public static void Install()
        {
            StellarDriveDemoTFAdvanced.Common.DevHarness.Steps.Add((95f, "station inhabitants", () =>
            {
                if (StellarDriveDemoTFAdvanced.Common.Settings.DevVisitStationSeed.Value == 131)
                    MelonLoader.MelonCoroutines.Start(DevVisit());
            }));
            TFNet.OnServer(TFMessageKind.NpcService, ServerService);
            TFNet.OnClient(TFMessageKind.NpcServiceResult, (kind, a, b, text) => NpcMenu.Result(text));
        }

        private static void Configure(PartSettings settings, GameObject prefab, ushort id)
        {
            // A low round pad with a lit ring (the person is made when a player comes near)
            var pad = new MeshBuilder();
            pad.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.04f, 0f), 0.42f, 0.4f, 24);
            var ring = new MeshBuilder();
            ring.Cylinder(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.046f, 0f), 0.36f, 0.36f, 24, false, false);
            Color glow = id == DoctorId ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.65f, 0.2f);
            Transform visuals = DeviceModels.Build(settings, prefab, Title(id),
                Loc.Pick("Un habitant de la station : regarde-le et appuie sur la touche d'utilisation (T).", "An inhabitant of the station: look at them and press the use key (T).", "Un habitante de la estación: míralo y pulsa la tecla de uso (T)."),
                80f, new[] { (101u, 1) }, BoundsCenter, BoundsSize,
                new Dictionary<string, MeshBuilder> { ["TF_NoPaint_NpcPad"] = pad, ["TF_NoPaint_NpcRing"] = ring },
                new Dictionary<string, DeviceMaterial>
                {
                    ["TF_NoPaint_NpcPad"] = new DeviceMaterial(new Color(0.12f, 0.13f, 0.15f), 0.4f, 0.4f),
                    ["TF_NoPaint_NpcRing"] = new DeviceMaterial(glow, 0f, 0.1f, glow * 1.1f)
                });
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize);
            var role = new GameObject("TF_NpcRole_" + id);
            role.transform.SetParent(visuals, false);
            visuals.gameObject.AddComponent<NpcVisuals>();
        }

        // Development: in front of each inhabitant of the trading station, a photo and their first service asked
        private static System.Collections.IEnumerator DevVisit()
        {
            var ships = GameServices.ShipsServer;
            TrackedShipServer station = ships?.LoadedShipsWithChildren.FirstOrDefault(s => s.StatefulParts.Any(p => p.Settings != null && p.Settings.id == EngineerId));
            TrackedShipServer hermes = ships?.Ships.FirstOrDefault(sh => sh.GetAllStatefulParts<BeaconState>().Any(p => ((BeaconState)p.State).Name.StartsWith("Comptoir")));
            TFMod.Log.Msg($"dev: inhabitants: station {(station != null ? station.Id.ToString() : "missing")}; Hermes parts: " + (hermes == null ? "no Hermes" : string.Join(",", hermes.StatefulParts.Select(p => p.Settings?.id ?? 0).Distinct())));
            if (station == null)
                yield break;
            foreach (ushort npc in new[] { EngineerId, DoctorId })
            {
                StatefulPart part = station.StatefulParts.FirstOrDefault(p => p.Settings != null && p.Settings.id == npc);
                if (part == null)
                {
                    TFMod.Log.Msg($"dev: inhabitant {npc} missing");
                    continue;
                }
                Vector3 front = part.LocalRotation * Vector3.back;
                Vector3 eye = part.LocalPosition + front * 2.4f;
                Worlds.StationVisit.Move(station, eye, Quaternion.LookRotation(-front, part.LocalRotation * Vector3.up));
                yield return Worlds.StationVisit.Wait(4f);
                StellarDriveDemoTFAdvanced.Common.DevHarness.Screenshot("npc_" + npc);
                int service = System.Array.FindIndex(Services, x => x.Npc == npc);
                Ask(service, npc);
                yield return Worlds.StationVisit.Wait(1f);
                TFMod.Log.Msg($"dev: inhabitant {npc} at {part.LocalPosition}, facing {front}: service {service} answered '{NpcMenu.LastResult}'");
                // Close up: the robot's face and chest
                Vector3 up = part.LocalRotation * Vector3.up, face = part.LocalPosition + up * 1.55f;
                yield return Parts.Showcase.PhotographFrom(station.Id, "npc_close_" + npc, new List<(Vector3, Vector3)>
                {
                    (face + front * 1.1f + part.LocalRotation * Vector3.right * 0.35f, -front * 1.1f - part.LocalRotation * Vector3.right * 0.35f - up * 0.25f),
                    (face + front * 0.6f - up * 0.35f, -front * 0.6f)
                });
                // Held at the station until hired: picked up refused, then the contract signed (shards given), then allowed
                var connection = FishNet.InstanceFinder.ClientManager.Connection;
                TrackedPlayerServer player = GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(connection.ClientId);
                bool before = NpcGuard.Allow(station.Id, part.Id, null);
                player?.AddItemToInventory(new ItemDrop { ItemId = Shard, Quantity = ContractPrice });
                int contract = System.Array.FindIndex(Services, x => x.Npc == npc && x.Item == Shard);
                Ask(contract, npc);
                yield return Worlds.StationVisit.Wait(1f);
                TFMod.Log.Msg($"dev: inhabitant {npc}: pick up allowed before the contract {before}; contract answered '{NpcMenu.LastResult}'; allowed after {NpcGuard.Allow(station.Id, part.Id, null)}");
            }
        }

        // Service robots: brushed metal bodies, a dark visor, the role's color in their lights
        public static Outfit OutfitOf(ushort npc) => npc == DoctorId
            ? new Outfit { Suit = new Color(0.78f, 0.82f, 0.86f), Helmet = new Color(0.9f, 0.92f, 0.95f), Pack = new Color(0.35f, 0.38f, 0.42f), Visor = new Color(0.03f, 0.04f, 0.05f) }
            : new Outfit { Suit = new Color(0.36f, 0.38f, 0.42f), Helmet = new Color(0.55f, 0.57f, 0.6f), Pack = new Color(0.2f, 0.21f, 0.24f), Visor = new Color(0.03f, 0.03f, 0.04f) };

        public static Color GlowOf(ushort npc) => npc == DoctorId ? new Color(0.3f, 1f, 0.75f) : new Color(1f, 0.55f, 0.12f);

        public static void Ask(int service, ushort npc) => TFNet.SendToServer(TFMessageKind.NpcService, (uint)service, npc, "");

        // ---- Host

        private static void ServerService(NetworkConnection sender, TFMessageKind kind, uint index, ushort npc, string text)
        {
            TrackedPlayerServer player = sender == null ? null : GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            Service[] services = Services;
            if (player == null || index >= services.Length || services[index].Npc != npc)
                return;
            Service service = services[index];
            string message;
            if (!Near(player, npc))
                message = Loc.Pick("Approche-toi de lui.", "Come closer.", "Acércate.");
            else
            {
                var cost = new[] { new ItemDrop { ItemId = service.Item, Quantity = (byte)service.Count } };
                if (!player.HasItemsInInventory(cost))
                    message = Loc.Pick("Il te faut ", "You need ", "Necesitas ") + service.Count + " × " + Trader.ItemName(service.Item) + ".";
                else
                {
                    message = Do(index, npc, player);
                    if (message == null)
                    {
                        player.RemoveItemsFromInventory(cost);
                        message = Loc.Pick("C'est fait : ", "Done: ", "Hecho: ") + service.Name + ".";
                    }
                }
            }
            TFNet.SendTo(sender, TFMessageKind.NpcServiceResult, 0, 0, message);
        }

        // The work itself, or why it cannot be done (nothing paid then)
        private static string Do(uint index, ushort npc, TrackedPlayerServer player)
        {
            if (index >= 3)
                return Hire(npc, player);
            if (index == 0)
            {
                player.ResetHealthAndOxygen();
                return null;
            }
            TrackedShipServer ship = NearestShip(player);
            if (ship == null)
                return Loc.Pick("Aucun de tes vaisseaux n'est assez près (300 m).", "None of your ships is close enough (300 m).", "Ninguna de tus naves está lo bastante cerca (300 m).");
            int done = 0;
            foreach (StatefulPart part in ship.GetAllStatefulParts<FluidTankState>().ToList())
            {
                var tank = (FluidTankState)part.State;
                ushort id = part.Settings.id;
                if (index == 1 && !Power.PowerCatalog.HoldsNoFluid(id) && tank.FluidItemId != 0 && tank.FluidQuantity < FluidTankState.Capacity)
                {
                    ship.UpdatePartState(part.Id, tank.WithFluid(tank.FluidItemId, FluidTankState.Capacity, 0f));
                    done++;
                }
                else if (index == 2 && Power.PowerCatalog.IsBattery(id) && Power.PowerGrid.Charge(ship, part))
                {
                    done++;
                }
            }
            return done > 0 ? null : index == 1
                ? Loc.Pick("Tes réservoirs sont déjà pleins (ou vides : il faut un fluide dedans).", "Your tanks are already full (or empty: they need some fluid in them).", "Tus depósitos ya están llenos (o vacíos: necesitan algo de fluido).")
                : Loc.Pick("Pas de batterie à recharger sur ce vaisseau.", "No battery to charge on that ship.", "No hay batería que cargar en esa nave.");
        }

        // The contract of the nearest inhabitant of that kind
        private static string Hire(ushort npc, TrackedPlayerServer player)
        {
            var ships = GameServices.ShipsServer;
            TrackedShipServer foundShip = null;
            StatefulPart foundPart = null;
            float best = Reach;
            foreach (TrackedShipServer ship in ships?.LoadedShipsWithChildren ?? Enumerable.Empty<TrackedShipServer>())
            {
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings == null || part.Settings.id != npc)
                        continue;
                    float distance = Distance(player, ship, ship.Position + ship.Rotation * part.LocalPosition);
                    if (distance < best)
                    {
                        best = distance;
                        foundShip = ship;
                        foundPart = part;
                    }
                }
            }
            if (foundShip == null)
                return Loc.Pick("Approche-toi de lui.", "Come closer.", "Acércate.");
            if (!Held(foundShip, foundPart))
                return Loc.Pick("Il est déjà libre de te suivre : prends-le.", "They are free to follow you already: pick them up.", "Ya puede seguirte: cógelo.");
            Contracts.Add(new PartKey(foundShip.Id, foundPart.Id));
            TFMod.Log.Msg($"inhabitants: {player.Name} hired {npc} of ship {foundShip.Id}");
            return null;
        }

        private static bool Near(TrackedPlayerServer player, ushort npc)
        {
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return false;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings != null && part.Settings.id == npc && Distance(player, ship, ship.Position + ship.Rotation * part.LocalPosition) < Reach)
                        return true;
                }
            }
            return false;
        }

        // From the player to a point of a ship's space, the player outside or inside a ship (its own space)
        private static float Distance(TrackedPlayerServer player, TrackedShipServer ship, Vector3 at)
        {
            if (player.SpaceId == ship.SpaceId)
                return Vector3.Distance(at, player.Position);
            var transpose = StellarDriveDemoTFAdvanced.Common.GameServices.TransposeServer;
            if (transpose == null)
                return float.MaxValue;
            try
            {
                return Vector3.Distance(at, transpose.Position(player.SpaceId, ship.SpaceId, player.Position));
            }
            catch (System.ArgumentException)
            {
                return float.MaxValue;
            }
        }

        // The player's nearest ship that is not a station (no inhabitant nor merchant aboard)
        private static TrackedShipServer NearestShip(TrackedPlayerServer player)
        {
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return null;
            return ships.LoadedShipsWithChildren
                .Where(s => Distance(player, s, s.Position) < ShipReach
                    && !s.StatefulParts.Any(p => p.Settings != null && (p.Settings.id == EngineerId || p.Settings.id == DoctorId || p.Settings.id == Trader.TerminalId)))
                .OrderBy(s => Distance(player, s, s.Position))
                .FirstOrDefault();
        }
    }

    /// <summary>An inhabitant: a still service robot, made when a player comes near.</summary>
    internal sealed class NpcVisuals : UsablePart
    {
        private ushort _role = Npc.EngineerId;
        private GameObject _person;
        private float _next;

        public override string Label => Npc.Title(_role);

        public override void Use() => NpcMenu.Show(_role);

        protected override void Awake()
        {
            base.Awake();
            foreach (Transform child in transform)
                if (child.name.StartsWith("TF_NpcRole_") && ushort.TryParse(child.name.Substring("TF_NpcRole_".Length), out ushort role))
                    _role = role;
        }

        private void LateUpdate()
        {
            if (!HasContext || _person != null || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.5f;
            Camera camera = MainCamera.Get();
            if (camera != null && (camera.transform.position - transform.position).sqrMagnitude > 40f * 40f)
                return;
            _person = CosmeticCabin.Mannequin(transform);
            if (_person != null)
            {
                _person.transform.localPosition += Vector3.up * 0.04f;
                OutfitDresser.Wear(_person, Npc.OutfitOf(_role));
                OutfitModels.Robot(_person.transform, Npc.GlowOf(_role));
            }
        }
    }

    /// <summary>What an inhabitant offers, and the answer of the host.</summary>
    internal static class NpcMenu
    {
        private static bool _open;
        private static ushort _npc;
        private static string _result;

        public static void Show(ushort npc)
        {
            _npc = npc;
            _open = true;
            _result = null;
            ModMenu.Open(() => _open = false);
        }

        public static string LastResult { get; private set; }

        private static float _noticeUntil;

        public static void Result(string text)
        {
            _result = LastResult = text;
            _noticeUntil = Time.unscaledTime + 5f;
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
            {
                // A pick up refused (no window open then)
                if (_result != null && Time.unscaledTime < _noticeUntil)
                    DeviceUi.Toast(Screen.width, Screen.height, _result);
                return;
            }
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            Npc.Service[] services = Npc.Services;
            Rect area = DeviceUi.Window(width, height, 620f, 330f + services.Count(s => s.Npc == _npc) * 96f, Npc.Title(_npc).ToUpperInvariant());
            GUILayout.BeginArea(area);
            GUILayout.Label(_npc == Npc.DoctorId
                ? Loc.Pick("« Bienvenue à l'infirmerie du comptoir. Un bobo, un manque d'air ? »", "\"Welcome to the trading post's infirmary. Hurt, short of air?\"", "«Bienvenido a la enfermería del puesto. ¿Herido, sin aire?»")
                : Loc.Pick("« Ton vaisseau a soif ? Je fais le plein et je recharge, pour un peu de métal. »", "\"Ship running dry? I fill it up and charge it, for a bit of metal.\"", "«¿Tu nave tiene sed? La lleno y la cargo, por un poco de metal.»"), DeviceUi.Text);
            GUILayout.Space(10f);
            for (int i = 0; i < services.Length; i++)
            {
                Npc.Service service = services[i];
                if (service.Npc != _npc)
                    continue;
                GUILayout.BeginHorizontal();
                Texture icon = Trader.ItemIcon(service.Item);
                if (icon != null)
                    GUILayout.Label(icon, GUILayout.Width(52f), GUILayout.Height(52f));
                GUILayout.BeginVertical();
                GUILayout.Label("<b>" + service.Name + "</b> — " + service.Count + " × " + Trader.ItemName(service.Item), DeviceUi.Text);
                GUILayout.Label(service.Description, DeviceUi.Muted);
                GUILayout.EndVertical();
                if (GUILayout.Button(Loc.Pick("Payer", "Pay", "Pagar"), DeviceUi.Button, GUILayout.Width(100f), GUILayout.Height(40f)))
                {
                    _result = Loc.Pick("Demande envoyée…", "Asked…", "Pedido…");
                    Npc.Ask(i, _npc);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(12f);
            }
            if (_result != null)
                GUILayout.Label(_result, DeviceUi.Text);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(34f)))
                ModMenu.Close();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
