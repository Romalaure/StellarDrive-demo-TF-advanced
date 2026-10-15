using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Core.Values;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Services;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using StellarDriveDemoTFAdvanced.Power;
using UnityEngine;
using WorldTracking.Interface.Services;

namespace StellarDriveDemoTFAdvanced.Combat
{
    /// <summary>
    /// Pirate radars: three models, 1 000, 5 000 and 20 000 km. While powered, a radar shows on its
    /// round screen the pirates around its ship (their ships, their bases and the raids on their
    /// way, which it sees coming from as far as its range), and the list of the nearest contacts.
    /// The host computes the contacts of each ship carrying a radar once a second and sends them
    /// to every player (<see cref="TFANet"/>); the radar's signal socket gives their number.
    /// </summary>
    internal static class Radar
    {
        public sealed class Tier
        {
            public ushort Id;
            public int Level;
            public float RangeKm;
            public float WattsScale;
            public string Label, Flavor;
            public Color Glow;
            public float Mass;
            public (uint Item, int Count)[] Cost;
        }

        public static readonly Tier[] Tiers =
        {
            new Tier { Id = PowerCatalog.RadarShortId, Level = 1, RangeKm = 1000f, WattsScale = 1f, Glow = MachineModels.Green, Mass = 60f,
                Label = Loc.Pick("Radar courte portée", "Short range radar"), Flavor = Loc.Pick("Un petit radar de patrouille (lueur verte).", "A small patrol radar (green glow)."),
                Cost = new[] { (112u, 10), (102u, 6), (101u, 6) } },
            new Tier { Id = PowerCatalog.RadarLongId, Level = 2, RangeKm = 5000f, WattsScale = 3f, Glow = new Color(1f, 0.72f, 0.2f), Mass = 140f,
                Label = Loc.Pick("Radar longue portée", "Long range radar"), Flavor = Loc.Pick("Un radar à grande antenne (lueur dorée).", "A radar with a big dish (golden glow)."),
                Cost = new[] { (112u, 20), (102u, 10), (Items.ModItems.Titanium, 8), (Items.ModItems.Quartz, 4) } },
            new Tier { Id = PowerCatalog.RadarDeepId, Level = 3, RangeKm = 20000f, WattsScale = 8f, Glow = MachineModels.Violet, Mass = 260f,
                Label = Loc.Pick("Radar de l'espace profond", "Deep space radar"), Flavor = Loc.Pick("Un mât d'écoute de l'espace profond (lueur violette).", "A deep space listening mast (violet glow)."),
                Cost = new[] { (112u, 30), (Items.ModItems.Titanium, 15), (Items.ModItems.Quartz, 8), (Items.ModItems.Cryonite, 6), (Items.ModItems.QuantumCore, 1) } },
        };

        public static Tier TierOf(ushort id)
        {
            foreach (Tier tier in Tiers)
            {
                if (tier.Id == id)
                    return tier;
            }
            return null;
        }

        public static bool IsRadar(ushort id) => TierOf(id) != null;

        public enum Kind : byte { Ship = 0, Base = 1, Raid = 2 }

        public struct Contact
        {
            public Kind Kind;
            /// <summary>Design key of a pirate ship or raid.</summary>
            public string Key;
            /// <summary>Position in the radar's ship frame (x right, y up, z forward), m.</summary>
            public Vector3 Local;
            public float Eta;
            public int Count;
        }

        private sealed class Reading
        {
            public float Time;
            public readonly List<Contact> Contacts = new List<Contact>();
        }

        /// <summary>Root ship id -> its last contacts (host and players alike).</summary>
        private static readonly Dictionary<uint, Reading> Readings = new Dictionary<uint, Reading>();
        private static float _nextScan;
        private static bool _listening;

        public static void Register()
        {
            foreach (Tier t in Tiers)
            {
                Tier tier = t;
                PartFactory.Register(tier.Id, "SignalDisplay", AdvancedTab.Name, AdvancedTab.CombatRow, (settings, prefab) => Configure(tier, settings, prefab));
            }
        }

        public static void OnSceneLoaded()
        {
            Readings.Clear();
            _nextScan = 0f;
            _alarmUntil = 0f;
            _alarmKnown.Clear();
        }

        // ------------------------------------------------------------------ host

        /// <summary>The number of contacts in range of a ship's radar (from the last scan).</summary>
        public static int ContactCount(uint root, float range)
        {
            if (!Readings.TryGetValue(root, out Reading reading))
                return 0;
            int count = 0;
            foreach (Contact c in reading.Contacts)
            {
                if (c.Local.sqrMagnitude <= range * range)
                    count++;
            }
            return count;
        }

        /// <summary>Every frame (host and players): scans once a second on the host.</summary>
        public static void Update()
        {
            if (!_listening)
            {
                _listening = true;
                TFANet.Received += OnMessage;
            }
            TFANet.Update();
            if (!GameServices.IsServer || CombatPower.RadarRanges.Count == 0)
                return;
            float now = Time.unscaledTime;
            if (now < _nextScan)
                return;
            _nextScan = now + 1f;
            try
            {
                Scan();
            }
            catch (System.Exception e)
            {
                ErrorLog.Report("radar", e);
            }
        }

        private static void Scan()
        {
            ShipsServerTracker ships = GameServices.ShipsServer;
            IWorldTransposeServer transpose = GameServices.TransposeServer;
            if (ships == null || transpose == null)
                return;
            var text = new StringBuilder();
            foreach (KeyValuePair<uint, float> radar in CombatPower.RadarRanges)
            {
                TrackedShipServer ship = ships.GetShip(radar.Key);
                if (ship == null)
                    continue;
                if (!Readings.TryGetValue(radar.Key, out Reading reading))
                    Readings[radar.Key] = reading = new Reading();
                reading.Time = Time.unscaledTime;
                reading.Contacts.Clear();
                float range = radar.Value;
                foreach (Pirate pirate in Pirates.All.Values)
                {
                    TrackedShipServer other = ships.GetShip(pirate.ShipId);
                    if (other == null || !TryLocal(transpose, ship, other.PhysicsState.SpaceId, other.PhysicsState.Position, range, out Vector3 local))
                        continue;
                    reading.Contacts.Add(new Contact { Kind = pirate.IsBase ? Kind.Base : Kind.Ship, Key = pirate.Design.Key, Local = local, Eta = -1f, Count = 1 });
                }
                foreach (Raid raid in Pirates.Raids)
                {
                    if (!Pirates.TryRaidPosition(ships, raid, out uint spaceId, out Vector3 at) || !TryLocal(transpose, ship, spaceId, at, range, out Vector3 local))
                        continue;
                    reading.Contacts.Add(new Contact { Kind = Kind.Raid, Key = raid.Design.Key, Local = local, Eta = raid.Eta, Count = raid.Count });
                }
                // The nearest first, at most 24 sent
                reading.Contacts.Sort((a, b) => a.Local.sqrMagnitude.CompareTo(b.Local.sqrMagnitude));
                if (reading.Contacts.Count > 24)
                    reading.Contacts.RemoveRange(24, reading.Contacts.Count - 24);
                TFANet.SendToAll(TFANet.RadarContacts, radar.Key, Encode(reading, text));
            }
        }

        // A position (in any space) in the ship's frame; false when out of range or not reachable
        private static bool TryLocal(IWorldTranspose transpose, TrackedShipServer ship, uint spaceId, Vector3 position, float range, out Vector3 local)
        {
            local = Vector3.zero;
            try
            {
                Vector3 offset = spaceId == ship.PhysicsState.SpaceId
                    ? position - ship.PhysicsState.Position
                    : (Vector3)(transpose.Position(spaceId, ship.PhysicsState.SpaceId, (Vector3Double)position) - (Vector3Double)ship.PhysicsState.Position);
                if (float.IsNaN(offset.x) || offset.sqrMagnitude > range * range)
                    return false;
                local = Quaternion.Inverse(ship.PhysicsState.Rotation) * offset;
                return true;
            }
            catch
            {
                // a space that no longer exists
                return false;
            }
        }

        private static string Encode(Reading reading, StringBuilder text)
        {
            text.Length = 0;
            foreach (Contact c in reading.Contacts)
            {
                text.Append((int)c.Kind).Append(',').Append(c.Key).Append(',')
                    .Append(Mathf.RoundToInt(c.Local.x).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Mathf.RoundToInt(c.Local.y).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Mathf.RoundToInt(c.Local.z).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Mathf.RoundToInt(c.Eta).ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(c.Count.ToString(CultureInfo.InvariantCulture)).Append(';');
            }
            return text.ToString();
        }

        // ------------------------------------------------------------------ players

        private static void OnMessage(byte kind, uint root, string text)
        {
            if (kind != TFANet.RadarContacts || GameServices.IsServer)
                return;
            if (!Readings.TryGetValue(root, out Reading reading))
                Readings[root] = reading = new Reading();
            reading.Time = Time.unscaledTime;
            reading.Contacts.Clear();
            foreach (string entry in text.Split(';'))
            {
                string[] f = entry.Split(',');
                if (f.Length < 7)
                    continue;
                int.TryParse(f[0], out int k);
                float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
                float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
                float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
                float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float eta);
                int.TryParse(f[6], out int count);
                reading.Contacts.Add(new Contact { Kind = (Kind)k, Key = f[1], Local = new Vector3(x, y, z), Eta = eta, Count = count });
            }
        }

        /// <summary>The ship's contacts, null when none came for 4 s (radar off, ship left).</summary>
        public static List<Contact> ContactsOf(uint root)
        {
            return Readings.TryGetValue(root, out Reading reading) && Time.unscaledTime - reading.Time < 4f ? reading.Contacts : null;
        }

        public static string NameOf(Contact c)
        {
            if (c.Kind == Kind.Base)
                return Loc.Pick("Base pirate", "Pirate base");
            PirateDesign design = PirateShips.ByKey(c.Key);
            string name = design != null ? design.Name : Loc.Pick("Pirate", "Pirate");
            return c.Kind == Kind.Raid ? Loc.Pick("Raid ", "Raid ") + c.Count + " × " + name : name;
        }

        public static string Distance(float meters) =>
            meters < 10000f ? (meters / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km" : (meters / 1000f).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ') + " km";

        // ------------------------------------------------------------------ alarm

        private static float _alarmUntil;
        private static string _alarm;
        private static readonly HashSet<string> _alarmKnown = new HashSet<string>();
        private static GUIStyle _alarmStyle;

        /// <summary>Called by a radar screen near the player that sees a raid coming.</summary>
        public static void Alarm(uint root, Contact raid)
        {
            if (!Settings.RadarAlarm.Value)
                return;
            string key = root + "/" + raid.Key + "/" + raid.Count;
            if (!_alarmKnown.Add(key))
                return;
            _alarm = Loc.Pick($"ALERTE RADAR : {NameOf(raid)} en approche, {Distance(raid.Local.magnitude)}, contact dans {raid.Eta:0} s",
                $"RADAR ALERT: {NameOf(raid)} incoming, {Distance(raid.Local.magnitude)}, contact in {raid.Eta:0} s");
            _alarmUntil = Time.unscaledTime + 8f;
        }

        /// <summary>Forgets alarms of raids no longer seen (a new raid alarms again).</summary>
        public static void ForgetAlarms(uint root, List<Contact> contacts)
        {
            if (_alarmKnown.Count == 0)
                return;
            string prefix = root + "/";
            _alarmKnown.RemoveWhere(k => k.StartsWith(prefix) && (contacts == null || !contacts.Exists(c => c.Kind == Kind.Raid && k == prefix + c.Key + "/" + c.Count)));
        }

        public static void OnGUI()
        {
            if (Time.unscaledTime > _alarmUntil || string.IsNullOrEmpty(_alarm))
                return;
            if (_alarmStyle == null)
                _alarmStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bool on = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.55f;
            _alarmStyle.normal.textColor = on ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.7f, 0.3f);
            var rect = new Rect(0f, Screen.height * 0.12f, Screen.width, 40f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), "<color=#000000>" + _alarm + "</color>", _alarmStyle);
            GUI.Label(rect, _alarm, _alarmStyle);
        }

        // ------------------------------------------------------------------ part

        private static void Configure(Tier tier, PartSettings settings, GameObject prefab)
        {
            float watts = Mathf.Max(0f, PowerCatalog.RadarWatts.Value) * tier.WattsScale;
            PartFactory.Describe(settings, tier.Label,
                Loc.Pick($"{tier.Flavor} Détecte les pirates autour du vaisseau sur {tier.RangeKm:#,0} km : leurs vaisseaux, leurs bases et les raids en approche (vus dès qu'ils entrent dans sa portée, avec le temps avant contact). L'écran rond montre les contacts vus du dessus, l'avant du radar en haut, la distance en échelle logarithmique (anneaux à 10, 100, 1 000, 10 000 km), et la liste des plus proches. Il lui faut {watts:0} W sur ses prises électriques. Sa prise signal donne le nombre de contacts ; éteint : -1 pas de réseau électrique, -3 pas assez de courant.",
                    $"{tier.Flavor} Detects the pirates around the ship within {tier.RangeKm:#,0} km: their ships, their bases and the incoming raids (seen as soon as they enter its range, with the time before contact). The round screen shows the contacts seen from above, the radar's front at the top, distance on a log scale (rings at 10, 100, 1,000, 10,000 km), and the list of the nearest. It needs {watts:0} W on its power sockets. Its signal socket gives the number of contacts; off: -1 no power grid, -3 not enough power.").Replace(" ", " ").Replace(" ", " "),
                tier.Mass, tier.Cost);
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 1f, height = 0.95f, depth = 0.7f;
            float mast = tier.Level == 3 ? 1.1f : tier.Level == 2 ? 0.75f : 0.5f;
            // The screen juts out of the front (+Z, the vents' side): the box reaches it
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, (height + mast) * 0.5f, 0.08f), new Vector3(width, height + mast, depth + 0.16f));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.8f);
            float face = depth * 0.47f;
            MachineModels.Vent(model, -0.3f, 0.25f, face, 0.24f, 0.22f, 3);
            MachineModels.Vent(model, 0.3f, 0.25f, face, 0.24f, 0.22f, 3);
            MachineModels.Grille(model, 0.25f, face, 0.1f, 0.24f);
            // The screen: a dark panel on the front, leaning back toward the player (its outward
            // normal +Z tilted up), framed; RadarVisuals draws on it
            Quaternion tilt = Quaternion.Euler(-35f, 0f, 0f);
            Vector3 screen = new Vector3(0f, 0.6f, depth * 0.47f + 0.08f);
            model.Armor.Box(new Vector3(0f, 0.42f, depth * 0.47f + 0.05f), new Vector3(width * 0.7f, 0.06f, 0.12f));
            model.Armor.Box(screen + tilt * new Vector3(0f, 0f, -0.012f), new Vector3(width * 0.94f, 0.5f, 0.03f), tilt);
            model.Dark.Box(screen, new Vector3(width * 0.88f, 0.44f, 0.01f), tilt);
            // The mast and its antenna on top (RadarVisuals turns the antenna while powered)
            float top = height;
            float mz = -depth * 0.12f;
            model.Armor.Cylinder(new Vector3(0f, top - 0.02f, mz), new Vector3(0f, top + 0.06f, mz), 0.16f, 0.13f, 10);
            model.Dark.Cylinder(new Vector3(0f, top + 0.06f, mz), new Vector3(0f, top + mast * 0.7f, mz), 0.04f, 0.035f, 8);
            if (tier.Level == 3)
            {
                foreach (float y in new[] { 0.3f, 0.5f })
                    model.Light.Cylinder(new Vector3(0f, top + mast * y, mz), new Vector3(0f, top + mast * y + 0.015f, mz), 0.07f, 0.07f, 12, capA: false, capB: false);
            }
            var antenna = new MachineModels.Model();
            float dish = tier.Level == 3 ? 0.42f : tier.Level == 2 ? 0.32f : 0.2f;
            // A dish (shallow cone) facing forward on a short arm, a feed horn at its focus
            antenna.Armor.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.05f), 0.03f, 0.03f, 8);
            antenna.Armor.Cylinder(new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0f, 0.05f + dish * 0.35f), dish * 0.15f, dish, 18, capA: true, capB: false);
            antenna.Glow.Cylinder(new Vector3(0f, 0f, 0.05f + dish * 0.35f), new Vector3(0f, 0f, 0.05f + dish * 0.36f), dish, dish * 1.02f, 18, capA: false, capB: false);
            antenna.Dark.Cylinder(new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0f, 0.05f + dish * 0.8f), 0.01f, 0.01f, 6);
            antenna.Core.Cylinder(new Vector3(0f, 0f, 0.05f + dish * 0.8f), new Vector3(0f, 0f, 0.05f + dish * 0.88f), 0.025f, 0.015f, 8);
            if (tier.Level >= 2)
            {
                // Side fins for the bigger ones
                antenna.Armor.Box(new Vector3(dish * 0.6f, 0f, 0.04f), new Vector3(dish * 0.5f, 0.02f, 0.06f));
                antenna.Armor.Box(new Vector3(-dish * 0.6f, 0f, 0.04f), new Vector3(dish * 0.5f, 0.02f, 0.06f));
            }
            MachineModels.Attach(visuals, model, "Radar" + tier.Level, tier.Glow, tier.Glow);
            var head = new GameObject(RadarVisuals.AntennaName) { layer = visuals.gameObject.layer };
            head.transform.SetParent(visuals, false);
            head.transform.localPosition = new Vector3(0f, top + mast * 0.7f + 0.02f, mz);
            MachineModels.Attach(head.transform, antenna, "RadarDish" + tier.Level, tier.Glow, tier.Glow);
            var anchor = new GameObject(RadarVisuals.ScreenName + tier.Level) { layer = visuals.gameObject.layer };
            anchor.transform.SetParent(visuals, false);
            // Text reads right when its forward points from the reader into the screen
            anchor.transform.localPosition = screen + tilt * new Vector3(0f, 0f, 0.008f);
            anchor.transform.localRotation = tilt * Quaternion.Euler(0f, 180f, 0f);
            visuals.gameObject.AddComponent<RadarVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.25f, PowerParts.BackOf(bounds), 0.22f,
                (PowerCatalog.MachinePowerA, Ships.Interface.Model.Parts.CableType.Power), (PowerCatalog.MachineSignal, Ships.Interface.Model.Parts.CableType.Signal), (PowerCatalog.MachinePowerB, Ships.Interface.Model.Parts.CableType.Power));
        }
    }
}
