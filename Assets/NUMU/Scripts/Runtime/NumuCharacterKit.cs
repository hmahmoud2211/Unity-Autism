using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>
    /// Chibi mascots assembled from smooth sculpted volumes (about 3 units tall, facing -Z).
    /// The body uses the soft character material; eyes, noses and highlights use a glossy one.
    /// </summary>
    public static class CharacterKit
    {
        static readonly Color EyeDark = NumuColor.Hex("#2a2230");
        static readonly Color White = NumuColor.Hex("#ffffff");
        static readonly Color Blush = NumuColor.Hex("#ff9db4");
        static readonly Color MouthDark = NumuColor.Hex("#7a2a35");
        static readonly Color Tongue = NumuColor.Hex("#ff7f9a");

        static Matrix4x4 TRS(Vector3 p, Quaternion r) => Matrix4x4.TRS(p, r, Vector3.one);
        static void E(MeshBuilder mb, Vector3 p, Vector3 r, Color c, Quaternion? rot = null, int level = 3) =>
            Shapes.Ellipsoid(mb, TRS(p, rot ?? Quaternion.identity), r, c, level);

        static void Eye(MeshBuilder eyes, Vector3 p, float size, float yaw)
        {
            Quaternion r = Quaternion.Euler(0, yaw, 0);
            E(eyes, p, new Vector3(size * 0.72f, size, size * 0.45f), EyeDark, r);
            E(eyes, p + r * new Vector3(size * 0.22f, size * 0.38f, -size * 0.36f), Vector3.one * size * 0.3f, White, r, 2);
            E(eyes, p + r * new Vector3(-size * 0.2f, -size * 0.35f, -size * 0.38f), Vector3.one * size * 0.14f, White, r, 1);
        }

        static void Face(MeshBuilder body, MeshBuilder eyes, float headY, float faceZ, float eyeSpread, float eyeSize, float mouthY, bool blush = true)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Eye(eyes, new Vector3(s * eyeSpread, headY + 0.08f, faceZ), eyeSize, s * -18f);
                if (blush) E(body, new Vector3(s * (eyeSpread + 0.16f), headY - 0.16f, faceZ + 0.06f), new Vector3(0.12f, 0.07f, 0.04f), Blush, Quaternion.Euler(0, s * -30f, 0), 2);
            }
            E(eyes, new Vector3(0, mouthY, faceZ - 0.02f), new Vector3(0.1f, 0.075f, 0.05f), MouthDark, null, 2);
            E(body, new Vector3(0, mouthY - 0.035f, faceZ - 0.05f), new Vector3(0.06f, 0.035f, 0.03f), Tongue, null, 2);
        }

        static void Limb(MeshBuilder mb, Vector3 from, Vector3 to, float r, Color c, Color? tip = null)
        {
            Shapes.Tube(mb, new List<Vector3> { from, Vector3.Lerp(from, to, 0.5f), to }, r, 10, c, true, t => Mathf.Lerp(1f, 0.85f, t));
            E(mb, to, Vector3.one * r * 1.15f, tip ?? c, null, 2);
        }

        // ------------------------------------------------------------------ fox

        public static void Fox(MeshBuilder body, MeshBuilder eyes)
        {
            Color orange = NumuColor.Hex("#f2892a");
            Color orangeLight = NumuColor.Hex("#f8a548");
            Color cream = NumuColor.Hex("#fff3e2");
            Color brown = NumuColor.Hex("#5b3a26");
            Color scarf = NumuColor.Hex("#3fae5a");

            E(body, new Vector3(0, 0.95f, 0), new Vector3(0.52f, 0.6f, 0.48f), orange);
            E(body, new Vector3(0, 0.88f, -0.3f), new Vector3(0.36f, 0.44f, 0.24f), cream);
            foreach (float s in new[] { -1f, 1f })
            {
                Limb(body, new Vector3(s * 0.24f, 0.55f, 0f), new Vector3(s * 0.26f, 0.14f, -0.08f), 0.17f, orange, brown);
                Limb(body, new Vector3(s * 0.42f, 1.2f, -0.05f), new Vector3(s * 0.62f, 0.86f, -0.22f), 0.12f, orange, brown);
            }
            // Fluffy tail curling up behind.
            var tail = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                tail.Add(new Vector3(0.3f + t * 0.75f, 0.55f + Mathf.Sin(t * 2.2f) * 0.85f, 0.35f + t * 0.3f));
            }
            Shapes.Tube(body, tail, 0.32f, 12, orange, true, t => 0.45f + Mathf.Sin(t * Mathf.PI) * 0.75f);
            E(body, tail[8] + new Vector3(0.05f, 0.1f, 0.02f), new Vector3(0.24f, 0.3f, 0.24f), cream);

            float hy = 2.0f;
            E(body, new Vector3(0, hy, 0), new Vector3(0.84f, 0.7f, 0.74f), orange);
            E(body, new Vector3(0, hy + 0.32f, -0.2f), new Vector3(0.55f, 0.32f, 0.5f), orangeLight);
            foreach (float s in new[] { -1f, 1f })
            {
                E(body, new Vector3(s * 0.4f, hy - 0.2f, -0.4f), new Vector3(0.42f, 0.32f, 0.34f), cream);
                Shapes.Cone(body, TRS(new Vector3(s * 0.72f, hy - 0.25f, -0.15f), Quaternion.Euler(0, 0, s * -105f)), 0.2f, 0.45f, 10, cream);
                // Big triangular ears with cream insides.
                Quaternion er = Quaternion.Euler(-8f, 0, s * -20f);
                Shapes.Lathe(body, TRS(new Vector3(s * 0.46f, hy + 0.42f, 0.02f), er), new[] { new Vector2(0, 0), new Vector2(0.34f, 0), new Vector2(0.24f, 0.45f), new Vector2(0.06f, 0.85f), new Vector2(0, 0.9f) }, 12, orange, true);
                Shapes.Lathe(body, TRS(new Vector3(s * 0.46f, hy + 0.47f, -0.1f), er), new[] { new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(0.13f, 0.4f), new Vector2(0, 0.66f) }, 10, cream, true);
            }
            E(body, new Vector3(0, hy - 0.2f, -0.66f), new Vector3(0.33f, 0.24f, 0.28f), cream);
            E(eyes, new Vector3(0, hy - 0.1f, -0.95f), new Vector3(0.12f, 0.09f, 0.08f), NumuColor.Hex("#2f2224"));
            Face(body, eyes, hy, -0.66f, 0.3f, 0.2f, hy - 0.33f);
            // Green scarf.
            Shapes.Torus(body, TRS(new Vector3(0, 1.42f, -0.02f), Quaternion.Euler(-6, 0, 0)), 0.42f, 0.12f, 24, 10, scarf);
            E(body, new Vector3(0.24f, 1.18f, -0.42f), new Vector3(0.13f, 0.26f, 0.06f), scarf, Quaternion.Euler(0, 0, 15));
        }

        // ------------------------------------------------------------------ penguin

        public static void Penguin(MeshBuilder body, MeshBuilder eyes)
        {
            Color blue = NumuColor.Hex("#2f66bd");
            Color blueLight = NumuColor.Hex("#4b8be0");
            Color navy = NumuColor.Hex("#23477f");
            Color orange = NumuColor.Hex("#ff9a2a");
            Color pack = NumuColor.Hex("#f2ab3b");

            E(body, new Vector3(0, 0.95f, 0), new Vector3(0.72f, 0.82f, 0.66f), blue);
            E(body, new Vector3(0, 0.88f, -0.36f), new Vector3(0.52f, 0.62f, 0.34f), White);
            foreach (float s in new[] { -1f, 1f })
            {
                E(body, new Vector3(s * 0.7f, 1.0f, -0.05f), new Vector3(0.13f, 0.44f, 0.26f), blueLight, Quaternion.Euler(0, 0, s * 28f));
                E(body, new Vector3(s * 0.26f, 0.08f, -0.25f), new Vector3(0.22f, 0.09f, 0.32f), orange);
            }
            // Backpack with straps.
            Shapes.RoundedBox(body, TRS(new Vector3(0, 1.08f, 0.62f), Quaternion.Euler(-8, 0, 0)), new Vector3(0.95f, 1.0f, 0.42f), 0.18f, 2, pack);
            Shapes.RoundedBox(body, TRS(new Vector3(0, 0.88f, 0.86f), Quaternion.Euler(-8, 0, 0)), new Vector3(0.7f, 0.45f, 0.18f), 0.1f, 2, NumuColor.Shade(pack, 0.88f));
            foreach (float s in new[] { -1f, 1f })
                Shapes.Torus(body, TRS(new Vector3(s * 0.36f, 1.25f, 0.05f), Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, 90, 0)), 0.62f, 0.05f, 20, 6, NumuColor.Hex("#c47a22"), 140f);

            float hy = 2.0f;
            E(body, new Vector3(0, hy, 0), new Vector3(0.8f, 0.72f, 0.74f), blue);
            foreach (float s in new[] { -1f, 1f })
                E(body, new Vector3(s * 0.2f, hy - 0.05f, -0.45f), new Vector3(0.34f, 0.44f, 0.3f), White);
            E(eyes, new Vector3(0, hy - 0.2f, -0.82f), new Vector3(0.17f, 0.1f, 0.2f), orange, Quaternion.Euler(10, 0, 0));
            Face(body, eyes, hy + 0.02f, -0.7f, 0.25f, 0.19f, hy - 0.36f);
            // Knitted beanie with pompom.
            E(body, new Vector3(0, hy + 0.42f, 0.1f), new Vector3(0.7f, 0.38f, 0.66f), navy);
            Shapes.Torus(body, TRS(new Vector3(0, hy + 0.38f, 0.06f), Quaternion.Euler(-16, 0, 0)), 0.66f, 0.11f, 28, 10, blueLight);
            E(body, new Vector3(0, hy + 0.84f, 0.14f), Vector3.one * 0.17f, NumuColor.Hex("#8fc3ff"));
        }

        // ------------------------------------------------------------------ bunny

        public static void Bunny(MeshBuilder body, MeshBuilder eyes)
        {
            Color white = NumuColor.Hex("#fff5f7");
            Color pink = NumuColor.Hex("#f8a9c6");
            Color magenta = NumuColor.Hex("#e2468f");
            Color pad = NumuColor.Hex("#ff8cc0");
            Color teal = NumuColor.Hex("#46c0c9");

            E(body, new Vector3(0, 0.9f, 0), new Vector3(0.5f, 0.58f, 0.45f), white);
            E(body, new Vector3(0, 0.82f, -0.3f), new Vector3(0.32f, 0.38f, 0.2f), NumuColor.Hex("#ffe9ef"));
            E(body, new Vector3(0, 0.75f, 0.45f), Vector3.one * 0.2f, white);
            foreach (float s in new[] { -1f, 1f })
            {
                Limb(body, new Vector3(s * 0.22f, 0.5f, 0f), new Vector3(s * 0.25f, 0.12f, -0.1f), 0.16f, white, pink);
                Limb(body, new Vector3(s * 0.4f, 1.15f, -0.05f), new Vector3(s * 0.66f, 1.35f, -0.2f), 0.11f, white);
            }
            Shapes.Torus(body, TRS(new Vector3(0, 1.38f, -0.02f), Quaternion.Euler(-6, 0, 0)), 0.38f, 0.1f, 24, 10, teal);

            float hy = 1.95f;
            E(body, new Vector3(0, hy, 0), new Vector3(0.82f, 0.7f, 0.72f), white);
            foreach (float s in new[] { -1f, 1f })
            {
                Quaternion er = Quaternion.Euler(-6f, 0, s * -10f);
                E(body, new Vector3(s * 0.3f, hy + 1.0f, 0.05f), new Vector3(0.2f, 0.66f, 0.15f), white, er);
                E(body, new Vector3(s * 0.3f, hy + 1.02f, -0.05f), new Vector3(0.11f, 0.52f, 0.07f), pink, er);
                E(body, new Vector3(s * 0.11f, hy - 0.22f, -0.64f), new Vector3(0.15f, 0.11f, 0.1f), white, null, 2);
                // Headphone cups.
                Shapes.Cylinder(body, TRS(new Vector3(s * 0.72f, hy - 0.02f, 0f), Quaternion.Euler(0, 0, s * -90f)), 0.27f, 0.25f, 0.2f, 20, magenta);
                Shapes.Cylinder(body, TRS(new Vector3(s * 0.66f, hy - 0.02f, 0f), Quaternion.Euler(0, 0, s * -90f)), 0.2f, 0.2f, 0.08f, 16, pad);
            }
            Shapes.Torus(body, TRS(new Vector3(0, hy - 0.02f, 0f), Quaternion.Euler(90, 0, 0)), 0.86f, 0.07f, 28, 8, magenta, 180f);
            E(eyes, new Vector3(0, hy - 0.12f, -0.72f), new Vector3(0.08f, 0.06f, 0.05f), NumuColor.Hex("#ff6f9c"));
            Face(body, eyes, hy + 0.02f, -0.64f, 0.27f, 0.2f, hy - 0.36f);
        }

        // ------------------------------------------------------------------ bear

        public static void Bear(MeshBuilder body, MeshBuilder eyes)
        {
            Color brown = NumuColor.Hex("#c98a4a");
            Color light = NumuColor.Hex("#f2d5a8");
            Color dark = NumuColor.Hex("#3a2620");
            Color scarf = NumuColor.Hex("#f6c12f");
            Color pack = NumuColor.Hex("#8f5a2c");

            E(body, new Vector3(0, 0.98f, 0), new Vector3(0.62f, 0.7f, 0.58f), brown);
            E(body, new Vector3(0, 0.9f, -0.36f), new Vector3(0.42f, 0.5f, 0.26f), light);
            foreach (float s in new[] { -1f, 1f })
                Limb(body, new Vector3(s * 0.28f, 0.55f, 0f), new Vector3(s * 0.3f, 0.15f, -0.1f), 0.2f, brown, NumuColor.Shade(brown, 0.85f));
            Limb(body, new Vector3(-0.45f, 1.25f, -0.05f), new Vector3(-0.72f, 0.9f, -0.25f), 0.14f, brown);
            Limb(body, new Vector3(0.45f, 1.3f, -0.05f), new Vector3(0.88f, 1.85f, -0.2f), 0.14f, brown);
            Shapes.RoundedBox(body, TRS(new Vector3(0, 1.12f, 0.62f), Quaternion.Euler(-8, 0, 0)), new Vector3(0.95f, 1.05f, 0.42f), 0.18f, 2, pack);
            Shapes.RoundedBox(body, TRS(new Vector3(0, 0.9f, 0.86f), Quaternion.Euler(-8, 0, 0)), new Vector3(0.7f, 0.42f, 0.18f), 0.1f, 2, scarf);

            float hy = 2.05f;
            E(body, new Vector3(0, hy, 0), new Vector3(0.86f, 0.74f, 0.78f), brown);
            foreach (float s in new[] { -1f, 1f })
            {
                E(body, new Vector3(s * 0.6f, hy + 0.56f, 0.05f), new Vector3(0.28f, 0.28f, 0.2f), brown);
                E(body, new Vector3(s * 0.6f, hy + 0.55f, -0.1f), new Vector3(0.16f, 0.16f, 0.08f), light);
            }
            E(body, new Vector3(0, hy - 0.2f, -0.66f), new Vector3(0.36f, 0.27f, 0.28f), light);
            E(eyes, new Vector3(0, hy - 0.08f, -0.93f), new Vector3(0.13f, 0.09f, 0.08f), dark);
            Face(body, eyes, hy + 0.04f, -0.7f, 0.32f, 0.19f, hy - 0.33f);
            Shapes.Torus(body, TRS(new Vector3(0, 1.55f, -0.02f), Quaternion.Euler(-6, 0, 0)), 0.5f, 0.14f, 24, 10, scarf);
            E(body, new Vector3(-0.28f, 1.28f, -0.5f), new Vector3(0.15f, 0.3f, 0.07f), scarf, Quaternion.Euler(0, 0, -15));
        }
    }
}
