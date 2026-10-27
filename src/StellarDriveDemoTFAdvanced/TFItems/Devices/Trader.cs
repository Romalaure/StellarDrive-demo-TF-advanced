using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using Items.Model;
using Players;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;
using Loc = StellarDriveDemoTFAdvanced.Common.Loc;
using ModItems = StellarDriveDemoTFAdvanced.Items.ModItems;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// The merchant terminal of the trading station: look at it and press the use key, then trade
    /// ores for rare finds and back. The host checks the player's inventory and makes the
    /// exchange. The terminal stands only in the trading station (it is not in the build menu).
    /// </summary>
    internal static class Trader
    {
        public const ushort TerminalId = 7465;

        private static readonly Vector3 BoundsCenter = new Vector3(0f, 0.9f, 0f);
        private static readonly Vector3 BoundsSize = new Vector3(1.6f, 1.8f, 0.9f);

        /// <summary>An exchange: give Count of Item, get Gets of Getting.</summary>
        internal struct Offer
        {
            public uint Give;
            public int Count;
            public uint Get;
            public int Gets;
            public Offer(uint give, int count, uint get, int gets) { Give = give; Count = count; Get = get; Gets = gets; }
        }

        // No chain of trades gives back more than it took
        public static readonly Offer[] Offers =
        {
            // Rare finds for ores
            new Offer(ModItems.Titanium, 25, ModItems.QuantumCore, 1),
            new Offer(ModItems.Uranium, 12, ModItems.QuantumCore, 1),
            new Offer(ModItems.Quartz, 20, ModItems.ExoticMatter, 1),
            new Offer(ModItems.Cryonite, 12, ModItems.NexoranAlloy, 2),
            new Offer(ModItems.Amber, 15, ModItems.Artifact, 1),
            new Offer(ModItems.Meteorite, 10, ModItems.GoldBar, 2),
            // Base materials into ores
            new Offer(ModItems.Iron, 30, ModItems.Titanium, 6),
            new Offer(ModItems.Copper, 30, ModItems.Quartz, 6),
            new Offer(ModItems.Wood, 20, ModItems.Glass, 10),
            // Valuables sold back
            new Offer(ModItems.GoldBar, 1, ModItems.Titanium, 15),
            new Offer(ModItems.Artifact, 1, ModItems.Uranium, 10),
            new Offer(ModItems.Key, 1, ModItems.Copper, 12)
        };

        private static global::Items.Interface.Service.IItemThumbnailProvider _thumbnails;

        /// <summary>The item's inventory picture (rendered and cached by the game), or null.</summary>
        public static Texture ItemIcon(uint id)
        {
            try
            {
                if (_thumbnails == null)
                    _thumbnails = Core.Services.ServiceLocator.GetService<global::Items.Interface.Service.IItemThumbnailProvider>();
                return _thumbnails?.GetThumbnail(id);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static string ItemName(uint id)
        {
            var mod = ModItems.All.FirstOrDefault(i => i.Id == id);
            if (mod != null)
                return mod.Name;
            switch (id)
            {
                case ModItems.Iron: return Loc.Pick("Fer", "Iron", "Hierro");
                case ModItems.Glass: return Loc.Pick("Verre", "Glass", "Vidrio");
                case ModItems.Copper: return Loc.Pick("Cuivre", "Copper", "Cobre");
                case ModItems.Wood: return Loc.Pick("Bois", "Wood", "Madera");
                case ModItems.GoldBar: return Loc.Pick("Lingot d'or", "Gold bar", "Lingote de oro");
                case ModItems.Key: return Loc.Pick("Clé", "Key", "Llave");
                default: return "#" + id;
            }
        }

        public static void Register()
        {
            // Not in the build menu: only the trading station has one
            Lights.LampParts.RegisterDonorPart(TerminalId, 0, Configure, "");
            TFMod.Log.Msg("registered the merchant terminal");
        }

        public static void Install()
        {
            TFNet.OnServer(TFMessageKind.Trade, ServerTrade);
            TFNet.OnServer(TFMessageKind.TraderMission, ServerMission);
            TFNet.OnServer(TFMessageKind.TraderInfo, ServerInfo);
            TFNet.OnClient(TFMessageKind.TradeResult, (kind, a, b, text) => TraderMenu.Result(text));
            TFNet.OnClient(TFMessageKind.TraderInfoReply, (kind, a, b, text) => TraderMenu.Info(text));
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Terminal marchand", "Merchant terminal", "Terminal de comercio"),
                Loc.Pick("Le comptoir de la station marchande : regarde-le et appuie sur la touche d'utilisation (T) pour échanger tes minerais contre des trouvailles rares, ou l'inverse.",
                    "The trading station's counter: look at it and press the use key (T) to trade your ores for rare finds, or the other way round.",
                    "El mostrador de la estación comercial: míralo y pulsa la tecla de uso (T) para cambiar tus minerales por hallazgos raros, o al revés."),
                120f, new[] { (101u, 20), (102u, 8), (112u, 10) }, BoundsCenter, BoundsSize, Model(), Materials());
            UsablePart.AddBox(visuals, BoundsCenter, BoundsSize);
            visuals.gameObject.AddComponent<TraderVisuals>();
        }

        private static Dictionary<string, DeviceMaterial> Materials() => new Dictionary<string, DeviceMaterial>
        {
            ["TF_NoPaint_TraderBody"] = new DeviceMaterial(new Color(0.2f, 0.22f, 0.26f), 0.5f, 0.55f),
            ["TF_NoPaint_TraderTrim"] = new DeviceMaterial(new Color(0.75f, 0.6f, 0.25f), 0.8f, 0.6f),
            ["TF_NoPaint_TraderScreen"] = new DeviceMaterial(new Color(0.1f, 0.35f, 0.4f), 0f, 0.9f, new Color(0.2f, 0.85f, 0.9f) * 1.6f),
            ["TF_NoPaint_TraderSign"] = new DeviceMaterial(new Color(1f, 0.75f, 0.3f), 0f, 0.9f, new Color(1f, 0.7f, 0.25f) * 2.2f)
        };

        // The Blender model (tools/blender/trader.py), or the simple one built here when it is missing
        private static Dictionary<string, Mesh> Model()
        {
            Dictionary<string, Mesh> model = StellarDriveDemoTFAdvanced.Common.ObjModels.Load("trader");
            if (model != null)
                return model;
            var simple = new Dictionary<string, Mesh>();
            foreach (KeyValuePair<string, MeshBuilder> entry in Shapes())
                simple[entry.Key] = entry.Value.Build("TF_Trader_" + entry.Key);
            return simple;
        }

        // A counter with a slanted screen, a gold trim, a lit sign above, a scale and goods on it
        private static Dictionary<string, MeshBuilder> Shapes()
        {
            var body = new MeshBuilder();
            var trim = new MeshBuilder();
            var screen = new MeshBuilder();
            var sign = new MeshBuilder();
            body.Box(new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1f, 0.7f));
            trim.Box(new Vector3(0f, 1.02f, 0f), new Vector3(1.58f, 0.04f, 0.78f));
            trim.Box(new Vector3(0f, 0.06f, 0f), new Vector3(1.56f, 0.12f, 0.76f));
            // The screen, tilted toward the customer (front: -z)
            Quaternion tilt = Quaternion.Euler(-35f, 0f, 0f);
            body.Box(new Vector3(0.35f, 1.25f, 0.15f), new Vector3(0.62f, 0.42f, 0.05f), tilt);
            screen.Box(new Vector3(0.35f, 1.25f, 0.15f) + tilt * new Vector3(0f, 0f, -0.03f), new Vector3(0.56f, 0.36f, 0.01f), tilt);
            // A balance on the counter, goods beside it
            trim.Cylinder(new Vector3(-0.4f, 1.04f, 0f), new Vector3(-0.4f, 1.4f, 0f), 0.02f, 0.02f, 8);
            trim.Box(new Vector3(-0.4f, 1.4f, 0f), new Vector3(0.44f, 0.02f, 0.02f));
            foreach (float s in new[] { -1f, 1f })
                trim.Cylinder(new Vector3(-0.4f + s * 0.2f, 1.28f, 0f), new Vector3(-0.4f + s * 0.2f, 1.3f, 0f), 0.09f, 0.09f, 12);
            body.Box(new Vector3(-0.05f, 1.12f, 0.12f), new Vector3(0.16f, 0.16f, 0.16f), Quaternion.Euler(0f, 20f, 0f));
            // The sign on a post behind the counter
            body.Cylinder(new Vector3(0f, 1.02f, 0.3f), new Vector3(0f, 2.05f, 0.3f), 0.035f, 0.035f, 8);
            trim.Box(new Vector3(0f, 2.15f, 0.3f), new Vector3(0.9f, 0.26f, 0.05f));
            sign.Box(new Vector3(0f, 2.15f, 0.27f), new Vector3(0.8f, 0.18f, 0.01f));
            // Front panel stripes
            for (int i = 0; i < 4; i++)
                trim.Box(new Vector3(-0.56f + i * 0.375f, 0.55f, -0.355f), new Vector3(0.04f, 0.8f, 0.01f));
            return new Dictionary<string, MeshBuilder>
            {
                ["TF_NoPaint_TraderBody"] = body,
                ["TF_NoPaint_TraderTrim"] = trim,
                ["TF_NoPaint_TraderScreen"] = screen,
                ["TF_NoPaint_TraderSign"] = sign
            };
        }

        // ---- Daily missions: deliveries the trader asks for, each once a day in a world

        /// <summary>A delivery: bring Count of Item, get Gets of Reward and reputation.</summary>
        internal struct Mission
        {
            public uint Item;
            public int Count;
            public uint Reward;
            public int Gets;
            public Mission(uint item, int count, uint reward, int gets) { Item = item; Count = count; Reward = reward; Gets = gets; }
        }

        private static readonly Mission[] MissionPool =
        {
            new Mission(ModItems.Cryonite, 30, ModItems.QuantumCore, 2),
            new Mission(ModItems.Titanium, 40, ModItems.ExoticMatter, 1),
            new Mission(ModItems.Amber, 20, ModItems.Artifact, 2),
            new Mission(ModItems.Meteorite, 15, ModItems.GoldBar, 3),
            new Mission(ModItems.Uranium, 20, ModItems.QuantumCore, 2),
            new Mission(ModItems.Quartz, 30, ModItems.ExoticMatter, 2),
            new Mission(ModItems.Wood, 60, ModItems.NexoranAlloy, 3),
            new Mission(ModItems.Iron, 50, ModItems.QuantumCore, 1),
            new Mission(ModItems.NexoranAlloy, 10, ModItems.ExoticMatter, 2)
        };

        public const int MissionsPerDay = 3, MissionReputation = 3;

        /// <summary>Days since 1970 (UTC): the trader's day.</summary>
        public static int Today => (int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

        /// <summary>The day's missions: three different ones of the pool, drawn by the day.</summary>
        public static Mission[] MissionsOf(int day)
        {
            var random = new System.Random(day * 7307 + 11);
            return MissionPool.OrderBy(m => random.Next()).Take(MissionsPerDay).ToArray();
        }

        // ---- Reputation: pirates destroyed and missions done make the trader's prices lower

        /// <summary>The trader's discount for a reputation: 2 % a point, 40 % at most.</summary>
        public static float Discount(int reputation) => Mathf.Min(0.4f, Mathf.Max(0, reputation) * 0.02f);

        /// <summary>What an offer asks at this reputation (at least one).</summary>
        public static int Price(Offer offer, int reputation) => Mathf.Max(1, Mathf.CeilToInt(offer.Count * (1f - Discount(reputation))));

        /// <summary>Asks the host for <paramref name="times"/> times the offer <paramref name="index"/>.</summary>
        public static void Request(int index, int times) => TFNet.SendToServer(TFMessageKind.Trade, (uint)index, (ushort)times, "");

        /// <summary>Asks the host to deliver the day's mission <paramref name="index"/>.</summary>
        public static void Deliver(int index) => TFNet.SendToServer(TFMessageKind.TraderMission, (uint)index, 0, "");

        /// <summary>Asks the host for the reputation and the day's missions done.</summary>
        public static void AskInfo() => TFNet.SendToServer(TFMessageKind.TraderInfo, 0, 0, "");

        private static TrackedPlayerServer Player(NetworkConnection sender) =>
            sender == null ? null : GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);

        private static bool Pay(TrackedPlayerServer player, uint item, int count)
        {
            var cost = new[] { new ItemDrop { ItemId = item, Quantity = (byte)Mathf.Clamp(count, 1, 255) } };
            if (!player.HasItemsInInventory(cost))
                return false;
            player.RemoveItemsFromInventory(cost);
            return true;
        }

        // Host: the exchange at the world's reputation price, as many times as the player can pay, up to what was asked
        private static void ServerTrade(NetworkConnection sender, TFMessageKind kind, uint index, ushort times, string text)
        {
            TrackedPlayerServer player = Player(sender);
            if (player == null || index >= Offers.Length)
                return;
            Offer offer = Offers[index];
            int price = Price(offer, TraderLedger.Reputation), done = 0;
            for (int i = 0; i < Mathf.Clamp(times, 1, 10); i++)
            {
                if (!Pay(player, offer.Give, price))
                    break;
                player.AddItemToInventory(new ItemDrop { ItemId = offer.Get, Quantity = (byte)offer.Gets });
                done++;
            }
            string result = done == 0
                ? Loc.Pick($"Il te faut {price} × {ItemName(offer.Give)}.", $"You need {price} × {ItemName(offer.Give)}.", $"Necesitas {price} × {ItemName(offer.Give)}.")
                : Loc.Pick($"Échange fait : +{offer.Gets * done} × {ItemName(offer.Get)}.", $"Trade done: +{offer.Gets * done} × {ItemName(offer.Get)}.", $"Intercambio hecho: +{offer.Gets * done} × {ItemName(offer.Get)}.");
            TFNet.SendTo(sender, TFMessageKind.TradeResult, index, (ushort)done, result);
            TFMod.Log.Msg($"trader: {player.Name} traded offer {index} x{done} at {price}");
        }

        // Host: a mission delivered, once a day in this world
        private static void ServerMission(NetworkConnection sender, TFMessageKind kind, uint index, ushort b, string text)
        {
            TrackedPlayerServer player = Player(sender);
            if (player == null || index >= MissionsPerDay)
                return;
            int day = Today;
            Mission mission = MissionsOf(day)[index];
            string result;
            if (TraderLedger.IsDone(day, (int)index))
                result = Loc.Pick("Cette livraison est déjà faite aujourd'hui.", "This delivery is already done today.", "Esta entrega ya está hecha hoy.");
            else if (!Pay(player, mission.Item, mission.Count))
                result = Loc.Pick($"Il te faut {mission.Count} × {ItemName(mission.Item)}.", $"You need {mission.Count} × {ItemName(mission.Item)}.", $"Necesitas {mission.Count} × {ItemName(mission.Item)}.");
            else
            {
                player.AddItemToInventory(new ItemDrop { ItemId = mission.Reward, Quantity = (byte)mission.Gets });
                TraderLedger.Complete(day, (int)index);
                TraderLedger.Reputation += MissionReputation;
                result = Loc.Pick($"Livraison faite : +{mission.Gets} × {ItemName(mission.Reward)}, réputation +{MissionReputation}.",
                    $"Delivery done: +{mission.Gets} × {ItemName(mission.Reward)}, reputation +{MissionReputation}.",
                    $"Entrega hecha: +{mission.Gets} × {ItemName(mission.Reward)}, reputación +{MissionReputation}.");
                TFMod.Log.Msg($"trader: {player.Name} delivered mission {index} of day {day}");
            }
            TFNet.SendTo(sender, TFMessageKind.TradeResult, index, 0, result);
            ServerInfo(sender, kind, 0, 0, "");
        }

        // Host: "reputation|day|done indexes"
        private static void ServerInfo(NetworkConnection sender, TFMessageKind kind, uint a, ushort b, string text)
        {
            if (sender == null)
                return;
            int day = Today;
            string done = string.Join(",", Enumerable.Range(0, MissionsPerDay).Where(i => TraderLedger.IsDone(day, i)));
            TFNet.SendTo(sender, TFMessageKind.TraderInfoReply, 0, 0, $"{TraderLedger.Reputation}|{day}|{done}");
        }
    }

    /// <summary>
    /// The trader's book for the world (host): the reputation and the missions done, kept next to
    /// the world save (tfa_trader.txt). Pirates destroyed raise the reputation.
    /// </summary>
    internal static class TraderLedger
    {
        private const string FileName = "tfa_trader.txt";
        public static int Reputation;
        private static readonly HashSet<string> Done = new HashSet<string>();

        public static bool IsDone(int day, int index) => Done.Contains(day + ":" + index);

        public static void Complete(int day, int index)
        {
            Done.Add(day + ":" + index);
            // Older days are of no use
            Done.RemoveWhere(k => int.TryParse(k.Split(':')[0], out int d) && d < day - 1);
        }

        /// <summary>A pirate ship destroyed: +1, a pirate base: +5.</summary>
        public static void PirateDestroyed(bool isBase) => Reputation += isBase ? 5 : 1;

        public static void Load(string worldDirectory)
        {
            Reputation = 0;
            Done.Clear();
            string path = string.IsNullOrEmpty(worldDirectory) ? null : System.IO.Path.Combine(worldDirectory, FileName);
            if (path == null || !System.IO.File.Exists(path))
                return;
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                string[] f = line.Split(' ');
                if (f.Length == 2 && f[0] == "reputation" && int.TryParse(f[1], out int r))
                    Reputation = r;
                else if (f.Length == 2 && f[0] == "done")
                    Done.Add(f[1]);
            }
            TFMod.Log.Msg($"trader: reputation {Reputation}, {Done.Count} mission(s) done lately");
        }

        public static void Save(string worldDirectory)
        {
            if (string.IsNullOrEmpty(worldDirectory) || (Reputation == 0 && Done.Count == 0))
                return;
            var lines = new List<string> { "reputation " + Reputation };
            lines.AddRange(Done.Select(d => "done " + d));
            StellarDriveDemoTFAdvanced.Common.SafeFile.WriteAllLines(System.IO.Path.Combine(worldDirectory, FileName), lines);
        }
    }

    internal sealed class TraderVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Terminal marchand", "Merchant terminal", "Terminal de comercio");

        public override void Use() => TraderMenu.Show();
    }

    /// <summary>The merchant terminal's window: two tabs, the trades (at the reputation's prices) and the day's deliveries.</summary>
    internal static class TraderMenu
    {
        private static bool _open;
        private static int _tab;
        private static Vector2 _scroll;
        private static string _result = "";
        private static int _reputation, _day = -1;
        private static readonly HashSet<int> Done = new HashSet<int>();

        public static void Show()
        {
            _open = true;
            _result = "";
            Trader.AskInfo();
            ModMenu.Open(() => _open = false);
        }

        public static void Result(string text) => _result = text ?? "";

        /// <summary>Development: the tab shown (0 trades, 1 deliveries).</summary>
        public static int Tab { set => _tab = value; }

        public static void Info(string text)
        {
            string[] f = (text ?? "").Split('|');
            if (f.Length < 3)
                return;
            int.TryParse(f[0], out _reputation);
            int.TryParse(f[1], out _day);
            Done.Clear();
            foreach (string d in f[2].Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(d, out int i))
                    Done.Add(i);
        }

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            Rect area = DeviceUi.Window(width, height, 720f, 680f, Loc.Pick("COMPTOIR MARCHAND", "TRADING COUNTER", "MOSTRADOR COMERCIAL"));
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            string[] tabs = { Loc.Pick("Échanges", "Trades", "Intercambios"), Loc.Pick("Livraisons du jour", "Today's deliveries", "Entregas del día") };
            for (int i = 0; i < tabs.Length; i++)
                if (GUILayout.Button(tabs[i], i == _tab ? DeviceUi.Selected : DeviceUi.Button, GUILayout.Height(32f)))
                    _tab = i;
            GUILayout.EndHorizontal();
            int discount = Mathf.RoundToInt(Trader.Discount(_reputation) * 100f);
            GUILayout.Label(Loc.Pick($"Réputation : {_reputation}  ·  remise {discount} % (pirates détruits, livraisons faites)",
                $"Reputation: {_reputation}  ·  discount {discount}% (pirates destroyed, deliveries made)",
                $"Reputación: {_reputation}  ·  descuento {discount} % (piratas destruidos, entregas hechas)"), DeviceUi.Muted);
            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_tab == 0)
            {
                for (int i = 0; i < Trader.Offers.Length; i++)
                {
                    Trader.Offer offer = Trader.Offers[i];
                    GUILayout.BeginHorizontal();
                    Stack(offer.Give, Trader.Price(offer, _reputation));
                    GUILayout.Label("→", DeviceUi.Text, GUILayout.Width(30f), GUILayout.Height(IconSize));
                    Stack(offer.Get, offer.Gets);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(Loc.Pick("Échanger", "Trade", "Cambiar"), DeviceUi.Button, GUILayout.Height(30f), GUILayout.Width(100f)))
                        Trader.Request(i, 1);
                    if (GUILayout.Button("× 5", DeviceUi.Button, GUILayout.Height(30f), GUILayout.Width(60f)))
                        Trader.Request(i, 5);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3f);
                }
            }
            else
            {
                Trader.Mission[] missions = Trader.MissionsOf(_day >= 0 ? _day : Trader.Today);
                GUILayout.Label(Loc.Pick("Le marchand a besoin de ces livraisons aujourd'hui (une fois chacune pour tout le monde, renouvelées chaque jour) :",
                    "The trader needs these deliveries today (once each for everyone, new ones every day):",
                    "El comerciante necesita estas entregas hoy (una vez cada una para todos, nuevas cada día):"), DeviceUi.Muted);
                for (int i = 0; i < missions.Length; i++)
                {
                    Trader.Mission mission = missions[i];
                    GUILayout.BeginHorizontal();
                    Stack(mission.Item, mission.Count);
                    GUILayout.Label("→", DeviceUi.Text, GUILayout.Width(30f), GUILayout.Height(IconSize));
                    Stack(mission.Reward, mission.Gets);
                    GUILayout.Label($"+{Trader.MissionReputation}", DeviceUi.Muted, GUILayout.Width(50f), GUILayout.Height(IconSize));
                    GUILayout.FlexibleSpace();
                    if (Done.Contains(i))
                        GUILayout.Label(Loc.Pick("Faite", "Done", "Hecha"), DeviceUi.Muted, GUILayout.Width(120f));
                    else if (GUILayout.Button(Loc.Pick("Livrer", "Deliver", "Entregar"), DeviceUi.Button, GUILayout.Height(30f), GUILayout.Width(120f)))
                        Trader.Deliver(i);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3f);
                }
            }
            GUILayout.EndScrollView();
            if (_result.Length > 0)
                GUILayout.Label(_result, DeviceUi.Text);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndArea();
            // The item's name under the pointer
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                Vector2 mouse = Event.current.mousePosition;
                var tip = new Rect(mouse.x + 16f, mouse.y + 10f, 260f, 30f);
                DeviceUi.Panel(tip);
                GUI.Label(new Rect(tip.x + 10f, tip.y, tip.width - 20f, tip.height), GUI.tooltip, DeviceUi.Text);
            }
            GUI.matrix = previous;
        }

        private const float IconSize = 44f;

        // An item's picture (the game's inventory thumbnail) and its count; its name shows on hover
        private static void Stack(uint item, int count)
        {
            Texture icon = Trader.ItemIcon(item);
            string name = Trader.ItemName(item);
            if (icon != null)
                GUILayout.Box(new GUIContent(icon, name), DeviceUi.Muted, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
            else
                GUILayout.Label(new GUIContent(name, name), DeviceUi.Text, GUILayout.Height(IconSize));
            GUILayout.Label(new GUIContent("× " + count, name), DeviceUi.Text, GUILayout.Width(60f), GUILayout.Height(IconSize));
        }
    }
}
