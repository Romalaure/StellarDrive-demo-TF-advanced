using System.Collections.Generic;
using StellarDriveDemoTFAdvanced.TFItems.Common;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Paint
{
    /// <summary>The paint gun's shape (the TF mod's model), hung on the tool rack as decoration.</summary>
    internal static class PaintGunModel
    {
        internal enum Part { Body, Dark, Accent, Chrome, Paint, Glow }

        internal static Dictionary<Part, MeshBuilder> BuildShapes()
        {
            var b = new Dictionary<Part, MeshBuilder>();
            foreach (Part part in System.Enum.GetValues(typeof(Part)))
                b[part] = new MeshBuilder();

            const float bodyY = 0.118f;
            const float cupZ = 0.035f;

            // Body: round barrel with a flat underside that meets the handle
            b[Part.Body].Cylinder(new Vector3(0f, bodyY, -0.07f), new Vector3(0f, bodyY, 0.085f), 0.03f, 0.03f, 20);
            b[Part.Body].Box(new Vector3(0f, 0.096f, 0.005f), new Vector3(0.044f, 0.03f, 0.13f));
            // Accent stripe along the barrel
            b[Part.Accent].Cylinder(new Vector3(0f, bodyY, 0.045f), new Vector3(0f, bodyY, 0.06f), 0.0315f, 0.0315f, 20);

            // Front: chrome collar, orange air cap with its two horns, fluid tip and glowing nozzle
            b[Part.Chrome].Cylinder(new Vector3(0f, bodyY, 0.085f), new Vector3(0f, bodyY, 0.1f), 0.031f, 0.027f, 20);
            b[Part.Accent].Cylinder(new Vector3(0f, bodyY, 0.1f), new Vector3(0f, bodyY, 0.124f), 0.027f, 0.019f, 20);
            b[Part.Accent].Box(new Vector3(0.022f, bodyY, 0.128f), new Vector3(0.009f, 0.02f, 0.024f));
            b[Part.Accent].Box(new Vector3(-0.022f, bodyY, 0.128f), new Vector3(0.009f, 0.02f, 0.024f));
            b[Part.Chrome].Cylinder(new Vector3(0f, bodyY, 0.124f), new Vector3(0f, bodyY, 0.138f), 0.009f, 0.006f, 12);
            b[Part.Glow].Cylinder(new Vector3(0f, bodyY, 0.138f), new Vector3(0f, bodyY, 0.143f), 0.0045f, 0.0035f, 12);

            // Rear: fan width knob (top) and fluid flow knob (bottom)
            b[Part.Dark].Cylinder(new Vector3(0f, 0.13f, -0.07f), new Vector3(0f, 0.13f, -0.09f), 0.011f, 0.011f, 10);
            b[Part.Chrome].Cylinder(new Vector3(0f, 0.13f, -0.09f), new Vector3(0f, 0.13f, -0.094f), 0.008f, 0.008f, 10);
            b[Part.Dark].Cylinder(new Vector3(0f, 0.1f, -0.07f), new Vector3(0f, 0.1f, -0.086f), 0.008f, 0.008f, 10);

            // Side pressure gauge, its face shows the paint color
            b[Part.Chrome].Cylinder(new Vector3(-0.026f, bodyY, 0.02f), new Vector3(-0.038f, bodyY, 0.02f), 0.014f, 0.014f, 16);
            b[Part.Glow].Cylinder(new Vector3(-0.038f, bodyY, 0.02f), new Vector3(-0.0395f, bodyY, 0.02f), 0.011f, 0.011f, 16);

            // Gravity cup on top: chrome neck, collar, paint-colored cup, dark lid and vent cap
            b[Part.Chrome].Cylinder(new Vector3(0f, 0.142f, cupZ), new Vector3(0f, 0.158f, cupZ), 0.009f, 0.009f, 12);
            b[Part.Body].Cylinder(new Vector3(0f, 0.158f, cupZ), new Vector3(0f, 0.166f, cupZ), 0.02f, 0.029f, 20);
            b[Part.Paint].Cylinder(new Vector3(0f, 0.166f, cupZ), new Vector3(0f, 0.212f, cupZ), 0.029f, 0.033f, 20);
            b[Part.Dark].Cylinder(new Vector3(0f, 0.212f, cupZ), new Vector3(0f, 0.22f, cupZ), 0.035f, 0.035f, 20);
            b[Part.Accent].Cylinder(new Vector3(0f, 0.22f, cupZ), new Vector3(0f, 0.228f, cupZ), 0.01f, 0.008f, 12);

            // Handle, tilted back, with a wider butt
            b[Part.Dark].Block(new[]
            {
                new Vector3(-0.021f, -0.085f, -0.072f), new Vector3(0.021f, -0.085f, -0.072f),
                new Vector3(-0.019f, 0.085f, -0.035f), new Vector3(0.019f, 0.085f, -0.035f),
                new Vector3(-0.021f, -0.085f, -0.022f), new Vector3(0.021f, -0.085f, -0.022f),
                new Vector3(-0.019f, 0.085f, 0.022f), new Vector3(0.019f, 0.085f, 0.022f)
            });
            b[Part.Body].Block(new[]
            {
                new Vector3(-0.024f, -0.097f, -0.078f), new Vector3(0.024f, -0.097f, -0.078f),
                new Vector3(-0.024f, -0.085f, -0.076f), new Vector3(0.024f, -0.085f, -0.076f),
                new Vector3(-0.024f, -0.097f, -0.018f), new Vector3(0.024f, -0.097f, -0.018f),
                new Vector3(-0.024f, -0.085f, -0.018f), new Vector3(0.024f, -0.085f, -0.018f)
            });

            // Air inlet under the handle with an orange hose fitting
            b[Part.Chrome].Cylinder(new Vector3(0f, -0.097f, -0.048f), new Vector3(0f, -0.122f, -0.048f), 0.0075f, 0.0075f, 12);
            b[Part.Accent].Cylinder(new Vector3(0f, -0.104f, -0.048f), new Vector3(0f, -0.112f, -0.048f), 0.0105f, 0.0105f, 12);

            // Trigger: two chrome segments curving down in front of the handle
            b[Part.Chrome].Block(new[]
            {
                new Vector3(-0.011f, 0.045f, 0.03f), new Vector3(0.011f, 0.045f, 0.03f),
                new Vector3(-0.011f, 0.083f, 0.028f), new Vector3(0.011f, 0.083f, 0.028f),
                new Vector3(-0.011f, 0.045f, 0.04f), new Vector3(0.011f, 0.045f, 0.04f),
                new Vector3(-0.011f, 0.083f, 0.038f), new Vector3(0.011f, 0.083f, 0.038f)
            });
            b[Part.Chrome].Block(new[]
            {
                new Vector3(-0.011f, 0.002f, 0.018f), new Vector3(0.011f, 0.002f, 0.018f),
                new Vector3(-0.011f, 0.045f, 0.03f), new Vector3(0.011f, 0.045f, 0.03f),
                new Vector3(-0.011f, 0.002f, 0.027f), new Vector3(0.011f, 0.002f, 0.027f),
                new Vector3(-0.011f, 0.045f, 0.04f), new Vector3(0.011f, 0.045f, 0.04f)
            });

            return b;
        }
    }
}
