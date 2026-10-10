using System.Collections.Generic;
using System.Linq;
using Planets.Objects.Common.Model;
using StellarDriveDemoTFAdvanced.Common;
using StellarDriveDemoTFAdvanced.Parts;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.Worlds
{
    /// <summary>
    /// A wrecked cargo ship, much bigger than the game's crashed pieces: a copy of the game's crash
    /// site prefab (so it can be recycled like it) with a broken hull in three sections, a torn
    /// wing and its engines, in the game's ship part look.
    /// </summary>
    internal static class Wrecks
    {
        private static GameObject _cargo;
        private static GameObject _holder;

        public static GameObject Cargo(PlanetShaper.Context planet)
        {
            if (_cargo != null)
                return _cargo;
            GameObject crash = planet.HotspotPrefab("CrashedShipNoSmoke");
            if (crash == null)
                return null;
            if (_holder == null)
            {
                _holder = new GameObject("TFA_WreckPrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Object.Instantiate(crash, _holder.transform);
            copy.name = "TFA_CargoWreck";

            var builder = new MeshBuilder { Panel = GameSkin.PlainPanel };
            Hull(builder);
            Mesh mesh = builder.Build("TFA_CargoWreck");

            // Visual: the crash site's own model replaced by the cargo
            PlanetObjectVisuals visuals = copy.GetComponentInChildren<PlanetObjectVisuals>(true);
            visuals.id = copy.name;
            foreach (Transform child in visuals.transform.Cast<Transform>().ToList())
                Object.DestroyImmediate(child.gameObject);
            GameSkin.AddMesh(visuals.transform, visuals.gameObject.layer, "TFA_CargoHull", builder,
                LitMaterials.Get("TFA_CargoHull", new Color(0.5f, 0.52f, 0.55f), 0.6f, 0.4f));

            // Collision follows the hull
            Transform physics = copy.transform.Find("Physics");
            if (physics != null)
            {
                physics.localScale = Vector3.one;
                foreach (BoxCollider box in physics.GetComponents<BoxCollider>())
                    Object.DestroyImmediate(box);
                physics.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            _cargo = copy;
            return copy;
        }

        // About 30 m long, nose down in the ground, broken behind the cockpit and at mid-ship
        private static void Hull(MeshBuilder m)
        {
            Quaternion tilt = Quaternion.Euler(-9f, 0f, 6f);
            // Cockpit section, half buried
            m.Box(tilt * new Vector3(0f, 1.0f, 12f), new Vector3(5f, 3.6f, 6f), tilt);
            m.Box(tilt * new Vector3(0f, 2.4f, 15.6f), new Vector3(3.6f, 1.6f, 2.4f), tilt * Quaternion.Euler(20f, 0f, 0f));
            // Cargo section, twisted off its axis
            Quaternion twist = tilt * Quaternion.Euler(0f, 7f, -4f);
            m.Box(new Vector3(0.3f, 2.0f, 3f), new Vector3(6f, 4.4f, 10f), twist);
            m.Box(new Vector3(0.3f, 4.5f, 3f), new Vector3(4.6f, 0.6f, 9f), twist);
            // Engine section, broken away
            Quaternion broken = Quaternion.Euler(4f, -14f, 11f);
            m.Box(new Vector3(-1.2f, 2.2f, -7.5f), new Vector3(5.4f, 4f, 7f), broken);
            foreach (float x in new[] { -2.6f, 0.2f })
                m.Cylinder(new Vector3(x - 1.2f, 2.4f, -11f), new Vector3(x - 1.4f, 2.6f, -13.6f), 1.2f, 1.5f, 10);
            // A wing torn up into the air, the other one lying on the ground
            m.Box(new Vector3(6.5f, 4.5f, 2f), new Vector3(9f, 0.5f, 4.5f), Quaternion.Euler(0f, 8f, 28f));
            m.Box(new Vector3(-7.5f, 0.3f, 1f), new Vector3(8f, 0.5f, 4f), Quaternion.Euler(0f, -12f, -4f));
            // Scattered hull plates
            m.Box(new Vector3(9f, 0.2f, -6f), new Vector3(2.4f, 0.2f, 1.8f), Quaternion.Euler(10f, 30f, 6f));
            m.Box(new Vector3(-5f, 0.3f, 12f), new Vector3(1.8f, 0.25f, 2.6f), Quaternion.Euler(-8f, 60f, 12f));
        }
    }
}
