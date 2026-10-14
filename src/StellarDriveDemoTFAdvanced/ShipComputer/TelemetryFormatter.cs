using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using PowerCatalog = StellarDriveDemoTFAdvanced.Power.PowerCatalog;
using StellarDriveDemoTFAdvanced.Common;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    internal enum ComputerSection
    {
        Flight,
        Propulsion,
        Power,
        Diagnostics
    }

    /// <summary>Writes the panel text of each section (TextMeshPro rich text, label / value columns).</summary>
    internal sealed class TelemetryFormatter
    {
        private const int MaxListedParts = 8;
        // Values start at this column; wrapped lines stay in it (indent, not pos) so they never run under the labels
        private const string ValueColumn = "<indent=34%>";
        // French style (1 234,5) with a plain space: the game font may lack the non-breaking ones fr-FR uses
        private static readonly NumberFormatInfo Fr = new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = " " };

        private readonly StringBuilder _sb = new StringBuilder(2048);
        private string _dim;
        private string _strong;

        public string Write(ComputerSection section, TelemetrySnapshot snap, List<DiagnosticMessage> messages, float sampleMs, float rate)
        {
            _sb.Clear();
            _dim = ComputerStyle.Hex(ComputerStyle.TextDim);
            _strong = ComputerStyle.Hex(ComputerStyle.TextStrong);
            if (!snap.HasShip && section != ComputerSection.Diagnostics)
            {
                Dim(snap.NoShipReason ?? Loc.Pick("Aucun vaisseau actif.", "No active ship."));
                return _sb.ToString();
            }
            switch (section)
            {
                case ComputerSection.Flight: Flight(snap); break;
                case ComputerSection.Propulsion: Propulsion(snap); break;
                case ComputerSection.Power: Power(snap); break;
                default: Diagnostics(snap, messages, sampleMs, rate); break;
            }
            return _sb.ToString();
        }

        // Compact: one fact per line, so the four-section screens stay readable
        private void Flight(TelemetrySnapshot snap)
        {
            string linked = snap.ConnectedShipCount > 0 ? Loc.Pick($" (+{snap.ConnectedShipCount} liés)", $" (+{snap.ConnectedShipCount} linked)") : "";
            Row(Loc.Pick("Vaisseau", "Ship"), Loc.Pick($"#{snap.ShipId} · {snap.ShipSource} · {snap.PartCount} pièces · {N(snap.Mass, "0")} kg{linked}", $"#{snap.ShipId} · {snap.ShipSource} · {snap.PartCount} parts · {N(snap.Mass, "0")} kg{linked}"));
            Row(Loc.Pick("Lieu", "Place"), (snap.SpaceKind ?? Unavailable()) + (snap.PlanetName != null ? " · " + snap.PlanetName : ""));
            Row("Altitude", snap.Altitude.HasValue ? N(snap.Altitude.Value, "0") + Loc.Pick($" m <color={_dim}>/ rayon moyen</color>", $" m <color={_dim}>/ mean radius</color>") : Unavailable());
            string vertical = snap.VerticalSpeed.HasValue ? $" · vert. {Signed(snap.VerticalSpeed.Value, "0.0")}" : "";
            string orbital = snap.SpaceSpeed.HasValue ? $" · orb. {N(snap.SpaceSpeed.Value, "0")}" : "";
            Row(Loc.Pick("Vitesse", "Speed"), snap.SurfaceSpeed.HasValue || snap.SpaceSpeed.HasValue
                ? $"surf. {(snap.SurfaceSpeed.HasValue ? N(snap.SurfaceSpeed.Value, "0.0") : "—")}{vertical}{orbital} m/s"
                : Unavailable());
            Row(Loc.Pick("Assiette", "Attitude"), snap.Pitch.HasValue
                ? Loc.Pick($"tangage {Signed(snap.Pitch.Value, "0")}° · roulis {Signed(snap.Roll ?? 0f, "0")}° <color={_dim}>({snap.AttitudeFrame})</color>", $"pitch {Signed(snap.Pitch.Value, "0")}° · roll {Signed(snap.Roll ?? 0f, "0")}° <color={_dim}>({snap.AttitudeFrame})</color>")
                : Unavailable());
            Row("Rotation", snap.AngularVelocity.HasValue ? N(snap.AngularVelocity.Value.magnitude, "0.0") + " °/s" : Unavailable(Loc.Pick("mesure en cours", "measuring")));
            if (snap.BlackHoleDistance.HasValue)
                Row(Loc.Pick("Trou noir", "Black hole"), Loc.Pick($"{N(snap.BlackHoleDistance.Value / 1000f, "0")} km · {N(snap.BlackHoleGravity, "0.00")} m/s² · temps {N(snap.BlackHoleTimeDilation * 100f, "0.0")} %", $"{N(snap.BlackHoleDistance.Value / 1000f, "0")} km · {N(snap.BlackHoleGravity, "0.00")} m/s² · time {N(snap.BlackHoleTimeDilation * 100f, "0.0")} %"));

            if (snap.Traffic.Count == 0)
            {
                Row(Loc.Pick("Trafic", "Traffic"), Unavailable(Loc.Pick("aucun vaisseau à portée", "no ship in range")));
                return;
            }
            var contacts = new List<string>();
            foreach (TrafficContact contact in snap.Traffic)
            {
                if (contacts.Count == 3)
                    break;
                string trend = contact.ClosingSpeed > 0.05f ? "↓" : contact.ClosingSpeed < -0.05f ? "↑" : "";
                contacts.Add($"#{contact.ShipId} {N(contact.Distance, "0")} m{trend}");
            }
            string more = snap.Traffic.Count > 3 ? $" <color={_dim}>+{snap.Traffic.Count - 3}</color>" : "";
            Row(Loc.Pick("Trafic", "Traffic"), string.Join(" · ", contacts) + more);
        }

        private void Propulsion(TelemetrySnapshot snap)
        {
            float thrust = snap.NetThrust.magnitude;
            Row(Loc.Pick("Poussée nette", "Net thrust"), $"{N(thrust, "0")} N <color={_dim}>/ {N(snap.MaxThrust, "0")} N max</color>");
            Row(Loc.Pick("Accélération", "Acceleration"), snap.Mass > 0f ? N(thrust / snap.Mass, "0.00") + " m/s²" : Unavailable());

            _sb.Append('\n');
            Title(Loc.Pick($"PROPULSEURS ({snap.Thrusters.Count})", $"THRUSTERS ({snap.Thrusters.Count})"));
            if (snap.Thrusters.Count == 0)
                Dim(Loc.Pick("Aucun propulseur détecté.", "No thruster found."));
            int shown = 0;
            foreach (ThrusterReading thruster in snap.Thrusters)
            {
                if (shown++ >= MaxListedParts)
                {
                    Dim(Loc.Pick($"+{snap.Thrusters.Count - MaxListedParts} autres", $"+{snap.Thrusters.Count - MaxListedParts} more"));
                    break;
                }
                string label = thruster.Name + $" #{thruster.PartId}";
                string detail = thruster.Kind == ThrusterKind.Chemical
                    ? $"{Pct(thruster.Level)} · {N(thruster.Thrust, "0")} N · {N(thruster.PropellantFlow, "0.000")} u/s"
                    : Loc.Pick($"{Pct(thruster.Level)} · {N(thruster.Thrust, "0")} N · bois {N(thruster.Wood, "0.0")}", $"{Pct(thruster.Level)} · {N(thruster.Thrust, "0")} N · wood {N(thruster.Wood, "0.0")}");
                Row(label, detail + $" <color={_dim}>{Axis(thruster.Direction)}</color>");
            }

            _sb.Append('\n');
            Title(Loc.Pick("CONTRÔLE D'ATTITUDE", "ATTITUDE CONTROL"));
            if (snap.Gyros.Count == 0)
                Dim(Loc.Pick("Aucun gyroscope ni siège pilote.", "No gyroscope or pilot seat."));
            shown = 0;
            foreach (GyroReading gyro in snap.Gyros)
            {
                if (shown++ >= MaxListedParts)
                {
                    Dim(Loc.Pick($"+{snap.Gyros.Count - MaxListedParts} autres", $"+{snap.Gyros.Count - MaxListedParts} more"));
                    break;
                }
                if (gyro.IsSeat)
                {
                    Row(Loc.Pick($"Siège #{gyro.PartId}", $"Seat #{gyro.PartId}"), $"{Pct(gyro.Strength)} <color={_dim}>{(gyro.SeatInUse ? Loc.Pick("occupé", "taken") : Loc.Pick("libre", "free"))}</color>");
                    continue;
                }
                string power = !gyro.PowerConnected ? Colored(ComputerStyle.Warning, Loc.Pick("non branché", "unplugged"))
                    : gyro.PowerSatisfaction.HasValue ? Loc.Pick("alim. ", "power ") + Pct(gyro.PowerSatisfaction.Value) : Loc.Pick($"<color={_dim}>branché</color>", $"<color={_dim}>plugged</color>");
                Row($"Gyroscope #{gyro.PartId}", $"{Pct(gyro.Strength)} · {power}");
            }

            if (snap.WarpDrives.Count > 0)
            {
                _sb.Append('\n');
                Title(Loc.Pick("CŒUR STELLAIRE", "STELLAR CORE"));
                foreach (WarpReading warp in snap.WarpDrives)
                    Row($"#{warp.PartId}", warp.State);
            }
        }

        private void Power(TelemetrySnapshot snap)
        {
            float produced = 0f;
            foreach (GeneratorReading generator in snap.Generators)
                produced += generator.Watts;
            Row("Production", Loc.Pick($"{N(produced, "0")} W <color={_dim}>({snap.Generators.Count} générateur(s))</color>", $"{N(produced, "0")} W <color={_dim}>({snap.Generators.Count} generator(s))</color>"));
            if (snap.Batteries.Count > 0)
            {
                float percent = 0f, flow = 0f;
                foreach (BatteryReading battery in snap.Batteries)
                {
                    percent += battery.Percent;
                    flow += battery.Flow;
                }
                percent /= snap.Batteries.Count;
                float stored = percent / 100f * PowerCatalog.BatteryCapacityKJ.Value * snap.Batteries.Count;
                Row(Loc.Pick("Stockage", "Storage"), $"{N(percent, "0")} % · {N(stored, "0")} kJ · {FlowText(flow)}");
            }
            else
            {
                Row(Loc.Pick("Stockage", "Storage"), Unavailable(snap.DecorativeBatteries > 0 ? Loc.Pick("Battery Bank du jeu sans fonction", "game Battery Bank does nothing") : Loc.Pick("aucune batterie", "no battery")));
            }

            if (snap.Batteries.Count > 0)
            {
                _sb.Append('\n');
                Title($"BATTERIES ({snap.Batteries.Count})");
                foreach (BatteryReading battery in snap.Batteries)
                {
                    string plug = battery.Connected ? "" : " · " + Colored(ComputerStyle.Warning, Loc.Pick("non branchée", "unplugged"));
                    Row(Loc.Pick($"Batterie #{battery.PartId}", $"Battery #{battery.PartId}"), $"{N(battery.Percent, "0")} % · {FlowText(battery.Flow)}{plug}");
                }
            }
            if (snap.Breakers.Count > 0)
            {
                _sb.Append('\n');
                Title(Loc.Pick($"DISJONCTEURS ({snap.Breakers.Count})", $"BREAKERS ({snap.Breakers.Count})"));
                foreach (BreakerReading breaker in snap.Breakers)
                {
                    string status = breaker.Status == PowerCatalog.BreakerTripped ? Colored(ComputerStyle.Error, Loc.Pick("disjoncté", "tripped"))
                        : breaker.Status == PowerCatalog.BreakerOpen ? Loc.Pick($"<color={_dim}>ouvert</color>", $"<color={_dim}>open</color>") : Loc.Pick("fermé", "closed");
                    string wiring = !breaker.InputConnected || !breaker.OutputConnected ? " · " + Colored(ComputerStyle.Warning, Loc.Pick("entrée/sortie à brancher", "plug input/output")) : "";
                    Row(Loc.Pick($"Disjoncteur #{breaker.PartId}", $"Breaker #{breaker.PartId}"), $"{status} · {N(breaker.Flow, "0")} W{wiring}");
                }
            }

            _sb.Append('\n');
            Title(Loc.Pick("GÉNÉRATEURS", "GENERATORS"));
            if (snap.Generators.Count == 0)
                Dim(Loc.Pick("Aucun générateur.", "No generator."));
            foreach (GeneratorReading generator in snap.Generators)
            {
                string output = generator.Connected ? "" : " · " + Colored(ComputerStyle.Warning, Loc.Pick("non branché", "unplugged"));
                if (generator.Quantum)
                    Row(Loc.Pick($"Réacteur #{generator.PartId}", $"Reactor #{generator.PartId}"), Loc.Pick($"{Pct(generator.Level)} · {N(generator.Watts, "0")} W · sans carburant{output}", $"{Pct(generator.Level)} · {N(generator.Watts, "0")} W · no fuel{output}"));
                else
                    Row(Loc.Pick($"Bois #{generator.PartId}", $"Wood #{generator.PartId}"), Loc.Pick($"{Pct(generator.Level)} · {N(generator.Watts, "0")} W · bois {N(generator.Wood, "0.0")}{output}", $"{Pct(generator.Level)} · {N(generator.Watts, "0")} W · wood {N(generator.Wood, "0.0")}{output}"));
            }

            _sb.Append('\n');
            Title(Loc.Pick("CONSOMMATEURS", "CONSUMERS"));
            if (snap.Consumers.Count == 0)
                Dim(Loc.Pick("Aucun consommateur électrique.", "No power consumer."));
            int shown = 0;
            foreach (ConsumerReading consumer in snap.Consumers)
            {
                if (shown++ >= MaxListedParts)
                {
                    Dim(Loc.Pick($"+{snap.Consumers.Count - MaxListedParts} autres", $"+{snap.Consumers.Count - MaxListedParts} more"));
                    break;
                }
                string watts = consumer.Watts.HasValue ? N(consumer.Watts.Value, "0") + " W" : Loc.Pick($"<color={_dim}>W non publié</color>", $"<color={_dim}>W not published</color>");
                string plug = consumer.Connected ? "" : " · " + Colored(ComputerStyle.Warning, Loc.Pick("non branché", "unplugged"));
                Row($"{consumer.Name} #{consumer.PartId}", $"{Pct(consumer.Level)} · {watts}{plug}");
            }

            _sb.Append('\n');
            Title(snap.HostPowerDetails ? Loc.Pick("RÉSEAUX (mesure serveur)", "GRIDS (server reading)") : Loc.Pick("RÉSEAUX (câbles)", "GRIDS (cables)"));
            if (snap.Networks.Count == 0)
                Dim(Loc.Pick("Aucun réseau électrique câblé.", "No wired power grid."));
            int index = 1;
            foreach (PowerNetworkReading network in snap.Networks)
            {
                string detail = Loc.Pick($"{N(network.Watts, "0")} W transmis", $"{N(network.Watts, "0")} W carried");
                if (network.Demand.HasValue)
                    detail += $" · dem. {N(network.Demand.Value, "0")} · cap. {N(network.Capacity ?? 0f, "0")}";
                if (network.Satisfaction.HasValue)
                    detail += " · " + Pct(network.Satisfaction.Value);
                Row(Loc.Pick($"Réseau {index++}", $"Grid {index++}"), detail + Loc.Pick($" <color={_dim}>({network.Cables} câbles)</color>", $" <color={_dim}>({network.Cables} cables)</color>"));
            }
        }

        private void Diagnostics(TelemetrySnapshot snap, List<DiagnosticMessage> messages, float sampleMs, float rate)
        {
            foreach (DiagnosticMessage message in messages)
            {
                string tag = message.Severity == Severity.Error ? Loc.Pick("ERREUR", "ERROR") : message.Severity == Severity.Warning ? Loc.Pick("ALERTE", "WARNING") : "INFO";
                // No indent here: a tag wider than the indent would be drawn over by the message
                _sb.Append(Colored(ShipComputer.Diagnostics.ColorOf(message.Severity), "<b>" + tag + "</b>")).Append("  ").Append(message.Text).Append('\n');
            }
            _sb.Append('\n');
            Title(Loc.Pick("TECHNIQUE", "TECHNICAL"));
            Row(Loc.Pick("Rôle", "Role"), (snap.IsHost ? Loc.Pick("hôte", "host") : "client") + Loc.Pick($" · {N(rate, "0.#")} lectures/s · {N(sampleMs, "0.00")} ms", $" · {N(rate, "0.#")} reads/s · {N(sampleMs, "0.00")} ms"));
        }

        // Longest label that always fits left of the value column, at the largest text size
        private const int LabelFit = 10;

        private void Row(string label, string value)
        {
            // <indent> moves the caret back to the column: a wider label would be drawn over by its
            // value, so a long label gets its own line
            _sb.Append(label);
            if (label.Length > LabelFit)
                _sb.Append('\n');
            _sb.Append(ValueColumn).Append("<color=").Append(_strong).Append('>').Append(value).Append("</color></indent>\n");
        }

        private void Title(string title)
        {
            _sb.Append("<color=").Append(ComputerStyle.Hex(ComputerStyle.Line)).Append("><b>").Append(title).Append("</b></color>\n");
        }

        private void Dim(string text)
        {
            _sb.Append("<color=").Append(_dim).Append('>').Append(text).Append("</color>\n");
        }

        private string Unavailable(string why = null)
        {
            return $"<color={_dim}>{why ?? Loc.Pick("indisponible", "unavailable")}</color>";
        }

        private string Speed(float? value)
        {
            return value.HasValue ? N(value.Value, "0.0") + " m/s" : Unavailable();
        }

        private static string FlowText(float watts)
        {
            if (watts > 1f)
                return Loc.Pick("charge ", "charging ") + N(watts, "0") + " W";
            if (watts < -1f)
                return Loc.Pick("décharge ", "discharging ") + N(-watts, "0") + " W";
            return Loc.Pick("repos", "idle");
        }

        private static string Colored(Color color, string text)
        {
            return $"<color={ComputerStyle.Hex(color)}>{text}</color>";
        }

        private static string N(float value, string format)
        {
            return value.ToString(format, Loc.English ? NumberFormatInfo.InvariantInfo : Fr);
        }

        private static string Signed(float value, string format)
        {
            return (value >= 0f ? "+" : "") + value.ToString(format, Loc.English ? NumberFormatInfo.InvariantInfo : Fr);
        }

        private static string Pct(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + " %";
        }

        // Which way a thruster pushes the ship, in the ship grid
        private static string Axis(Vector3 direction)
        {
            Vector3 a = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            if (a.x >= a.y && a.x >= a.z)
                return direction.x > 0f ? "→ +X" : "→ −X";
            if (a.y >= a.z)
                return direction.y > 0f ? "→ +Y" : "→ −Y";
            return direction.z > 0f ? "→ +Z" : "→ −Z";
        }
    }
}
