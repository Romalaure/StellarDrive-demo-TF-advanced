using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using PowerCatalog = StellarDriveDemoTFAdvanced.Power.PowerCatalog;

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
                Dim(snap.NoShipReason ?? "Aucun vaisseau actif.");
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
            string linked = snap.ConnectedShipCount > 0 ? $" (+{snap.ConnectedShipCount} liés)" : "";
            Row("Vaisseau", $"#{snap.ShipId} · {snap.ShipSource} · {snap.PartCount} pièces · {N(snap.Mass, "0")} kg{linked}");
            Row("Lieu", (snap.SpaceKind ?? Unavailable()) + (snap.PlanetName != null ? " · " + snap.PlanetName : ""));
            Row("Altitude", snap.Altitude.HasValue ? N(snap.Altitude.Value, "0") + $" m <color={_dim}>/ rayon moyen</color>" : Unavailable());
            string vertical = snap.VerticalSpeed.HasValue ? $" · vert. {Signed(snap.VerticalSpeed.Value, "0.0")}" : "";
            string orbital = snap.SpaceSpeed.HasValue ? $" · orb. {N(snap.SpaceSpeed.Value, "0")}" : "";
            Row("Vitesse", snap.SurfaceSpeed.HasValue || snap.SpaceSpeed.HasValue
                ? $"surf. {(snap.SurfaceSpeed.HasValue ? N(snap.SurfaceSpeed.Value, "0.0") : "—")}{vertical}{orbital} m/s"
                : Unavailable());
            Row("Assiette", snap.Pitch.HasValue
                ? $"tangage {Signed(snap.Pitch.Value, "0")}° · roulis {Signed(snap.Roll ?? 0f, "0")}° <color={_dim}>({snap.AttitudeFrame})</color>"
                : Unavailable());
            Row("Rotation", snap.AngularVelocity.HasValue ? N(snap.AngularVelocity.Value.magnitude, "0.0") + " °/s" : Unavailable("mesure en cours"));
            if (snap.BlackHoleDistance.HasValue)
                Row("Trou noir", $"{N(snap.BlackHoleDistance.Value / 1000f, "0")} km · {N(snap.BlackHoleGravity, "0.00")} m/s² · temps {N(snap.BlackHoleTimeDilation * 100f, "0.0")} %");

            if (snap.Traffic.Count == 0)
            {
                Row("Trafic", Unavailable("aucun vaisseau à portée"));
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
            Row("Trafic", string.Join(" · ", contacts) + more);
        }

        private void Propulsion(TelemetrySnapshot snap)
        {
            float thrust = snap.NetThrust.magnitude;
            Row("Poussée nette", $"{N(thrust, "0")} N <color={_dim}>/ {N(snap.MaxThrust, "0")} N max</color>");
            Row("Accélération", snap.Mass > 0f ? N(thrust / snap.Mass, "0.00") + " m/s²" : Unavailable());

            _sb.Append('\n');
            Title($"PROPULSEURS ({snap.Thrusters.Count})");
            if (snap.Thrusters.Count == 0)
                Dim("Aucun propulseur détecté.");
            int shown = 0;
            foreach (ThrusterReading thruster in snap.Thrusters)
            {
                if (shown++ >= MaxListedParts)
                {
                    Dim($"+{snap.Thrusters.Count - MaxListedParts} autres");
                    break;
                }
                string label = thruster.Name + $" #{thruster.PartId}";
                string detail = thruster.Kind == ThrusterKind.Chemical
                    ? $"{Pct(thruster.Level)} · {N(thruster.Thrust, "0")} N · {N(thruster.PropellantFlow, "0.000")} u/s"
                    : $"{Pct(thruster.Level)} · {N(thruster.Thrust, "0")} N · bois {N(thruster.Wood, "0.0")}";
                Row(label, detail + $" <color={_dim}>{Axis(thruster.Direction)}</color>");
            }

            _sb.Append('\n');
            Title("CONTRÔLE D'ATTITUDE");
            if (snap.Gyros.Count == 0)
                Dim("Aucun gyroscope ni siège pilote.");
            shown = 0;
            foreach (GyroReading gyro in snap.Gyros)
            {
                if (shown++ >= MaxListedParts)
                {
                    Dim($"+{snap.Gyros.Count - MaxListedParts} autres");
                    break;
                }
                if (gyro.IsSeat)
                {
                    Row($"Siège #{gyro.PartId}", $"{Pct(gyro.Strength)} <color={_dim}>{(gyro.SeatInUse ? "occupé" : "libre")}</color>");
                    continue;
                }
                string power = !gyro.PowerConnected ? Colored(ComputerStyle.Warning, "non branché")
                    : gyro.PowerSatisfaction.HasValue ? "alim. " + Pct(gyro.PowerSatisfaction.Value) : $"<color={_dim}>branché</color>";
                Row($"Gyroscope #{gyro.PartId}", $"{Pct(gyro.Strength)} · {power}");
            }

            if (snap.WarpDrives.Count > 0)
            {
                _sb.Append('\n');
                Title("CŒUR STELLAIRE");
                foreach (WarpReading warp in snap.WarpDrives)
                    Row($"#{warp.PartId}", warp.State);
            }
        }

        private void Power(TelemetrySnapshot snap)
        {
            float produced = 0f;
            foreach (GeneratorReading generator in snap.Generators)
                produced += generator.Watts;
            Row("Production", $"{N(produced, "0")} W <color={_dim}>({snap.Generators.Count} générateur(s))</color>");
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
                Row("Stockage", $"{N(percent, "0")} % · {N(stored, "0")} kJ · {FlowText(flow)}");
            }
            else
            {
                Row("Stockage", Unavailable(snap.DecorativeBatteries > 0 ? "Battery Bank du jeu sans fonction" : "aucune batterie"));
            }

            if (snap.Batteries.Count > 0)
            {
                _sb.Append('\n');
                Title($"BATTERIES ({snap.Batteries.Count})");
                foreach (BatteryReading battery in snap.Batteries)
                {
                    string plug = battery.Connected ? "" : " · " + Colored(ComputerStyle.Warning, "non branchée");
                    Row($"Batterie #{battery.PartId}", $"{N(battery.Percent, "0")} % · {FlowText(battery.Flow)}{plug}");
                }
            }
            if (snap.Breakers.Count > 0)
            {
                _sb.Append('\n');
                Title($"DISJONCTEURS ({snap.Breakers.Count})");
                foreach (BreakerReading breaker in snap.Breakers)
                {
                    string status = breaker.Status == PowerCatalog.BreakerTripped ? Colored(ComputerStyle.Error, "disjoncté")
                        : breaker.Status == PowerCatalog.BreakerOpen ? $"<color={_dim}>ouvert</color>" : "fermé";
                    string wiring = !breaker.InputConnected || !breaker.OutputConnected ? " · " + Colored(ComputerStyle.Warning, "entrée/sortie à brancher") : "";
                    Row($"Disjoncteur #{breaker.PartId}", $"{status} · {N(breaker.Flow, "0")} W{wiring}");
                }
            }

            _sb.Append('\n');
            Title("GÉNÉRATEURS");
            if (snap.Generators.Count == 0)
                Dim("Aucun générateur.");
            foreach (GeneratorReading generator in snap.Generators)
            {
                string output = generator.Connected ? "" : " · " + Colored(ComputerStyle.Warning, "non branché");
                if (generator.Quantum)
                    Row($"Réacteur #{generator.PartId}", $"{Pct(generator.Level)} · {N(generator.Watts, "0")} W · sans carburant{output}");
                else
                    Row($"Bois #{generator.PartId}", $"{Pct(generator.Level)} · {N(generator.Watts, "0")} W · bois {N(generator.Wood, "0.0")}{output}");
            }

            _sb.Append('\n');
            Title("CONSOMMATEURS");
            if (snap.Consumers.Count == 0)
                Dim("Aucun consommateur électrique.");
            int shown = 0;
            foreach (ConsumerReading consumer in snap.Consumers)
            {
                if (shown++ >= MaxListedParts)
                {
                    Dim($"+{snap.Consumers.Count - MaxListedParts} autres");
                    break;
                }
                string watts = consumer.Watts.HasValue ? N(consumer.Watts.Value, "0") + " W" : $"<color={_dim}>W non publié</color>";
                string plug = consumer.Connected ? "" : " · " + Colored(ComputerStyle.Warning, "non branché");
                Row($"{consumer.Name} #{consumer.PartId}", $"{Pct(consumer.Level)} · {watts}{plug}");
            }

            _sb.Append('\n');
            Title(snap.HostPowerDetails ? "RÉSEAUX (mesure serveur)" : "RÉSEAUX (câbles)");
            if (snap.Networks.Count == 0)
                Dim("Aucun réseau électrique câblé.");
            int index = 1;
            foreach (PowerNetworkReading network in snap.Networks)
            {
                string detail = $"{N(network.Watts, "0")} W transmis";
                if (network.Demand.HasValue)
                    detail += $" · dem. {N(network.Demand.Value, "0")} · cap. {N(network.Capacity ?? 0f, "0")}";
                if (network.Satisfaction.HasValue)
                    detail += " · " + Pct(network.Satisfaction.Value);
                Row($"Réseau {index++}", detail + $" <color={_dim}>({network.Cables} câbles)</color>");
            }
        }

        private void Diagnostics(TelemetrySnapshot snap, List<DiagnosticMessage> messages, float sampleMs, float rate)
        {
            foreach (DiagnosticMessage message in messages)
            {
                string tag = message.Severity == Severity.Error ? "ERREUR" : message.Severity == Severity.Warning ? "ALERTE" : "INFO";
                // No indent here: a tag wider than the indent would be drawn over by the message
                _sb.Append(Colored(ShipComputer.Diagnostics.ColorOf(message.Severity), "<b>" + tag + "</b>")).Append("  ").Append(message.Text).Append('\n');
            }
            _sb.Append('\n');
            Title("TECHNIQUE");
            Row("Rôle", (snap.IsHost ? "hôte" : "client") + $" · {N(rate, "0.#")} lectures/s · {N(sampleMs, "0.00")} ms");
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

        private string Unavailable(string why = "indisponible")
        {
            return $"<color={_dim}>{why}</color>";
        }

        private string Speed(float? value)
        {
            return value.HasValue ? N(value.Value, "0.0") + " m/s" : Unavailable();
        }

        private static string FlowText(float watts)
        {
            if (watts > 1f)
                return "charge " + N(watts, "0") + " W";
            if (watts < -1f)
                return "décharge " + N(-watts, "0") + " W";
            return "repos";
        }

        private static string Colored(Color color, string text)
        {
            return $"<color={ComputerStyle.Hex(color)}>{text}</color>";
        }

        private static string N(float value, string format)
        {
            return value.ToString(format, Fr);
        }

        private static string Signed(float value, string format)
        {
            return (value >= 0f ? "+" : "") + value.ToString(format, Fr);
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
