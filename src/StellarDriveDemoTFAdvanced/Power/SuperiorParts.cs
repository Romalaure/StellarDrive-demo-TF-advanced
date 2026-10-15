using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Items;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Power
{
    /// <summary>
    /// The superior machines, run on dark matter and built with meteorite: the dark matter
    /// collector, its tank, the dark matter reactor and the portal generator. Same style as the
    /// other machines, in violet. Only call when SDModKit is loaded.
    /// </summary>
    internal static class SuperiorParts
    {
        private const uint Copper = 112, Glass = 102;

        public static void Register()
        {
            PartFactory.Register(PowerCatalog.DarkCollectorId, "SignalDisplay", AdvancedTab.Name, AdvancedTab.SuperiorRow, ConfigureCollector);
            PartFactory.Register(PowerCatalog.DarkTankId, "FluidTank", AdvancedTab.Name, AdvancedTab.SuperiorRow, ConfigureTank);
            PartFactory.Register(InfiniteDarkTank.Id, "FluidTank", AdvancedTab.Name, AdvancedTab.SuperiorRow, ConfigureInfiniteTank);
            PartFactory.Register(PowerCatalog.DarkReactorId, "SignalDisplay", AdvancedTab.Name, AdvancedTab.SuperiorRow, ConfigureReactor);
            PartFactory.Register(PowerCatalog.PortalId, "SignalDisplay", AdvancedTab.Name, AdvancedTab.SuperiorRow, ConfigurePortal);
            AdvancedMod.Log.Msg("registered the dark matter collector, tank and reactor, and the portal generator");
        }

        private static void ConfigureCollector(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Collecteur de matière noire", "Dark matter collector", "Colector de materia oscura"),
                Loc.Pick($"Une antenne de météorite qui capte la matière noire du vide et la condense dans la conduite branchée sur sa prise de fluide (vide ou déjà de matière noire) : {PowerCatalog.DarkCollectorPerSecond.Value:0.###} unité/s, cinq fois plus près d'un trou noir, trois fois plus dans la galaxie d'Érèbe. Consomme {PowerCatalog.DarkCollectorWatts.Value:0} W. Sa prise signal donne sa production en dix-millièmes d'unité/s.", $"A meteorite antenna that catches dark matter out of the void and condenses it into the line on its fluid socket (empty or already dark matter): {PowerCatalog.DarkCollectorPerSecond.Value:0.###} unit/s, five times more near a black hole, three times more in the Erebos galaxy. Uses {PowerCatalog.DarkCollectorWatts.Value:0} W. Its signal socket gives its output in ten-thousandths of a unit/s.", $"Una antena de meteorito que capta materia oscura del vacío y la condensa en la línea de su toma de fluido (vacía o ya de materia oscura): {PowerCatalog.DarkCollectorPerSecond.Value:0.###} unidad/s, cinco veces más cerca de un agujero negro, tres veces más en la galaxia de Érebo. Consume {PowerCatalog.DarkCollectorWatts.Value:0} W. Su toma de señal da su producción en diezmilésimas de unidad/s."),
                150f, new[] { (ModItems.Meteorite, 8), (ModItems.QuantumCore, 1), (ModItems.Titanium, 6), (Copper, 10) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 1f, height = 0.6f, depth = 1f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.75f, 0f), new Vector3(width, 1.5f, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.7f);
            float face = depth * 0.47f;
            MachineModels.Vent(model, -0.25f, 0.25f, face, 0.22f, 0.24f, 3);
            MachineModels.Vent(model, 0.25f, 0.25f, face, 0.22f, 0.24f, 3);
            MachineModels.Grille(model, 0.25f, face, 0.1f, 0.26f);
            // A mast of meteorite iron carrying a wide funnel open to the sky, three violet rings inside
            model.Armor.Cylinder(new Vector3(0f, height, 0f), new Vector3(0f, height + 0.3f, 0f), 0.08f, 0.06f, 8);
            model.Armor.Cylinder(new Vector3(0f, height + 0.3f, 0f), new Vector3(0f, height + 0.85f, 0f), 0.08f, 0.45f, 12, capA: true, capB: false);
            model.Dark.Cylinder(new Vector3(0f, height + 0.32f, 0f), new Vector3(0f, height + 0.84f, 0f), 0.06f, 0.42f, 12, capA: false, capB: false);
            for (int i = 0; i < 3; i++)
            {
                float y = height + 0.45f + i * 0.13f;
                float r = 0.12f + i * 0.1f;
                model.Glow.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.02f, 0f), r, r, 16, capA: false, capB: false);
            }
            model.Core.Cylinder(new Vector3(0f, height + 0.32f, 0f), new Vector3(0f, height + 0.5f, 0f), 0.05f, 0f, 6);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.right;
                model.Light.Box(new Vector3(0f, height + 0.6f, 0f) + dir * 0.32f, new Vector3(0.015f, 0.5f, 0.015f), Quaternion.FromToRotation(Vector3.up, (Vector3.up + dir * 0.75f).normalized));
            }
            MachineModels.Attach(visuals, model, "DarkCollector", MachineModels.Violet, MachineModels.Violet);
            Sockets(prefab, width * 0.47f, face, 0.25f);
        }

        private static void ConfigureTank(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Réservoir de matière noire", "Dark matter tank", "Tanque de materia oscura"),
                Loc.Pick("Le réservoir de fluide du jeu, blindé de météorite pour garder la matière noire (il prend aussi les autres fluides). Branchement et prise signal comme le réservoir du jeu.", "The game's fluid tank, armoured with meteorite to hold dark matter (it takes the other fluids too). Wiring and signal socket as the game's tank.", "El tanque de fluido del juego, blindado con meteorito para contener materia oscura (también acepta los otros fluidos). Conexiones y toma de señal como el tanque del juego."),
                30f, new[] { (ModItems.Meteorite, 2), (ModItems.Titanium, 4), (Glass, 4) });
            Recolor(prefab, new Color(0.45f, 0.38f, 0.6f), new Color(0.08f, 0.02f, 0.14f));
        }

        private static void ConfigureInfiniteTank(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Réservoir de matière noire infini", "Infinite dark matter tank", "Tanque de materia oscura infinito"),
                Loc.Pick("Un réservoir de fluide toujours plein de matière noire : ce que les conduites y puisent se recrée aussitôt, et son contenu ne pèse rien. Pour alimenter sans fin un réacteur à matière noire ou un générateur de portail. Branchement et prise signal comme le réservoir du jeu.", "A fluid tank always full of dark matter: whatever the lines draw from it is made again at once, and its contents weigh nothing. To feed a dark matter reactor or a portal generator without end. Wiring and signal socket as the game's tank.", "Un tanque de fluido siempre lleno de materia oscura: lo que las líneas extraen se rehace al instante, y su contenido no pesa nada. Para alimentar sin fin un reactor de materia oscura o un generador de portal. Conexiones y toma de señal como el tanque del juego."),
                30f, new[] { (ModItems.Meteorite, 20), (ModItems.QuantumCore, 2), (ModItems.ExoticMatter, 2), (ModItems.Titanium, 8) });
            // Darker shell, a stronger violet glow than the plain dark matter tank
            Recolor(prefab, new Color(0.3f, 0.22f, 0.45f), new Color(0.35f, 0.08f, 0.6f));
        }

        private static void ConfigureReactor(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Réacteur à matière noire", "Dark matter reactor", "Reactor de materia oscura"),
                Loc.Pick($"Brûle {PowerCatalog.DarkReactorBurnPerSecond.Value:0.###} unité/s de matière noire prise sur sa prise de fluide et donne {PowerCatalog.DarkReactorWatts.Value:0} W aux réseaux branchés sur ses deux prises électriques (partagés s'il y en a deux). Sa prise signal vaut 100 quand il tourne, 0 à sec.", $"Burns {PowerCatalog.DarkReactorBurnPerSecond.Value:0.###} unit/s of dark matter from its fluid socket and gives {PowerCatalog.DarkReactorWatts.Value:0} W to the networks on its two power sockets (shared if there are two). Its signal socket reads 100 while it runs, 0 when dry.", $"Quema {PowerCatalog.DarkReactorBurnPerSecond.Value:0.###} unidad/s de materia oscura de su toma de fluido y da {PowerCatalog.DarkReactorWatts.Value:0} W a las redes de sus dos tomas de energía (repartidos si hay dos). Su toma de señal da 100 mientras funciona, 0 cuando está seco."),
                260f, new[] { (ModItems.Meteorite, 12), (ModItems.QuantumCore, 2), (ModItems.NexoranAlloy, 6), (ModItems.Titanium, 10) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 1.2f, height = 1.1f, depth = 1.2f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.75f, 0f), new Vector3(width, 1.5f, depth));
            var model = new MachineModels.Model();
            // A low armored base, four pillars and a cage around a dark sphere, coils on top
            model.Armor.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.1f, 0f), width * 0.58f, width * 0.56f, 8);
            MachineModels.Housing(model, width * 0.92f, 0.5f, depth * 0.92f, 0.85f);
            float face = depth * 0.92f * 0.47f;
            MachineModels.Vent(model, -0.3f, 0.26f, face, 0.24f, 0.26f, 4);
            MachineModels.Vent(model, 0.3f, 0.26f, face, 0.24f, 0.26f, 4);
            MachineModels.Grille(model, 0.26f, face, 0.12f, 0.28f);
            MachineModels.Vent(model, 0f, 0.26f, -face, 0.5f, 0.26f, 4);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
            {
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0.42f, 0f, 0f);
                model.Armor.Box(p + new Vector3(0f, 0.5f + 0.32f, 0f), new Vector3(0.09f, 0.64f, 0.09f), Quaternion.Euler(0f, a, 0f));
                model.Light.Box(p * 1.12f + new Vector3(0f, 0.5f + 0.32f, 0f), new Vector3(0.012f, 0.56f, 0.012f), Quaternion.Euler(0f, a, 0f));
            }
            model.Armor.Cylinder(new Vector3(0f, height - 0.04f, 0f), new Vector3(0f, height + 0.02f, 0f), 0.5f, 0.46f, 8);
            model.Dark.Cylinder(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.56f, 0f), 0.3f, 0.3f, 12);
            // The dark matter sphere (two cones) with glowing rings around it
            model.Core.Cylinder(new Vector3(0f, 0.58f, 0f), new Vector3(0f, 0.78f, 0f), 0.04f, 0.2f, 12, capA: true, capB: false);
            model.Core.Cylinder(new Vector3(0f, 0.78f, 0f), new Vector3(0f, 0.98f, 0f), 0.2f, 0.04f, 12, capA: false, capB: true);
            foreach (float y in new[] { 0.68f, 0.78f, 0.88f })
                model.Glow.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.015f, 0f), 0.3f, 0.3f, 16, capA: false, capB: false);
            for (int i = 0; i < 3; i++)
                model.Glow.Cylinder(new Vector3(0f, height + 0.04f + i * 0.05f, 0f), new Vector3(0f, height + 0.06f + i * 0.05f, 0f), 0.36f - i * 0.06f, 0.36f - i * 0.06f, 16, capA: false, capB: false);
            MachineModels.Attach(visuals, model, "DarkReactor", MachineModels.Violet, MachineModels.Violet);
            Sockets(prefab, width * 0.92f * 0.47f, face, 0.26f);
        }

        private static void ConfigurePortal(PartSettings settings, GameObject prefab)
        {
            PartFactory.Describe(settings, Loc.Pick("Générateur de portail", "Portal generator", "Generador de portal"),
                Loc.Pick($"Ouvre un portail droit devant lui, à {PowerCatalog.PortalDistance.Value:0} m : le vaisseau qui le traverse ressort à côté de la cible choisie sur son ordinateur de visée ou sa carte stellaire (au-dessus d'une planète, ou près d'une balise). Un signal > 0 sur la prise de commande le lance : il charge {PowerCatalog.PortalChargeSeconds.Value:0} s à {PowerCatalog.PortalWatts.Value:0} W, brûle {PowerCatalog.PortalDarkMatter.Value:0} unités de matière noire (prise de fluide) et le portail reste ouvert {PowerCatalog.PortalOpenSeconds.Value:0} s, puis {PowerCatalog.PortalCooldownSeconds.Value:0} s de recharge. Prise d'état : 0 prêt, 1-99 charge %, 100 ouvert, -1 pas de cible, -2 pas assez de matière noire, -3 pas de courant, -10 et moins : secondes de recharge.", $"Opens a portal straight ahead, {PowerCatalog.PortalDistance.Value:0} m away: the ship that goes through comes out beside the target picked on its targeting computer or star map (above a planet, or near a beacon). A signal > 0 on the control socket starts it: it charges {PowerCatalog.PortalChargeSeconds.Value:0} s at {PowerCatalog.PortalWatts.Value:0} W, burns {PowerCatalog.PortalDarkMatter.Value:0} units of dark matter (fluid socket) and the portal stays open {PowerCatalog.PortalOpenSeconds.Value:0} s, then {PowerCatalog.PortalCooldownSeconds.Value:0} s to recharge. Status socket: 0 ready, 1-99 charge %, 100 open, -1 no target, -2 not enough dark matter, -3 no power, -10 and below: seconds of recharge.", $"Abre un portal justo delante, a {PowerCatalog.PortalDistance.Value:0} m: la nave que lo atraviesa sale junto al objetivo elegido en su ordenador de navegación o mapa estelar (sobre un planeta, o cerca de una baliza). Una señal > 0 en la toma de mando lo activa: se carga {PowerCatalog.PortalChargeSeconds.Value:0} s a {PowerCatalog.PortalWatts.Value:0} W, quema {PowerCatalog.PortalDarkMatter.Value:0} unidades de materia oscura (toma de fluido) y el portal queda abierto {PowerCatalog.PortalOpenSeconds.Value:0} s, luego {PowerCatalog.PortalCooldownSeconds.Value:0} s de recarga. Toma de estado: 0 listo, 1-99 % de carga, 100 abierto, -1 sin objetivo, -2 materia oscura insuficiente, -3 sin energía, -10 y menos: segundos de recarga."),
                400f, new[] { (ModItems.Meteorite, 25), (ModItems.QuantumCore, 3), (ModItems.ExoticMatter, 5), (ModItems.Artifact, 3), (ModItems.NexoranAlloy, 10) });
            settings.internalStateClass = PowerCatalog.MachineStateClass;
            const float width = 1.6f, height = 0.4f, depth = 0.9f;
            Transform visuals = PartFactory.BlankFromSignalDisplay(prefab, new Vector3(0f, 0.95f, 0f), new Vector3(width, 1.9f, depth));
            var model = new MachineModels.Model();
            MachineModels.Housing(model, width, height, depth, 0.8f);
            float face = depth * 0.47f;
            MachineModels.Vent(model, -0.5f, 0.17f, face, 0.3f, 0.16f, 3);
            MachineModels.Vent(model, 0.5f, 0.17f, face, 0.3f, 0.16f, 3);
            MachineModels.Grille(model, 0.17f, face, 0.3f, 0.16f);
            // Two pylons holding an upright ring that faces forward (+Z), where the portal will open
            const float ringY = 1.15f, ringR = 0.6f;
            foreach (float x in new[] { -0.72f, 0.72f })
            {
                model.Armor.Box(new Vector3(x, height + 0.38f, 0f), new Vector3(0.14f, 0.76f, 0.3f));
                model.Light.Box(new Vector3(x, height + 0.38f, 0.155f), new Vector3(0.012f, 0.7f, 0.012f));
                model.Armor.Box(new Vector3(x * 0.92f, ringY, 0f), new Vector3(0.18f, 0.16f, 0.22f));
            }
            // The outer ring: twelve armoured sections, a seam lit with the charge between each, clamps outside
            var ring = new MeshBuilder();
            const int segments = 36;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 pa = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringR + Vector3.up * ringY;
                Vector3 pb = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * ringR + Vector3.up * ringY;
                if (i % 3 == 2)
                    ring.Cylinder(pa, pb, 0.062f, 0.062f, 10, capA: false, capB: false);
                else
                    model.Armor.Cylinder(pa, pb, 0.078f, 0.078f, 10, capA: false, capB: false);
                ring.Cylinder(pa + Vector3.forward * 0.07f, pb + Vector3.forward * 0.07f, 0.02f, 0.02f, 6, capA: false, capB: false);
                ring.Cylinder(pa - Vector3.forward * 0.07f, pb - Vector3.forward * 0.07f, 0.014f, 0.014f, 6, capA: false, capB: false);
                if (i % 3 == 0)
                {
                    Vector3 outward = new Vector3(Mathf.Cos(a + 0.087f), Mathf.Sin(a + 0.087f), 0f);
                    model.Dark.Box(outward * (ringR + 0.075f) + Vector3.up * ringY, new Vector3(0.1f, 0.06f, 0.2f), Quaternion.Euler(0f, 0f, (a + 0.087f) * Mathf.Rad2Deg));
                }
            }
            // Four focusing emitters on the ring's front, aimed at the middle
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + k * 90f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 root = dir * (ringR - 0.02f) + new Vector3(0f, ringY, 0.09f);
                model.Dark.Cylinder(root, root - dir * 0.12f + Vector3.forward * 0.02f, 0.045f, 0.02f, 8);
                model.Core.Cylinder(root - dir * 0.12f + Vector3.forward * 0.02f, root - dir * 0.14f + Vector3.forward * 0.02f, 0.02f, 0.012f, 8);
            }
            // The gimbal's pivots, top and bottom of the ring
            foreach (float s in new[] { -1f, 1f })
                model.Dark.Cylinder(new Vector3(0f, ringY + s * ringR, 0f), new Vector3(0f, ringY + s * ringR * 0.8f, 0f), 0.025f, 0.025f, 8);
            PortalDetails(model, width, height, depth, ringY, ringR);
            MachineModels.Attach(visuals, model, "Portal", MachineModels.Violet, MachineModels.Violet);
            int layer = visuals.gameObject.layer;
            Material glow = LitMaterials.Get("TFA_PortalRingPart", Color.white, 0f, 0.8f, Color.white);
            Material metal = LitMaterials.Get("TFA_PortalGimbal", new Color(0.2f, 0.18f, 0.24f), 0.85f, 0.6f);
            var ringObject = new GameObject(PortalVisuals.RingName) { layer = layer };
            ringObject.transform.SetParent(visuals, false);
            ringObject.AddComponent<MeshFilter>().sharedMesh = ring.Build(PortalVisuals.RingName);
            ringObject.AddComponent<MeshRenderer>().sharedMaterial = glow;
            // Two gimbal rings, the inner one turning inside the outer (a gyroscope), and a floating crystal
            Transform gimbalA = Gimbal(visuals, PortalVisuals.GimbalAName, new Vector3(0f, ringY, 0f), ringR * 0.8f, ringR * 0.64f, metal, glow, layer);
            Gimbal(gimbalA, PortalVisuals.GimbalBName, Vector3.zero, ringR * 0.64f, 0f, metal, glow, layer);
            var crystal = new MeshBuilder();
            crystal.Cylinder(new Vector3(0f, -0.17f, 0f), Vector3.zero, 0.002f, 0.085f, 6, capA: false, capB: false);
            crystal.Cylinder(Vector3.zero, new Vector3(0f, 0.17f, 0f), 0.085f, 0.002f, 6, capA: false, capB: false);
            Transform crystalObject = Piece(visuals, PortalVisuals.CrystalName, new Vector3(0f, ringY, 0f), layer);
            Renderer crystalGlow = Piece(crystalObject, "CrystalGlow", Vector3.zero, layer, crystal.Build("TFA_PortalCrystal"), glow);
            visuals.gameObject.AddComponent<PortalVisuals>();
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, 0.17f, -face, 0.24f,
                (PowerCatalog.MachinePowerA, CableType.Power), (PowerCatalog.MachineFluid, CableType.Fluid), (Portal.CommandSocket, CableType.Signal),
                (PowerCatalog.MachineSignal, CableType.Signal), (PowerCatalog.MachinePowerB, CableType.Power));
        }

        /// <summary>A gimbal ring facing forward: armoured band, lit runes on its faces, axles out to the next ring at ±X.</summary>
        private static Transform Gimbal(Transform parent, string name, Vector3 at, float radius, float inner, Material metal, Material glow, int layer)
        {
            var band = new MeshBuilder { DoubleSided = true };
            var runes = new MeshBuilder { DoubleSided = true };
            const int sides = 32;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 pa = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius, pb = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * radius;
                band.Box((pa + pb) * 0.5f, new Vector3((pb - pa).magnitude + 0.004f, 0.026f, 0.05f), Quaternion.Euler(0f, 0f, (a + b) * 0.5f * Mathf.Rad2Deg + 90f));
                if (i % 2 == 0)
                    foreach (float z in new[] { -0.027f, 0.027f })
                        runes.Box((pa + pb) * 0.5f + Vector3.forward * z, new Vector3(i % 8 == 0 ? 0.035f : 0.016f, 0.01f, 0.003f), Quaternion.Euler(0f, 0f, (a + b) * 0.5f * Mathf.Rad2Deg + 90f));
            }
            // Axles out to the outer ring (at ±Y for the outer gimbal, ±X inside it)
            if (inner > 0f)
                foreach (float s in new[] { -1f, 1f })
                    band.Cylinder(new Vector3(s * inner, 0f, 0f), new Vector3(s * radius, 0f, 0f), 0.016f, 0.016f, 8);
            Transform holder = Piece(parent, name, at, layer);
            Piece(holder, "Band", Vector3.zero, layer, band.Build(name + "Band"), metal);
            Piece(holder, "Glow", Vector3.zero, layer, runes.Build(name + "Glow"), glow);
            return holder;
        }

        private static Transform Piece(Transform parent, string name, Vector3 at, int layer)
        {
            var child = new GameObject(name) { layer = layer };
            child.transform.SetParent(parent, false);
            child.transform.localPosition = at;
            return child.transform;
        }

        private static Renderer Piece(Transform parent, string name, Vector3 at, int layer, Mesh mesh, Material material)
        {
            Transform child = Piece(parent, name, at, layer);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>
        /// The portal generator's finer parts: glowing coils and cooling fins on the pylons,
        /// conduits up to the ring clamps, chevrons around the ring, capacitor banks and a
        /// control desk at the back, an emitter lens under the ring.
        /// </summary>
        private static void PortalDetails(MachineModels.Model model, float width, float height, float depth, float ringY, float ringR)
        {
            foreach (float x in new[] { -0.72f, 0.72f })
            {
                float outward = Mathf.Sign(x);
                // Coils around the pylon, fins on its outer face, a conduit up to the ring clamp
                for (int i = 0; i < 4; i++)
                    model.Glow.Box(new Vector3(x, height + 0.14f + i * 0.16f, 0f), new Vector3(0.165f, 0.022f, 0.325f));
                for (int i = 0; i < 6; i++)
                    model.Armor.Box(new Vector3(x + outward * 0.09f, height + 0.1f + i * 0.12f, 0f), new Vector3(0.05f, 0.05f, 0.26f));
                model.Dark.Cylinder(new Vector3(x * 0.82f, height, 0.2f), new Vector3(x * 0.82f, ringY - 0.12f, 0.17f), 0.025f, 0.025f, 8);
                model.Dark.Cylinder(new Vector3(x * 0.82f, ringY - 0.12f, 0.17f), new Vector3(x * 0.88f, ringY - 0.02f, 0.1f), 0.025f, 0.025f, 8);
                // Capacitor banks behind the pylon: three cells with a glowing band and a cap
                for (int i = 0; i < 3; i++)
                {
                    Vector3 b = new Vector3(x - outward * (0.12f + i * 0.14f), height, -depth * 0.32f);
                    model.Armor.Cylinder(b, b + Vector3.up * 0.34f, 0.055f, 0.055f, 10);
                    model.Glow.Cylinder(b + Vector3.up * 0.2f, b + Vector3.up * 0.23f, 0.058f, 0.058f, 10, capA: false, capB: false);
                    model.Dark.Cylinder(b + Vector3.up * 0.34f, b + Vector3.up * 0.37f, 0.045f, 0.035f, 10);
                }
            }
            // Chevrons around the ring's front, lit
            for (int k = 0; k < 9; k++)
            {
                float a = (90f + k * 40f) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (ringR + 0.075f) + new Vector3(0f, ringY, 0.07f);
                Quaternion turn = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
                model.Armor.Box(at, new Vector3(0.12f, 0.06f, 0.06f), turn);
                model.Glow.Box(at + Vector3.forward * 0.031f, new Vector3(0.07f, 0.025f, 0.004f), turn);
            }
            // An emitter lens under the ring, ringed with light
            model.Armor.Cylinder(new Vector3(0f, height, 0.05f), new Vector3(0f, height + 0.05f, 0.05f), 0.2f, 0.17f, 16);
            model.Core.Cylinder(new Vector3(0f, height + 0.05f, 0.05f), new Vector3(0f, height + 0.08f, 0.05f), 0.12f, 0.09f, 16);
            model.Glow.Cylinder(new Vector3(0f, height + 0.05f, 0.05f), new Vector3(0f, height + 0.055f, 0.05f), 0.19f, 0.19f, 20, capA: false, capB: false);
            // The control desk at the back: a tilted screen and a row of keys
            Quaternion tilt = Quaternion.Euler(-35f, 0f, 0f);
            Vector3 desk = new Vector3(0f, height + 0.13f, -depth * 0.36f);
            model.Armor.Box(desk + new Vector3(0f, -0.07f, 0f), new Vector3(0.36f, 0.12f, 0.14f));
            model.Armor.Box(desk, new Vector3(0.36f, 0.2f, 0.04f), tilt);
            model.Dark.Box(desk + tilt * new Vector3(0f, 0.01f, -0.022f), new Vector3(0.3f, 0.14f, 0.004f), tilt);
            model.Glow.Box(desk + tilt * new Vector3(0f, 0.01f, -0.025f), new Vector3(0.26f, 0.1f, 0.002f), tilt);
            for (int i = 0; i < 6; i++)
                model.Light.Box(desk + new Vector3(-0.125f + i * 0.05f, -0.005f, -0.085f), new Vector3(0.03f, 0.012f, 0.025f));
            // Warning lights at the base's front corners
            foreach (float x in new[] { -width * 0.42f, width * 0.42f })
                model.Core.Cylinder(new Vector3(x, height, depth * 0.38f), new Vector3(x, height + 0.05f, depth * 0.38f), 0.03f, 0.02f, 8);
        }

        // Every socket in one row at the back: power, fluid, signal, power
        internal static void Sockets(GameObject prefab, float side, float face, float y)
        {
            var bounds = prefab.GetComponent<ShipPartBounds>();
            PowerParts.GraftRow(prefab, bounds, y, -face, 0.2f,
                (PowerCatalog.MachinePowerA, CableType.Power), (PowerCatalog.MachineFluid, CableType.Fluid), (PowerCatalog.MachineSignal, CableType.Signal), (PowerCatalog.MachinePowerB, CableType.Power));
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        internal static void Recolor(GameObject prefab, Color color, Color glow)
        {
            var copies = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<SocketObject>() != null)
                    continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null || original.name.StartsWith("Plugs") || !original.HasProperty(ColorId) || original.shader == null || original.shader.name != "Custom/PlanetObject")
                        continue;
                    if (!copies.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original) { name = original.name + "_DarkTank" };
                        copy.SetColor(ColorId, original.GetColor(ColorId) * color);
                        if (copy.HasProperty(EmissionColorId))
                        {
                            copy.EnableKeyword("_EMISSION");
                            copy.SetColor(EmissionColorId, glow);
                        }
                        copies[original] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
