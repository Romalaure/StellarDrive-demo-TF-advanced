using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Players.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Common.Model;
using Ships.Parts.Common.Model.Reactions;
using Ships.Parts.Common.Utils;
using Ships.Interface.Model.Parts.StateTypes;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Construction
{
    /// <summary>
    /// What makes the station modules' furniture usable: the habitation's bed (the game's bed
    /// interaction: wait), its locker wardrobe (the part's own storage), and the consoles (they
    /// open the mod's wiki in the guide).
    /// </summary>
    internal static class Usable
    {
        private const ushort GameBed = 43, GameChest = 30;

        public static void Habitation(StationModules.Spec spec, GameObject prefab)
        {
            Transform interactions = prefab.transform.Find("Interactions");
            if (interactions == null)
                return;
            float x = spec.Size.x * 0.5f - StationModules.Wall, z = spec.Size.z * 0.5f;
            // The storage opens from the lockers by the front door
            foreach (PartInteraction chest in interactions.GetComponentsInChildren<PartInteraction>(true))
            {
                chest.transform.localPosition = spec.Offset + new Vector3(x - 0.3f, 0f, -z + 1.35f);
                chest.transform.localRotation = Quaternion.identity;
                foreach (BoxCollider box in chest.GetComponents<BoxCollider>())
                {
                    box.center = new Vector3(0f, 1f, 0f);
                    box.size = new Vector3(0.55f, 2f, 1.35f);
                }
            }
            // The lower bunk is the game's bed
            PartSettings bed = Resources.FindObjectsOfTypeAll<PartSettings>().FirstOrDefault(p => p != null && p.id == GameBed);
            Transform bedInteraction = bed != null && bed.part != null ? bed.part.transform.Find("Interactions/Bed") : null;
            if (bedInteraction != null)
            {
                Transform copy = Object.Instantiate(bedInteraction.gameObject, interactions, false).transform;
                copy.name = "Bed";
                // The game's bed lies along X; the bunk along Z
                copy.localPosition = spec.Offset + new Vector3(-x + 0.45f, 0.35f, 0.4f);
                copy.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
                AdvancedMod.Log.Warning("habitation: the game's bed was not found, the bunk is decoration");
            Console(prefab, spec.Offset + new Vector3(StationModules.ConsoleX, 1.3f, z - StationModules.Wall - 0.15f), new Vector3(0.8f, 0.8f, 0.3f));
        }

        /// <summary>An interaction box that opens the mod's wiki in the guide.</summary>
        public static void Console(GameObject prefab, Vector3 at, Vector3 size)
        {
            Transform interactions = prefab.transform.Find("Interactions");
            if (interactions == null)
            {
                interactions = new GameObject("Interactions").transform;
                interactions.SetParent(prefab.transform, false);
            }
            var console = new GameObject("TFA_Console") { layer = 6, tag = "Interactable" };
            console.transform.SetParent(interactions, false);
            console.transform.localPosition = at;
            console.AddComponent<BoxCollider>().size = size;
            var interaction = console.AddComponent<StationConsoleInteraction>();
            // The label of the game's chest ("use")
            PartSettings chest = Resources.FindObjectsOfTypeAll<PartSettings>().FirstOrDefault(p => p != null && p.id == GameChest);
            PartInteraction label = chest != null && chest.part != null ? chest.part.GetComponentInChildren<PartInteraction>(true) : null;
            if (label != null)
                StationConsoleInteraction.Configs(interaction) = new List<PartInteractionConfig>(label.Interactions);
        }
    }

    /// <summary>A station console: using it opens the guide on the mod's wiki.</summary>
    internal sealed class StationConsoleInteraction : PartInteraction
    {
        public static readonly AccessTools.FieldRef<PartInteraction, List<PartInteractionConfig>> Configs =
            AccessTools.FieldRefAccess<PartInteraction, List<PartInteractionConfig>>("interactions");
        private static readonly AccessTools.FieldRef<UI.Guide.GuideMenuActivator, uint?> Previous =
            AccessTools.FieldRefAccess<UI.Guide.GuideMenuActivator, uint?>("_previousSelectedEntryId");

        public override bool Interact(InteractionType type)
        {
            if (type != InteractionType.Interact)
                return base.Interact(type);
            var guide = FindObjectsByType<UI.Guide.GuideMenuActivator>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (guide == null)
                return false;
            Previous(guide) = Wiki.WikiGuide.EntryId;
            if (!guide.IsGuideActive)
                guide.ToggleGuide();
            return true;
        }
    }

    /// <summary>
    /// The airlock's two doors, moved by its lever: lever down, the inner door (back) is open and
    /// the outer one (front) shut; lever up, the other way round.
    /// </summary>
    internal sealed class AirlockDoors : MonoBehaviour, IContextAwarePart
    {
        public const string Outer = "TFA_AirlockOuter", Inner = "TFA_AirlockInner";
        // Into the wall beside the doorway (0.55 m of wall on each side in the airlock)
        private const float Slide = 0.56f, Speed = 1.2f;

        private readonly List<(Transform Leaf, Vector3 Closed, float Direction, bool OuterDoor)> _leaves = new List<(Transform, Vector3, float, bool)>();
        private PartContext _part;
        private bool _hasContext;
        private float _open, _target, _next;

        public void SetContext(ShipPartContext context)
        {
            _part = context.Part;
            _hasContext = true;
        }

        private void Awake()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                bool outer = child.name.StartsWith(Outer), inner = child.name.StartsWith(Inner);
                if (!outer && !inner)
                    continue;
                _leaves.Add((child, child.localPosition, child.name.EndsWith("L") ? -1f : 1f, outer));
            }
        }

        private void Update()
        {
            // Only placed parts move their doors: the part's template (and build previews) stay shut
            if (!_hasContext)
                return;
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.1f;
                var ships = ServiceLocator.GetService<IShipsClientProvider>();
                if (ships != null && ships.TryGetPartState(_part, out LeverState lever))
                    _target = AirlockColliders.OuterOpen(lever) ? 1f : 0f;
            }
            _open = Mathf.MoveTowards(_open, _target, Time.deltaTime * Speed);
            foreach ((Transform leaf, Vector3 closed, float direction, bool outerDoor) in _leaves)
            {
                // The outer door opens as the lever goes up, the inner one as it goes down
                float amount = outerDoor ? _open : 1f - _open;
                leaf.localPosition = closed + Vector3.right * direction * Slide * amount;
            }
        }
    }

    /// <summary>The airlock doors' collision follows the lever too (see <see cref="ModuleColliders"/>).</summary>
    internal sealed class AirlockColliders : MonoBehaviour
    {
        public static bool OuterOpen(LeverState lever) => lever != null && lever.LeverHeight > 0.5f;

        public void Apply(IStatefulPartState state)
        {
            bool outerOpen = OuterOpen(state as LeverState);
            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.name == AirlockDoors.Outer)
                    box.enabled = !outerOpen;
                else if (box.name == AirlockDoors.Inner)
                    box.enabled = outerOpen;
            }
        }
    }
}
