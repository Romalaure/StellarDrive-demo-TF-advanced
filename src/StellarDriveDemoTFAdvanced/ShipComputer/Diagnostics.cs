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
                messages.Add(new DiagnosticMessage(Severity.Info, snap.NoShipReason ?? Loc.Pick("Aucun vaisseau actif.", "No active ship.", "Ninguna nave activa.")));
                return;
            }

            CheckFlight(snap, messages);
            CheckPropulsion(snap, messages);
            CheckPower(snap, messages);
            CheckTraffic(snap, messages);

            if (!snap.SurfaceSpeed.HasValue && !snap.SpaceSpeed.HasValue)
                messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick("Vitesse indisponible (visuels du vaisseau non chargés ou espace non pris en charge).", "Speed unavailable (ship visuals not loaded or space not supported).", "Velocidad no disponible (visuales de la nave no cargados o espacio no compatible).")));
            if (!snap.Altitude.HasValue)
                messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick("Altitude indisponible : aucune planète de référence.", "Altitude unavailable: no reference planet.", "Altitud no disponible: sin planeta de referencia.")));
            if (snap.DecorativeBatteries > 0)
                messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"{snap.DecorativeBatteries} Battery Bank du jeu à bord : décorative dans la démo. La Batterie de l'onglet TF Avancé stocke réellement.", $"{snap.DecorativeBatteries} game Battery Bank(s) on board: decorative in the demo. The Battery from the TF Avancé tab really stores power.", $"{snap.DecorativeBatteries} Battery Bank(s) del juego a bordo: decorativas en la demo. La Batería de la pestaña TF Avancé almacena energía de verdad.")));
            if (messages.Count == 0)
                messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick("Aucune anomalie détectée dans les données disponibles.", "No fault found in the available data.", "No se encontró ningún fallo en los datos disponibles.")));
        }

        private static void CheckFlight(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            if (snap.VerticalSpeed.HasValue && snap.Altitude.HasValue
                && -snap.VerticalSpeed.Value > Settings.DescentWarningSpeed.Value
                && snap.Altitude.Value < Settings.DescentWarningAltitude.Value)
            {
                messages.Add(new DiagnosticMessage(Severity.Warning,
                    Loc.Pick($"Descente rapide : {-snap.VerticalSpeed.Value:0} m/s à {snap.Altitude.Value:0} m (seuils {Settings.DescentWarningSpeed.Value:0} m/s, {Settings.DescentWarningAltitude.Value:0} m).", $"Fast descent: {-snap.VerticalSpeed.Value:0} m/s at {snap.Altitude.Value:0} m (thresholds {Settings.DescentWarningSpeed.Value:0} m/s, {Settings.DescentWarningAltitude.Value:0} m).", $"Descenso rápido: {-snap.VerticalSpeed.Value:0} m/s a {snap.Altitude.Value:0} m (umbrales {Settings.DescentWarningSpeed.Value:0} m/s, {Settings.DescentWarningAltitude.Value:0} m).")));
            }
            if (snap.BlackHoleDistance.HasValue)
            {
                float distance = snap.BlackHoleDistance.Value;
                Worlds.Hole hole = snap.BlackHole;
                if (distance < hole.TidalRadius)
                    messages.Add(new DiagnosticMessage(Severity.Error, Loc.Pick($"Forces de marée : {hole.Name} arrache des pièces ({distance / 1000f:0.0} km, horizon à {hole.Horizon / 1000f:0} km).", $"Tidal forces: {hole.Name} is tearing parts off ({distance / 1000f:0.0} km, horizon at {hole.Horizon / 1000f:0} km).", $"Fuerzas de marea: {hole.Name} está arrancando piezas ({distance / 1000f:0.0} km, horizonte a {hole.Horizon / 1000f:0} km).")));
                else if (distance < hole.TidalRadius * 4f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"{hole.Name} proche : {distance / 1000f:0} km, zone de marée à {hole.TidalRadius / 1000f:0} km. Attraction {snap.BlackHoleGravity:0.0} m/s².", $"{hole.Name} near: {distance / 1000f:0} km, tidal zone at {hole.TidalRadius / 1000f:0} km. Pull {snap.BlackHoleGravity:0.0} m/s².", $"{hole.Name} cerca: {distance / 1000f:0} km, zona de marea a {hole.TidalRadius / 1000f:0} km. Atracción {snap.BlackHoleGravity:0.0} m/s².")));
            }
            if (snap.AngularVelocity.HasValue && snap.AngularVelocity.Value.magnitude > Settings.SpinWarningRate.Value)
            {
                messages.Add(new DiagnosticMessage(Severity.Warning,
                    Loc.Pick($"Rotation rapide : {snap.AngularVelocity.Value.magnitude:0} °/s (seuil {Settings.SpinWarningRate.Value:0} °/s).", $"Fast spin: {snap.AngularVelocity.Value.magnitude:0} °/s (threshold {Settings.SpinWarningRate.Value:0} °/s).", $"Giro rápido: {snap.AngularVelocity.Value.magnitude:0} °/s (umbral {Settings.SpinWarningRate.Value:0} °/s).")));
            }
        }

        private static void CheckPropulsion(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            if (snap.Thrusters.Count == 0)
                messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick("Propulseur non détecté sur le vaisseau.", "No thruster found on the ship.", "No se encontró ningún propulsor en la nave.")));

            foreach (ThrusterReading thruster in snap.Thrusters)
            {
                if (thruster.Kind == ThrusterKind.Chemical)
                {
                    if (!thruster.FuelSocketsConnected)
                    {
                        messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"Propulseur chimique #{thruster.PartId} : prises d'ergols 1 et 2 pas toutes les deux branchées.", $"Chemical thruster #{thruster.PartId}: propellant sockets 1 and 2 are not both plugged.", $"Propulsor químico n.º {thruster.PartId}: las tomas de propergol 1 y 2 no están ambas conectadas.")));
                    }
                    else if (IsPropellant(thruster.FluidA) && IsPropellant(thruster.FluidB) && thruster.FluidA == thruster.FluidB)
                    {
                        messages.Add(new DiagnosticMessage(Severity.Error, Loc.Pick($"Propulseur chimique #{thruster.PartId} : les deux conduites portent le même fluide ({FluidName(thruster.FluidA)}), il faut oxygène ET éthanol.", $"Chemical thruster #{thruster.PartId}: both lines carry the same fluid ({FluidName(thruster.FluidA)}), it needs oxygen AND ethanol.", $"Propulsor químico n.º {thruster.PartId}: las dos líneas llevan el mismo fluido ({FluidName(thruster.FluidA)}), necesita oxígeno Y etanol.")));
                    }
                }
                else if (thruster.TroubleStarting)
                {
                    messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"Propulseur à bois #{thruster.PartId} : commandé mais à court de bois.", $"Wood thruster #{thruster.PartId}: commanded but out of wood.", $"Propulsor de madera n.º {thruster.PartId}: activado pero sin madera.")));
                }
            }

            foreach (GyroReading gyro in snap.Gyros)
            {
                if (!gyro.IsSeat && !gyro.PowerConnected)
                    messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"Gyroscope #{gyro.PartId} non alimenté : prise électrique non branchée.", $"Gyroscope #{gyro.PartId} unpowered: power socket not plugged.", $"Giroscopio n.º {gyro.PartId} sin energía: toma de energía no conectada.")));
                else if (gyro.PowerSatisfaction.HasValue && gyro.PowerSatisfaction.Value < 0.999f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"Gyroscope #{gyro.PartId} sous-alimenté : {gyro.PowerSatisfaction.Value:P0} de la puissance demandée.", $"Gyroscope #{gyro.PartId} underpowered: {gyro.PowerSatisfaction.Value:P0} of the power asked.", $"Giroscopio n.º {gyro.PartId} con poca energía: {gyro.PowerSatisfaction.Value:P0} de la energía pedida.")));
            }
        }

        private static void CheckPower(TelemetrySnapshot snap, List<DiagnosticMessage> messages)
        {
            foreach (GeneratorReading generator in snap.Generators)
            {
                if (!generator.Connected)
                    messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"Générateur #{generator.PartId} : sortie électrique non branchée.", $"Generator #{generator.PartId}: power output not plugged.", $"Generador n.º {generator.PartId}: salida de energía no conectada.")));
                else if (!generator.Quantum && generator.Wood <= 0f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"Générateur #{generator.PartId} : plus de bois.", $"Generator #{generator.PartId}: out of wood.", $"Generador n.º {generator.PartId}: sin madera.")));
            }
            foreach (ConsumerReading consumer in snap.Consumers)
            {
                if (consumer.Name != "Gyroscope" && !consumer.Connected)
                    messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"{consumer.Name} #{consumer.PartId} non alimenté : prise électrique non branchée.", $"{consumer.Name} #{consumer.PartId} unpowered: power socket not plugged.", $"{consumer.Name} n.º {consumer.PartId} sin energía: toma de energía no conectada.")));
            }
            foreach (BatteryReading battery in snap.Batteries)
            {
                if (!battery.Connected)
                    messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"Batterie #{battery.PartId} non branchée.", $"Battery #{battery.PartId} not plugged.", $"Batería n.º {battery.PartId} no conectada.")));
                else if (battery.Percent < 5f && battery.Flow < -1f)
                    messages.Add(new DiagnosticMessage(Severity.Warning, Loc.Pick($"Batterie #{battery.PartId} presque vide ({battery.Percent:0} %) et en décharge.", $"Battery #{battery.PartId} almost empty ({battery.Percent:0}%) and discharging.", $"Batería n.º {battery.PartId} casi vacía ({battery.Percent:0}%) y descargándose.")));
            }
            foreach (BreakerReading breaker in snap.Breakers)
            {
                if (breaker.Status == Power.PowerCatalog.BreakerTripped)
                    messages.Add(new DiagnosticMessage(Severity.Error, breaker.ControlWired
                        ? Loc.Pick($"Disjoncteur #{breaker.PartId} disjoncté (surcharge) : repasser sa commande à 0 puis > 0 pour le réarmer.", $"Breaker #{breaker.PartId} tripped (overload): set its command back to 0 then > 0 to reset it.", $"Disyuntor n.º {breaker.PartId} saltado (sobrecarga): pon su mando a 0 y luego > 0 para rearmarlo.")
                        : Loc.Pick($"Disjoncteur #{breaker.PartId} disjoncté (surcharge) : réarmement automatique dans {Power.PowerCatalog.BreakerAutoResetSeconds.Value:0} s.", $"Breaker #{breaker.PartId} tripped (overload): automatic reset in {Power.PowerCatalog.BreakerAutoResetSeconds.Value:0} s.", $"Disyuntor n.º {breaker.PartId} saltado (sobrecarga): rearme automático en {Power.PowerCatalog.BreakerAutoResetSeconds.Value:0} s.")));
                else if (breaker.Status == Power.PowerCatalog.BreakerOpen)
                    messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"Disjoncteur #{breaker.PartId} ouvert par sa commande.", $"Breaker #{breaker.PartId} opened by its command.", $"Disyuntor n.º {breaker.PartId} abierto por su mando.")));
                if (!breaker.InputConnected || !breaker.OutputConnected)
                    messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"Disjoncteur #{breaker.PartId} : entrée et sortie doivent être branchées sur deux réseaux différents.", $"Breaker #{breaker.PartId}: input and output must be plugged into two different grids.", $"Disyuntor n.º {breaker.PartId}: la entrada y la salida deben conectarse a dos redes distintas.")));
            }
            if (snap.IsHost)
            {
                foreach (Power.PowerGrid.Trip trip in Power.PowerGrid.Journal)
                {
                    float ago = UnityEngine.Time.unscaledTime - trip.Time;
                    if (ago > 300f)
                        break;
                    messages.Add(new DiagnosticMessage(Severity.Info, Loc.Pick($"Journal : disjoncteur {trip.ShipId}:{trip.PartId} déclenché il y a {ago:0} s ({trip.DemandWatts:0} W demandés).", $"Log: breaker {trip.ShipId}:{trip.PartId} tripped {ago:0} s ago ({trip.DemandWatts:0} W asked).", $"Registro: disyuntor {trip.ShipId}:{trip.PartId} saltó hace {ago:0} s ({trip.DemandWatts:0} W pedidos).")));
                }
            }
            foreach (PowerNetworkReading network in snap.Networks)
            {
                if (network.Satisfaction.HasValue && network.Satisfaction.Value < 0.999f)
                {
                    messages.Add(new DiagnosticMessage(network.Satisfaction.Value < 0.5f ? Severity.Error : Severity.Warning,
                        Loc.Pick($"Réseau électrique sous-alimenté : {network.Demand:0} W demandés, {network.Capacity:0} W disponibles ({network.Satisfaction.Value:P0}).", $"Underpowered grid: {network.Demand:0} W asked, {network.Capacity:0} W available ({network.Satisfaction.Value:P0}).", $"Red con poca energía: {network.Demand:0} W pedidos, {network.Capacity:0} W disponibles ({network.Satisfaction.Value:P0}).")));
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
                        Loc.Pick($"Rapprochement : vaisseau #{contact.ShipId} à {contact.ClosestDistance:0} m (centre à centre) dans {contact.TimeToClosest.Value:0.0} s.", $"Closing: ship #{contact.ShipId} at {contact.ClosestDistance:0} m (centre to centre) in {contact.TimeToClosest.Value:0.0} s.", $"Acercamiento: nave n.º {contact.ShipId} a {contact.ClosestDistance:0} m (centro a centro) en {contact.TimeToClosest.Value:0.0} s.")));
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
                return Loc.Pick("vide", "empty", "vacío");
            if (itemId == ItemsConstants.OxygenItemId)
                return Loc.Pick("oxygène", "oxygen", "oxígeno");
            if (itemId == ItemsConstants.EthanolItemId)
                return Loc.Pick("éthanol", "ethanol", "etanol");
            return Loc.Pick("fluide ", "fluid ", "fluido ") + itemId;
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
