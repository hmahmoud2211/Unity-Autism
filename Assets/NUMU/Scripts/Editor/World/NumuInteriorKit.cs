using System.Collections.Generic;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>A hole in a wall, measured along the wall from its start point (metres).</summary>
    public struct WallOpening
    {
        public float start, width, bottom, top;

        public WallOpening(float start, float width, float bottom, float top)
        {
            this.start = start;
            this.width = width;
            this.bottom = bottom;
            this.top = top;
        }

        public float End => start + width;
    }

    /// <summary>
    /// Architectural geometry for the indoor worlds: walls with door/window openings, window frames and glass,
    /// doors, kitchen cabinets, worktops with cut-outs, curtains and shelving. UVs are in metres so tiling
    /// PBR materials keep a real-world scale on every surface.
    /// </summary>
    public static class NumuInteriorKit
    {
        // ------------------------------------------------------------------ primitives with metre UVs

        /// <summary>One quad; UVs are the world-space distances along <paramref name="u"/> and <paramref name="v"/> (plus offsets).</summary>
        public static void Quad(MeshBuilder mb, Vector3 origin, Vector3 u, Vector3 v, Vector3 normal, Vector2 uvOffset = default)
        {
            float lu = u.magnitude, lv = v.magnitude;
            int a = mb.Add(origin, normal, Color.white, uvOffset);
            int b = mb.Add(origin + u, normal, Color.white, uvOffset + new Vector2(lu, 0));
            int c = mb.Add(origin + u + v, normal, Color.white, uvOffset + new Vector2(lu, lv));
            int d = mb.Add(origin + v, normal, Color.white, uvOffset + new Vector2(0, lv));
            mb.QuadFacing(a, b, c, d, normal);
        }

        /// <summary>Axis-aligned box (in the given frame) with metre UVs on all six faces.</summary>
        public static void Box(MeshBuilder mb, Matrix4x4 m, Vector3 center, Vector3 size, bool bottom = true)
        {
            var part = new MeshBuilder();
            Vector3 h = size * 0.5f;
            Vector3 c = center;
            Quad(part, c + new Vector3(-h.x, -h.y, h.z), Vector3.right * size.x, Vector3.up * size.y, Vector3.forward);
            Quad(part, c + new Vector3(h.x, -h.y, -h.z), Vector3.left * size.x, Vector3.up * size.y, Vector3.back);
            Quad(part, c + new Vector3(h.x, -h.y, h.z), Vector3.back * size.z, Vector3.up * size.y, Vector3.right);
            Quad(part, c + new Vector3(-h.x, -h.y, -h.z), Vector3.forward * size.z, Vector3.up * size.y, Vector3.left);
            Quad(part, c + new Vector3(-h.x, h.y, -h.z), Vector3.right * size.x, Vector3.forward * size.z, Vector3.up);
            if (bottom) Quad(part, c + new Vector3(-h.x, -h.y, h.z), Vector3.right * size.x, Vector3.back * size.z, Vector3.down);
            mb.Append(part, m);
        }

        public static void Box(MeshBuilder mb, Vector3 center, Vector3 size, bool bottom = true) => Box(mb, Matrix4x4.identity, center, size, bottom);

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// A straight wall from <paramref name="a"/> to <paramref name="b"/> (floor level), centred on that line.
        /// The side facing <paramref name="insidePoint"/> goes into <paramref name="inner"/>, the other into <paramref name="outer"/>;
        /// reveals (opening jambs, sills, lintels) and the wall top go into <paramref name="inner"/>.
        /// </summary>
        public static void Wall(MeshBuilder inner, MeshBuilder outer, Vector3 a, Vector3 b, Vector3 insidePoint, float height, float thickness, IList<WallOpening> openings = null)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            dir /= len;
            Vector3 n = Vector3.Cross(Vector3.up, dir).normalized;
            if (Vector3.Dot(insidePoint - a, n) < 0f) n = -n;
            float ht = thickness * 0.5f;
            var ops = new List<WallOpening>(openings ?? new WallOpening[0]);
            ops.Sort((x, y) => x.start.CompareTo(y.start));

            // Split the wall face into solid rectangles: full-height columns between openings, plus the
            // pieces below and above each opening.
            var rects = new List<Rect>();
            float x = 0f;
            foreach (var o in ops)
            {
                if (o.start > x) rects.Add(new Rect(x, 0, o.start - x, height));
                if (o.bottom > 0f) rects.Add(new Rect(o.start, 0, o.width, o.bottom));
                if (o.top < height) rects.Add(new Rect(o.start, o.top, o.width, height - o.top));
                x = o.End;
            }
            if (x < len) rects.Add(new Rect(x, 0, len - x, height));

            Vector3 P(float along, float y, float side) => a + dir * along + Vector3.up * y + n * side;
            foreach (var r in rects)
            {
                // Inside face (facing n) and outside face (facing -n); UVs continue along the wall.
                Quad(inner, P(r.xMax, r.yMin, ht), -dir * r.width, Vector3.up * r.height, n, new Vector2(len - r.xMax, r.yMin));
                Quad(outer, P(r.xMin, r.yMin, -ht), dir * r.width, Vector3.up * r.height, -n, new Vector2(r.xMin, r.yMin));
            }
            foreach (var o in ops)
            {
                // Reveals.
                Quad(inner, P(o.start, o.bottom, -ht), n * thickness, Vector3.up * (o.top - o.bottom), dir);
                Quad(inner, P(o.End, o.bottom, ht), -n * thickness, Vector3.up * (o.top - o.bottom), -dir);
                Quad(inner, P(o.start, o.top, ht), dir * o.width, -n * thickness, Vector3.down);
                if (o.bottom > 0f) Quad(inner, P(o.start, o.bottom, -ht), dir * o.width, n * thickness, Vector3.up);
            }
            // Wall top and ends.
            Quad(inner, P(0, height, -ht), dir * len, n * thickness, Vector3.up);
            Quad(inner, P(0, 0, ht), -n * thickness, Vector3.up * height, -dir);
            Quad(inner, P(len, 0, -ht), n * thickness, Vector3.up * height, dir);
        }

        /// <summary>Skirting board along the inside of a wall, interrupted by door openings.</summary>
        public static void Skirting(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 insidePoint, float wallThickness, IList<WallOpening> openings = null, float h = 0.09f, float depth = 0.016f)
        {
            Vector3 dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            Vector3 n = Vector3.Cross(Vector3.up, dir).normalized;
            if (Vector3.Dot(insidePoint - a, n) < 0f) n = -n;
            var segs = new List<Vector2> { new Vector2(0, len) };
            if (openings != null)
                foreach (var o in openings)
                {
                    if (o.bottom > 0.01f) continue;
                    var next = new List<Vector2>();
                    foreach (var s in segs)
                    {
                        if (o.End <= s.x || o.start >= s.y) { next.Add(s); continue; }
                        if (o.start > s.x) next.Add(new Vector2(s.x, o.start));
                        if (o.End < s.y) next.Add(new Vector2(o.End, s.y));
                    }
                    segs = next;
                }
            var rot = Quaternion.LookRotation(n, Vector3.up);
            foreach (var s in segs)
            {
                float l = s.y - s.x;
                if (l < 0.02f) continue;
                Vector3 c = a + dir * ((s.x + s.y) * 0.5f) + n * (wallThickness * 0.5f + depth * 0.5f) + Vector3.up * (h * 0.5f);
                Box(mb, Matrix4x4.TRS(c, rot, Vector3.one), Vector3.zero, new Vector3(l, h, depth), false);
            }
        }

        /// <summary>Floor or ceiling slab over a rectangle (x/z), top surface at y. UVs are metres.</summary>
        public static void Slab(MeshBuilder mb, Rect r, float y, bool faceUp)
        {
            if (faceUp) Quad(mb, new Vector3(r.xMin, y, r.yMin), Vector3.forward * r.height, Vector3.right * r.width, Vector3.up, new Vector2(r.yMin, r.xMin));
            else Quad(mb, new Vector3(r.xMin, y, r.yMin), Vector3.right * r.width, Vector3.forward * r.height, Vector3.down, new Vector2(r.xMin, r.yMin));
        }

        /// <summary>Worktop over a rectangle with rectangular cut-outs (sinks, hobs).</summary>
        public static void Worktop(MeshBuilder mb, Rect r, float y, float thickness, IList<Rect> holes = null)
        {
            var pieces = new List<Rect> { r };
            if (holes != null)
                foreach (var h in holes)
                {
                    var next = new List<Rect>();
                    foreach (var p in pieces)
                    {
                        if (!p.Overlaps(h)) { next.Add(p); continue; }
                        if (h.xMin > p.xMin) next.Add(Rect.MinMaxRect(p.xMin, p.yMin, h.xMin, p.yMax));
                        if (h.xMax < p.xMax) next.Add(Rect.MinMaxRect(h.xMax, p.yMin, p.xMax, p.yMax));
                        float x0 = Mathf.Max(p.xMin, h.xMin), x1 = Mathf.Min(p.xMax, h.xMax);
                        if (h.yMin > p.yMin) next.Add(Rect.MinMaxRect(x0, p.yMin, x1, h.yMin));
                        if (h.yMax < p.yMax) next.Add(Rect.MinMaxRect(x0, h.yMax, x1, p.yMax));
                    }
                    pieces = next;
                }
            foreach (var p in pieces)
                Box(mb, new Vector3(p.center.x, y - thickness * 0.5f, p.center.y), new Vector3(p.width, thickness, p.height));
        }

        // ------------------------------------------------------------------ windows and doors

        /// <summary>
        /// Window filling an opening: frame, a centre mullion and transom, sill board on the inside and glass.
        /// The frame sits in the wall's middle plane; <paramref name="a"/>→<paramref name="b"/> is the wall line.
        /// </summary>
        public static void Window(MeshBuilder frame, MeshBuilder glass, MeshBuilder sill, Vector3 a, Vector3 b, Vector3 insidePoint, WallOpening o, float wallThickness, bool mullion = true)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 n = Vector3.Cross(Vector3.up, dir).normalized;
            if (Vector3.Dot(insidePoint - a, n) < 0f) n = -n;
            var rot = Quaternion.LookRotation(n, Vector3.up);
            Vector3 c = a + dir * (o.start + o.width * 0.5f) + Vector3.up * ((o.bottom + o.top) * 0.5f);
            var m = Matrix4x4.TRS(c, rot, Vector3.one);
            float w = o.width, h = o.top - o.bottom, f = 0.06f, d = 0.09f;
            // In the rotated frame +x runs along -dir (LookRotation), which doesn't matter for a symmetric frame.
            Box(frame, m, new Vector3(0, h * 0.5f - f * 0.5f, 0), new Vector3(w, f, d));
            Box(frame, m, new Vector3(0, -h * 0.5f + f * 0.5f, 0), new Vector3(w, f, d));
            Box(frame, m, new Vector3(w * 0.5f - f * 0.5f, 0, 0), new Vector3(f, h, d));
            Box(frame, m, new Vector3(-w * 0.5f + f * 0.5f, 0, 0), new Vector3(f, h, d));
            if (mullion && w > 1.1f) Box(frame, m, Vector3.zero, new Vector3(f * 0.8f, h - f, d * 0.8f));
            if (mullion && h > 1.4f) Box(frame, m, new Vector3(0, h * 0.5f - h * 0.28f, 0), new Vector3(w - f, f * 0.8f, d * 0.8f));
            var g = new MeshBuilder();
            Quad(g, new Vector3(-w * 0.5f + f, -h * 0.5f + f, 0), Vector3.right * (w - 2 * f), Vector3.up * (h - 2 * f), Vector3.forward);
            Quad(g, new Vector3(w * 0.5f - f, -h * 0.5f + f, 0), Vector3.left * (w - 2 * f), Vector3.up * (h - 2 * f), Vector3.back);
            glass.Append(g, m);
            if (o.bottom > 0.05f && sill != null)
                Box(sill, m, new Vector3(0, -h * 0.5f - 0.015f, wallThickness * 0.5f + 0.02f), new Vector3(w + 0.1f, 0.03f, wallThickness * 0.5f + 0.1f));
        }

        /// <summary>Door casing around an opening plus a door leaf (closed, or swung open by <paramref name="openDeg"/>) and a lever handle.</summary>
        public static void Door(MeshBuilder casing, MeshBuilder leaf, MeshBuilder handle, Vector3 a, Vector3 b, Vector3 insidePoint, WallOpening o, float wallThickness, float openDeg = 0f)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 n = Vector3.Cross(Vector3.up, dir).normalized;
            if (Vector3.Dot(insidePoint - a, n) < 0f) n = -n;
            var rot = Quaternion.LookRotation(n, Vector3.up);
            Vector3 baseC = a + dir * (o.start + o.width * 0.5f);
            var m = Matrix4x4.TRS(baseC, rot, Vector3.one);
            float w = o.width, h = o.top, cw = 0.07f, cd = 0.015f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 off = new Vector3(0, 0, side * (wallThickness * 0.5f + cd * 0.5f));
                Box(casing, m, off + new Vector3(w * 0.5f + cw * 0.5f, h * 0.5f, 0), new Vector3(cw, h + cw, cd));
                Box(casing, m, off + new Vector3(-w * 0.5f - cw * 0.5f, h * 0.5f, 0), new Vector3(cw, h + cw, cd));
                Box(casing, m, off + new Vector3(0, h + cw * 0.5f, 0), new Vector3(w + cw * 2f, cw, cd));
            }
            // Leaf hinged on the local -x jamb, swinging into the room.
            float lw = w - 0.01f, lh = h - 0.01f, lt = 0.04f;
            var hinge = Matrix4x4.TRS(new Vector3(-w * 0.5f, 0, wallThickness * 0.5f - lt * 0.5f), Quaternion.Euler(0, -openDeg, 0), Vector3.one);
            var lm = m * hinge;
            Box(leaf, lm, new Vector3(lw * 0.5f, lh * 0.5f, 0), new Vector3(lw, lh, lt));
            // Raised panels for a realistic leaf.
            foreach (float s in new[] { -1f, 1f })
                for (int k = 0; k < 2; k++)
                    Box(leaf, lm, new Vector3(lw * 0.5f, lh * (k == 0 ? 0.3f : 0.72f), s * (lt * 0.5f + 0.004f)), new Vector3(lw * 0.62f, lh * (k == 0 ? 0.36f : 0.36f), 0.008f), false);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(handle, lm, new Vector3(lw - 0.07f, 1.0f, s * (lt * 0.5f + 0.03f)), new Vector3(0.022f, 0.022f, 0.06f));
                Box(handle, lm, new Vector3(lw - 0.12f, 1.0f, s * (lt * 0.5f + 0.06f)), new Vector3(0.12f, 0.02f, 0.02f));
            }
        }

        // ------------------------------------------------------------------ furniture

        /// <summary>Kitchen base or wall cabinet run: carcass, shaker doors with a small gap, bar handles.</summary>
        public static void CabinetRun(MeshBuilder body, MeshBuilder doors, MeshBuilder handles, Matrix4x4 m, float length, float height, float depth, float doorWidth, bool baseUnit, float plinth = 0.1f)
        {
            float y0 = baseUnit ? plinth : 0f;
            Box(body, m, new Vector3(length * 0.5f, y0 + (height - y0) * 0.5f, depth * 0.5f), new Vector3(length, height - y0, depth));
            if (baseUnit) Box(body, m, new Vector3(length * 0.5f, plinth * 0.5f, depth * 0.5f + 0.04f), new Vector3(length, plinth, depth - 0.08f), false);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / doorWidth));
            float dw = length / count, gap = 0.004f, t = 0.02f;
            for (int i = 0; i < count; i++)
            {
                float cx = dw * (i + 0.5f);
                float dh = height - y0 - gap * 2f;
                float cy = y0 + gap + dh * 0.5f;
                Box(doors, m, new Vector3(cx, cy, depth + t * 0.5f), new Vector3(dw - gap * 2f, dh, t));
                // Shaker profile: a recessed centre panel framed by rails/stiles.
                Box(doors, m, new Vector3(cx, cy, depth + t + 0.003f), new Vector3(dw - 0.14f, dh - 0.14f, 0.004f), false);
                float hx = cx + (i % 2 == 0 ? 1f : -1f) * (dw * 0.5f - 0.05f);
                float hy = baseUnit ? height - 0.1f : y0 + 0.1f;
                Box(handles, m, new Vector3(hx, hy - (baseUnit ? 0.06f : -0.06f), depth + t + 0.025f), new Vector3(0.012f, 0.14f, 0.012f));
                Box(handles, m, new Vector3(hx, hy - (baseUnit ? 0.06f : -0.06f) + 0.06f, depth + t + 0.012f), new Vector3(0.01f, 0.01f, 0.024f));
                Box(handles, m, new Vector3(hx, hy - (baseUnit ? 0.06f : -0.06f) - 0.06f, depth + t + 0.012f), new Vector3(0.01f, 0.01f, 0.024f));
            }
        }

        /// <summary>Fridge-freezer: rounded body, two doors, long bar handles. Origin at the floor, front facing +z.</summary>
        public static void Fridge(MeshBuilder body, MeshBuilder handles, Matrix4x4 m, float width, float height, float depth)
        {
            Shapes.RoundedBox(body, m * Matrix4x4.Translate(new Vector3(0, height * 0.5f, depth * 0.5f - 0.03f)), new Vector3(width, height, depth - 0.06f), 0.02f, 2, Color.white);
            float split = height * 0.62f;
            Shapes.RoundedBox(body, m * Matrix4x4.Translate(new Vector3(0, split * 0.5f + 0.02f, depth - 0.02f)), new Vector3(width - 0.01f, split - 0.03f, 0.05f), 0.015f, 2, Color.white);
            Shapes.RoundedBox(body, m * Matrix4x4.Translate(new Vector3(0, split + (height - split) * 0.5f + 0.005f, depth - 0.02f)), new Vector3(width - 0.01f, height - split - 0.03f, 0.05f), 0.015f, 2, Color.white);
            float hx = width * 0.5f - 0.06f;
            Box(handles, m, new Vector3(hx, split * 0.6f, depth + 0.035f), new Vector3(0.025f, split * 0.5f, 0.025f));
            Box(handles, m, new Vector3(hx, split + 0.22f, depth + 0.035f), new Vector3(0.025f, 0.3f, 0.025f));
            foreach (float y in new[] { split * 0.6f - split * 0.24f, split * 0.6f + split * 0.24f, split + 0.09f, split + 0.35f })
                Box(handles, m, new Vector3(hx, y, depth + 0.012f), new Vector3(0.02f, 0.02f, 0.025f), false);
        }

        /// <summary>Gathered curtain: a pleated cloth surface hanging from <paramref name="top"/> (centre), facing +z of the frame.</summary>
        public static void Curtain(MeshBuilder mb, Matrix4x4 m, float width, float height, float pleatDepth, float pleatsPerMetre)
        {
            var part = new MeshBuilder();
            int nx = Mathf.Max(8, Mathf.RoundToInt(width * pleatsPerMetre * 4f)), ny = 6;
            int start = part.VertexCount;
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx, v = j / (float)ny;
                    float x = (u - 0.5f) * width;
                    float phase = u * width * pleatsPerMetre * Mathf.PI * 2f;
                    // Folds deepen slightly towards the hem.
                    float z = Mathf.Sin(phase) * pleatDepth * (0.75f + 0.25f * (1f - v));
                    var nrm = new Vector3(-Mathf.Cos(phase) * pleatDepth * pleatsPerMetre * Mathf.PI * 2f, 0, 1f).normalized;
                    part.Add(new Vector3(x, -v * height, z), nrm, Color.white, new Vector2(u * width, (1f - v) * height));
                }
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = start + j * (nx + 1) + i;
                    part.Tri(a, a + 1, a + nx + 2);
                    part.Tri(a, a + nx + 2, a + nx + 1);
                }
            mb.Append(part, m);
        }

        /// <summary>Flat rug with softly rounded corners and a thin edge.</summary>
        public static void Rug(MeshBuilder mb, Vector3 center, Vector2 size, float corner = 0.05f)
        {
            var part = new MeshBuilder();
            Numu.World.RealisticKit.Slab(part, size, corner, 0.012f, 1f);
            mb.Append(part, Matrix4x4.Translate(center + Vector3.up * 0.012f));
        }

        /// <summary>Metal shelving unit (uprights + shelves) facing +z. Returns the shelf top heights.</summary>
        public static List<float> ShelfUnit(MeshBuilder metal, MeshBuilder shelves, Matrix4x4 m, float length, float height, float depth, int levels, float baseHeight = 0.12f)
        {
            var tops = new List<float>();
            foreach (float x in new[] { 0.015f, length - 0.015f })
            {
                Box(metal, m, new Vector3(x, height * 0.5f, 0.02f), new Vector3(0.03f, height, 0.04f));
                Box(metal, m, new Vector3(x, height * 0.5f, depth - 0.02f), new Vector3(0.03f, height, 0.04f));
            }
            for (int i = 0; i < levels; i++)
            {
                float y = baseHeight + (height - baseHeight - 0.15f) * i / Mathf.Max(1, levels - 1);
                Box(shelves, m, new Vector3(length * 0.5f, y, depth * 0.5f), new Vector3(length - 0.03f, 0.025f, depth));
                tops.Add(y + 0.0125f);
            }
            return tops;
        }
    }
}
