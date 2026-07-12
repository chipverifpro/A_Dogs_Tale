#!/usr/bin/env python3
"""Generate a Unity-ready shepherd's crook OBJ with outward-facing normals."""

from __future__ import annotations

import math
from pathlib import Path


OUT_DIR = Path(__file__).resolve().parent
OBJ_PATH = OUT_DIR / "ShepherdsCrook.obj"
MTL_PATH = OUT_DIR / "ShepherdsCrook.mtl"


def normalize(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v)


def centerline():
    points = []
    # A subtly hand-hewn shaft, 1.52 Unity units tall.
    shaft_segments = 31
    for i in range(shaft_segments + 1):
        y = 1.52 * i / shaft_segments
        blend = math.sin(math.pi * i / shaft_segments)
        x = 0.008 * math.sin(y * 5.1) * blend
        z = 0.006 * math.sin(y * 7.3 + 0.6) * blend
        points.append((x, y, z))

    # A broad 190-degree hook, tangent to the shaft at its base.
    hook_segments = 31
    radius = 0.245
    cx = points[-1][0] + radius
    cy = points[-1][1]
    cz = points[-1][2]
    for i in range(1, hook_segments + 1):
        theta = math.pi - math.radians(190.0) * i / hook_segments
        x = cx + radius * math.cos(theta)
        y = cy + radius * math.sin(theta)
        z = cz + 0.008 * math.sin(i * math.pi / hook_segments)
        points.append((x, y, z))
    return points


def build_mesh():
    path = centerline()
    sides = 12
    vertices = []
    normals = []
    uvs = []
    faces = []

    distances = [0.0]
    for a, b in zip(path, path[1:]):
        distances.append(distances[-1] + math.dist(a, b))
    total_length = distances[-1]

    for i, p in enumerate(path):
        if i == 0:
            tangent = normalize(tuple(path[1][k] - p[k] for k in range(3)))
        elif i == len(path) - 1:
            tangent = normalize(tuple(p[k] - path[i - 1][k] for k in range(3)))
        else:
            tangent = normalize(tuple(path[i + 1][k] - path[i - 1][k] for k in range(3)))

        # The path is near the XY plane. This frame makes ring winding explicit.
        side = normalize((-tangent[1], tangent[0], 0.0))
        depth = normalize((
            tangent[1] * side[2] - tangent[2] * side[1],
            tangent[2] * side[0] - tangent[0] * side[2],
            tangent[0] * side[1] - tangent[1] * side[0],
        ))
        progress = distances[i] / total_length
        base_radius = 0.040 * (1.0 - 0.25 * progress)

        for j in range(sides):
            angle = 2.0 * math.pi * j / sides
            # Small deterministic variation keeps the silhouette natural.
            variation = 1.0 + 0.045 * math.sin(i * 0.83 + j * 1.71)
            radius = base_radius * variation
            radial = normalize(tuple(
                math.cos(angle) * side[k] + math.sin(angle) * depth[k]
                for k in range(3)
            ))
            vertices.append(tuple(p[k] + radius * radial[k] for k in range(3)))
            normals.append(radial)
            uvs.append((j / sides, progress))

    rings = len(path)
    for i in range(rings - 1):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            c = (i + 1) * sides + (j + 1) % sides
            d = (i + 1) * sides + j
            faces.append((a, b, c, d))

    # Closed caps: bottom order is reversed; top follows the ring order.
    bottom_center = len(vertices)
    vertices.append(path[0])
    normals.append(normalize(tuple(path[0][k] - path[1][k] for k in range(3))))
    uvs.append((0.5, 0.5))
    top_center = len(vertices)
    vertices.append(path[-1])
    normals.append(normalize(tuple(path[-1][k] - path[-2][k] for k in range(3))))
    uvs.append((0.5, 0.5))
    for j in range(sides):
        nxt = (j + 1) % sides
        faces.append((bottom_center, nxt, j))
        top = (rings - 1) * sides
        faces.append((top_center, top + j, top + nxt))

    return vertices, normals, uvs, faces


def signed_volume(vertices, faces):
    volume = 0.0
    for face in faces:
        for i in range(1, len(face) - 1):
            a, b, c = vertices[face[0]], vertices[face[i]], vertices[face[i + 1]]
            volume += (
                a[0] * (b[1] * c[2] - b[2] * c[1])
                + a[1] * (b[2] * c[0] - b[0] * c[2])
                + a[2] * (b[0] * c[1] - b[1] * c[0])
            ) / 6.0
    return volume


def main():
    vertices, normals, uvs, faces = build_mesh()
    volume = signed_volume(vertices, faces)
    if volume <= 0.0:
        raise RuntimeError(f"Mesh winding is inverted (signed volume {volume:.6f})")

    lines = [
        "# Shepherd's crook generated for A Dog's Tale",
        "# Units: meters / Unity units; Y-up",
        "mtllib ShepherdsCrook.mtl",
        "o ShepherdsCrook",
    ]
    lines.extend(f"v {x:.6f} {y:.6f} {z:.6f}" for x, y, z in vertices)
    lines.extend(f"vt {u:.6f} {v:.6f}" for u, v in uvs)
    lines.extend(f"vn {x:.6f} {y:.6f} {z:.6f}" for x, y, z in normals)
    lines.extend(("usemtl ShepherdsCrook_Wood", "s 1"))
    for face in faces:
        # Vertex, UV, and normal arrays intentionally share indices.
        lines.append("f " + " ".join(f"{i + 1}/{i + 1}/{i + 1}" for i in face))
    OBJ_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")

    MTL_PATH.write_text(
        "# Warm, weathered wood\n"
        "newmtl ShepherdsCrook_Wood\n"
        "Ka 0.090 0.045 0.018\n"
        "Kd 0.390 0.185 0.065\n"
        "Ks 0.035 0.025 0.015\n"
        "Ns 18.0\n"
        "d 1.0\n"
        "illum 2\n",
        encoding="utf-8",
    )
    print(f"Wrote {OBJ_PATH.name}: {len(vertices)} vertices, {len(faces)} faces")
    print(f"Outward-winding check passed; signed volume = {volume:.6f}")


if __name__ == "__main__":
    main()
