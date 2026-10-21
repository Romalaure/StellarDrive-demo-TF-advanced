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
    /// The inhabitants of the trading station: an engineer (7482) who fills the tanks of your ship
    /// and a doctor (7483) who heals you and fills your suit's oxygen, for a few base resources.
    /// They stand on a small lit pad; look at one and press the use key. The host checks that the
    /// player stands near one, takes the price from their inventory and does the work. Not in the
    /// build menu: the station places them.
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
                Description = Loc.Pick("Recharge à fond les batteries du mod de ton vaisseau le plus proche.", "Fully charges the mod's batteries on your nearest ship.", "Carga a tope las baterías del mod de tu nave más cercana."), Item = 112, Count = 6 }
        };

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
            }
        }

        public static Outfit OutfitOf(ushort npc) => npc == DoctorId
            ? new Outfit { Suit = new Color(0.92f, 0.94f, 0.96f), Helmet = new Color(0.95f, 0.95f, 0.97f), Pack = new Color(0.3f, 0.75f, 0.45f), Jacket = 1, JacketColor = new Color(0.96f, 0.96f, 0.98f) }
            : new Outfit { Suit = new Color(0.95f, 0.5f, 0.12f), Helmet = new Color(0.25f, 0.27f, 0.3f), Pack = new Color(0.3f, 0.32f, 0.35f), Hat = 1, HatColor = new Color(1f, 0.82f, 0.1f), Jacket = 2, JacketColor = new Color(0.22f, 0.3f, 0.45f) };

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
                    message = Do(index, player);
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
        private static string Do(uint index, TrackedPlayerServer player)
        {
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

        private static bool Near(TrackedPlayerServer player, ushort npc)
        {
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return false;
            foreach (TrackedShipServer ship in ships.LoadedShipsWithChildren)
            {
                if (ship.SpaceId != player.SpaceId)
                    continue;
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings != null && part.Settings.id == npc && Vector3.Distance(ship.Position + ship.Rotation * part.LocalPosition, player.Position) < Reach)
                        return true;
                }
            }
            return false;
        }

        // The player's nearest ship that is not a station (no inhabitant nor merchant aboard)
        private static TrackedShipServer NearestShip(TrackedPlayerServer player)
        {
            var ships = GameServices.ShipsServer;
            if (ships == null)
                return null;
            return ships.LoadedShipsWithChildren
                .Where(s => s.SpaceId == player.SpaceId && Vector3.Distance(s.Position, player.Position) < ShipReach
                    && !s.StatefulParts.Any(p => p.Settings != null && (p.Settings.id == EngineerId || p.Settings.id == DoctorId || p.Settings.id == Trader.TerminalId)))
                .OrderBy(s => Vector3.Distance(s.Position, player.Position))
                .FirstOrDefault();
        }
    }

    /// <summary>An inhabitant: a still person in their work clothes, made when a player comes near.</summary>
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

        public static void Result(string text) => _result = LastResult = text;

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
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
