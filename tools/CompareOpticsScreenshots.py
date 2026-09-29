"""Compare raw 4320x2160 RhinoX reference and SpatialXR screen captures.

Usage: python tools/CompareOpticsScreenshots.py reference.png candidate.png output_dir
No registration or flipping is applied: orientation and position errors are part
of the optical comparison.
"""

import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageChops, ImageDraw


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    reference_path, candidate_path, output_path = map(Path, sys.argv[1:])
    output_path.mkdir(parents=True, exist_ok=True)
    with Image.open(reference_path) as image:
        reference = image.convert("RGB")
    with Image.open(candidate_path) as image:
        candidate = image.convert("RGB")
    if reference.size != candidate.size or reference.size != (4320, 2160):
        raise SystemExit(f"Expected two 4320x2160 images, got {reference.size} and {candidate.size}")

    difference = ImageChops.difference(reference, candidate)
    difference.save(output_path / "difference.png")
    metrics = {}
    for eye, box in (("left", (0, 0, 2160, 2160)),
                     ("right", (2160, 0, 4320, 2160))):
        values = np.asarray(difference.crop(box), dtype=np.uint8)
        reference_eye = np.asarray(reference.crop(box), dtype=np.uint8)
        candidate_eye = np.asarray(candidate.crop(box), dtype=np.uint8)
        # Ignore the outer 80 px where Cardboard's diagnostic buttons appear.
        reference_interior = reference_eye[80:2080, 80:2080]
        candidate_interior = candidate_eye[80:2080, 80:2080]
        reference_edges = cv2.Canny(cv2.cvtColor(reference_interior, cv2.COLOR_RGB2GRAY), 50, 110)
        candidate_edges = cv2.Canny(cv2.cvtColor(candidate_interior, cv2.COLOR_RGB2GRAY), 50, 110)
        distance_to_candidate_edge = cv2.distanceTransform(
            255 - candidate_edges, cv2.DIST_L2, 3
        )
        flat_mask = cv2.dilate(
            reference_edges | candidate_edges, np.ones((7, 7), np.uint8)
        ) == 0
        interior_difference = np.abs(
            reference_interior.astype(np.int16) - candidate_interior.astype(np.int16)
        )
        metrics[eye] = {
            "mean_absolute_channel_error": round(float(values.mean()), 3),
            "pixels_with_channel_error_over_32_percent": round(
                float((values.max(axis=2) > 32).mean() * 100), 3),
            "reference_edges_within_2px_percent": round(float(
                (distance_to_candidate_edge[reference_edges > 0] <= 2).mean() * 100
            ), 3),
            "flat_region_mean_absolute_channel_error": round(float(
                interior_difference[flat_mask].mean()
            ), 3),
        }

    preview = Image.new("RGB", (2160, 1660), (25, 25, 25))
    draw = ImageDraw.Draw(preview)
    for row, (label, image) in enumerate((
        ("Reference", reference), ("SpatialXR", candidate), ("Absolute difference", difference)
    )):
        draw.text((8, row * 550 + 6), label, fill=(255, 255, 255))
        preview.paste(image.resize((2160, 540), Image.Resampling.BILINEAR), (0, row * 550 + 20))
    preview.save(output_path / "comparison-preview.jpg", quality=90)
    (output_path / "metrics.json").write_text(
        json.dumps(metrics, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(metrics, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
