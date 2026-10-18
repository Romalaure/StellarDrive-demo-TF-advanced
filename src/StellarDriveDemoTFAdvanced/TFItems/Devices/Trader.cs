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
            TFNet.OnClient(TFMessageKind.TradeResult, (kind, a, b, text) => TraderMenu.Result(text));
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            Transform visuals = DeviceModels.Build(settings, prefab, Loc.Pick("Terminal marchand", "Merchant terminal", "Terminal de comercio"),
                Loc.Pick("Le comptoir de la station marchande : regarde-le et appuie sur la touche d'utilisation (T) pour échanger tes minerais contre des trouvailles rares, ou l'inverse.",
                    "The trading station's counter: look at it and press the use key (T) to trade your ores for rare finds, or the other way round.",
                    "El mostrador de la estación comercial: míralo y pulsa la tecla de uso (T) para cambiar tus minerales por hallazgos raros, o al revés."),
                120f, new[] { (101u, 20), (102u, 8), (112u, 10) }, BoundsCenter, BoundsSize, Shapes(), Materials());
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

        /// <summary>Asks the host for <paramref name="times"/> times the offer <paramref name="index"/>.</summary>
        public static void Request(int index, int times) => TFNet.SendToServer(TFMessageKind.Trade, (uint)index, (ushort)times, "");

        // Host: the exchange, if the player has what it takes (as many times as they can, up to what was asked)
        private static void ServerTrade(NetworkConnection sender, TFMessageKind kind, uint index, ushort times, string text)
        {
            if (sender == null || index >= Offers.Length)
                return;
            TrackedPlayerServer player = GameServices.PlayersServer?.GetTrackedPlayerFromConnectionId(sender.ClientId);
            if (player == null)
                return;
            Offer offer = Offers[index];
            int done = 0;
            for (int i = 0; i < Mathf.Clamp(times, 1, 10); i++)
            {
                var cost = new[] { new ItemDrop { ItemId = offer.Give, Quantity = (byte)offer.Count } };
                if (!player.HasItemsInInventory(cost))
                    break;
                player.RemoveItemsFromInventory(cost);
                player.AddItemToInventory(new ItemDrop { ItemId = offer.Get, Quantity = (byte)offer.Gets });
                done++;
            }
            string result = done == 0
                ? Loc.Pick($"Il te faut {offer.Count} × {ItemName(offer.Give)}.", $"You need {offer.Count} × {ItemName(offer.Give)}.", $"Necesitas {offer.Count} × {ItemName(offer.Give)}.")
                : Loc.Pick($"Échange fait : +{offer.Gets * done} × {ItemName(offer.Get)}.", $"Trade done: +{offer.Gets * done} × {ItemName(offer.Get)}.", $"Intercambio hecho: +{offer.Gets * done} × {ItemName(offer.Get)}.");
            TFNet.SendTo(sender, TFMessageKind.TradeResult, index, (ushort)done, result);
            TFMod.Log.Msg($"trader: {player.Name} traded offer {index} x{done}");
        }
    }

    internal sealed class TraderVisuals : UsablePart
    {
        public override string Label => Loc.Pick("Terminal marchand", "Merchant terminal", "Terminal de comercio");

        public override void Use() => TraderMenu.Show();
    }

    /// <summary>The merchant terminal's window: the offers, a trade button for one and for five.</summary>
    internal static class TraderMenu
    {
        private static bool _open;
        private static Vector2 _scroll;
        private static string _result = "";

        public static void Show()
        {
            _open = true;
            _result = "";
            ModMenu.Open(() => _open = false);
        }

        public static void Result(string text) => _result = text ?? "";

        public static void Draw()
        {
            if (!_open || !ModMenu.IsOpen)
                return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            Rect area = DeviceUi.Window(width, height, 700f, 660f, Loc.Pick("COMPTOIR MARCHAND", "TRADING COUNTER", "MOSTRADOR COMERCIAL"));
            GUILayout.BeginArea(area);
            GUILayout.Label(Loc.Pick("Ce que tu donnes, ce que tu reçois. Les objets viennent de ton inventaire.", "What you give, what you get. Items come from your inventory.", "Lo que das, lo que recibes. Los objetos salen de tu inventario."), DeviceUi.Muted);
            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < Trader.Offers.Length; i++)
            {
                Trader.Offer offer = Trader.Offers[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{offer.Count} × {Trader.ItemName(offer.Give)}   →   {offer.Gets} × {Trader.ItemName(offer.Get)}", DeviceUi.Text, GUILayout.Width(470f));
                if (GUILayout.Button(Loc.Pick("Échanger", "Trade", "Cambiar"), DeviceUi.Button, GUILayout.Height(30f), GUILayout.Width(100f)))
                    Trader.Request(i, 1);
                if (GUILayout.Button("× 5", DeviceUi.Button, GUILayout.Height(30f), GUILayout.Width(60f)))
                    Trader.Request(i, 5);
                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }
            GUILayout.EndScrollView();
            if (_result.Length > 0)
                GUILayout.Label(_result, DeviceUi.Text);
            if (GUILayout.Button(Loc.Pick("Fermer (Échap)", "Close (Esc)", "Cerrar (Esc)"), DeviceUi.Button, GUILayout.Height(36f)))
                ModMenu.Close();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
