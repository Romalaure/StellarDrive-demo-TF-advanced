using System.Collections.Generic;
using Items.Model;
using StellarDriveDemoTFAdvanced.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.ShipComputer
{
    internal enum Severity
    {
        Info,
        Warning,
        Error
    }

    internal struct DiagnosticMessage
    {
        public Severity Severity;
        public string Text;

        public DiagnosticMessage(Severity severity, string text)
        {
            Severity = severity;
            Text = text;
        }
    }

    /// <summary>
    /// Turns a snapshot into messages. A check only fires when the data it needs was actually read;
    /// what cannot be verified is reported as unavailable rather than guessed.
    /// </summary>
    internal static class Diagnostics
    {
        public static void Evaluate(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            messages.Clear();
            if (!snap.HasShip)
            {
                messages.Add(new DiagnosticMessage(Severity.Info, snap.NoShipReason ?? "Aucun vaisseau actif."));
                return;
            }

            CheckFlight(snap, messages);
            CheckPropulsion(snap, messages);
            CheckPower(snap, messages);
            CheckTraffic(snap, messages);

            if (!snap.SurfaceSpeed.HasValue && !snap.SpaceSpeed.HasValue)
                messages.Add(new DiagnosticMessage(Severity.Info, "Vitesse indisponible (visuels du vaisseau non chargés ou espace non pris en charge)."));
            if (!snap.Altitude.HasValue)
                messages.Add(new DiagnosticMessage(Severity.Info, "Altitude indisponible : aucune planète de référence."));
            if (snap.DecorativeBatteries > 0)
                messages.Add(new DiagnosticMessage(Severity.Info, $"{snap.DecorativeBatteries} Battery Bank du jeu à bord : décorative dans la démo. La Batterie de l'onglet TF Avancé stocke réellement."));
            if (messages.Count == 0)
                messages.Add(new DiagnosticMessage(Severity.Info, "Aucune anomalie détectée dans les données disponibles."));
        }

        private static void CheckFlight(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            if (snap.VerticalSpeed.HasValue && snap.Altitude.HasValue
                && -snap.VerticalSpeed.Value > Settings.DescentWarningSpeed.Value
                && snap.Altitude.Value < Settings.DescentWarningAltitude.Value)
            {
                messages.Add(new DiagnosticMessage(Severity.Warning,
                    $"Descente rapide : {-snap.VerticalSpeed.Value:0} m/s à {snap.Altitude.Value:0} m (seuils {Settings.DescentWarningSpeed.Value:0} m/s, {Settings.DescentWarningAltitude.Value:0} m)."));
            }
            if (snap.BlackHoleDistance.HasValue)
            {
                float distance = snap.BlackHoleDistance.Value;
                Worlds.Hole hole = snap.BlackHole;
                if (distance < hole.TidalRadius)
                    messages.Add(new DiagnosticMessage(Severity.Error, $"Forces de marée : {hole.Name} arrache des pièces ({distance / 1000f:0.0} km, horizon à {hole.Horizon / 1000f:0} km)."));
                else if (distance < hole.TidalRadius * 4f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, $"{hole.Name} proche : {distance / 1000f:0} km, zone de marée à {hole.TidalRadius / 1000f:0} km. Attraction {snap.BlackHoleGravity:0.0} m/s²."));
            }
            if (snap.AngularVelocity.HasValue && snap.AngularVelocity.Value.magnitude > Settings.SpinWarningRate.Value)
            {
                messages.Add(new DiagnosticMessage(Severity.Warning,
                    $"Rotation rapide : {snap.AngularVelocity.Value.magnitude:0} °/s (seuil {Settings.SpinWarningRate.Value:0} °/s)."));
            }
        }

        private static void CheckPropulsion(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            if (snap.Thrusters.Count == 0)
                messages.Add(new DiagnosticMessage(Severity.Warning, "Propulseur non détecté sur le vaisseau."));

            foreach (ThrusterReading thruster in snap.Thrusters)
            {
                if (thruster.Kind == ThrusterKind.Chemical)
                {
                    if (!thruster.FuelSocketsConnected)
                    {
                        messages.Add(new DiagnosticMessage(Severity.Warning, $"Propulseur chimique #{thruster.PartId} : prises d'ergols 1 et 2 pas toutes les deux branchées."));
                    }
                    else if (IsPropellant(thruster.FluidA) && IsPropellant(thruster.FluidB) && thruster.FluidA == thruster.FluidB)
                    {
                        messages.Add(new DiagnosticMessage(Severity.Error, $"Propulseur chimique #{thruster.PartId} : les deux conduites portent le même fluide ({FluidName(thruster.FluidA)}), il faut oxygène ET éthanol."));
                    }
                }
                else if (thruster.TroubleStarting)
                {
                    messages.Add(new DiagnosticMessage(Severity.Warning, $"Propulseur à bois #{thruster.PartId} : commandé mais à court de bois."));
                }
            }

            foreach (GyroReading gyro in snap.Gyros)
            {
                if (!gyro.IsSeat && !gyro.PowerConnected)
                    messages.Add(new DiagnosticMessage(Severity.Warning, $"Gyroscope #{gyro.PartId} non alimenté : prise électrique non branchée."));
                else if (gyro.PowerSatisfaction.HasValue && gyro.PowerSatisfaction.Value < 0.999f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, $"Gyroscope #{gyro.PartId} sous-alimenté : {gyro.PowerSatisfaction.Value:P0} de la puissance demandée."));
            }
        }

        private static void CheckPower(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            foreach (GeneratorReading generator in snap.Generators)
            {
                if (!generator.Connected)
                    messages.Add(new DiagnosticMessage(Severity.Info, $"Générateur #{generator.PartId} : sortie électrique non branchée."));
                else if (!generator.Quantum && generator.Wood <= 0f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, $"Générateur #{generator.PartId} : plus de bois."));
            }
            foreach (ConsumerReading consumer in snap.Consumers)
            {
                if (consumer.Name != "Gyroscope" && !consumer.Connected)
                    messages.Add(new DiagnosticMessage(Severity.Info, $"{consumer.Name} #{consumer.PartId} non alimenté : prise électrique non branchée."));
            }
            foreach (BatteryReading battery in snap.Batteries)
            {
                if (!battery.Connected)
                    messages.Add(new DiagnosticMessage(Severity.Info, $"Batterie #{battery.PartId} non branchée."));
                else if (battery.Percent < 5f && battery.Flow < -1f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, $"Batterie #{battery.PartId} presque vide ({battery.Percent:0} %) et en décharge."));
            }
            foreach (BreakerReading breaker in snap.Breakers)
            {
                if (breaker.Status == Power.PowerCatalog.BreakerTripped)
                    messages.Add(new DiagnosticMessage(Severity.Error, breaker.ControlWired
                        ? $"Disjoncteur #{breaker.PartId} disjoncté (surcharge) : repasser sa commande à 0 puis > 0 pour le réarmer."
                        : $"Disjoncteur #{breaker.PartId} disjoncté (surcharge) : réarmement automatique dans {Power.PowerCatalog.BreakerAutoResetSeconds.Value:0} s."));
                else if (breaker.Status == Power.PowerCatalog.BreakerOpen)
                    messages.Add(new DiagnosticMessage(Severity.Info, $"Disjoncteur #{breaker.PartId} ouvert par sa commande."));
                if (!breaker.InputConnected || !breaker.OutputConnected)
                    messages.Add(new DiagnosticMessage(Severity.Info, $"Disjoncteur #{breaker.PartId} : entrée et sortie doivent être branchées sur deux réseaux différents."));
            }
            if (snap.IsHost)
            {
                foreach (Power.PowerGrid.Trip trip in Power.PowerGrid.Journal)
                {
                    float ago = UnityEngine.Time.unscaledTime - trip.Time;
                    if (ago > 300f)
                        break;
                    messages.Add(new DiagnosticMessage(Severity.Info, $"Journal : disjoncteur {trip.ShipId}:{trip.PartId} déclenché il y a {ago:0} s ({trip.DemandWatts:0} W demandés)."));
                }
            }
            foreach (PowerNetworkReading network in snap.Networks)
            {
                if (network.Satisfaction.HasValue && network.Satisfaction.Value < 0.999f)
                {
                    messages.Add(new DiagnosticMessage(network.Satisfaction.Value < 0.5f ? Severity.Error : Severity.Warning,
                        $"Réseau électrique sous-alimenté : {network.Demand:0} W demandés, {network.Capacity:0} W disponibles ({network.Satisfaction.Value:P0})."));
                }
            }
        }

        private static void CheckTraffic(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            foreach (TrafficContact contact in snap.Traffic)
            {
                if (contact.TimeToClosest.HasValue && contact.TimeToClosest.Value < Settings.CollisionWarningSeconds.Value
                    && contact.ClosestDistance < Settings.CollisionWarningDistance.Value)
                {
                    messages.Add(new DiagnosticMessage(Severity.Error,
                        $"Rapprochement : vaisseau #{contact.ShipId} à {contact.ClosestDistance:0} m (centre à centre) dans {contact.TimeToClosest.Value:0.0} s."));
                }
            }
        }

        public static bool IsPropellant(uint itemId)
        {
            return itemId == ItemsConstants.OxygenItemId || itemId == ItemsConstants.EthanolItemId;
        }

        public static string FluidName(uint itemId)
        {
            if (itemId == 0)
                return "vide";
            if (itemId == ItemsConstants.OxygenItemId)
                return "oxygène";
            if (itemId == ItemsConstants.EthanolItemId)
                return "éthanol";
            return "fluide " + itemId;
        }

        public static Color ColorOf(Severity severity)
        {
            switch (severity)
            {
                case Severity.Error: return ComputerStyle.Error;
                case Severity.Warning: return ComputerStyle.Warning;
                default: return ComputerStyle.Info;
            }
        }
    }
}
