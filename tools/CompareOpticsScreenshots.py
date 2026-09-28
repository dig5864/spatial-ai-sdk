"""Compare raw 4320x2160 RhinoX reference and SpatialXR screen captures.

Usage: python tools/CompareOpticsScreenshots.py reference.png candidate.png output_dir
No registration or flipping is applied: orientation and position errors are part
of the optical comparison.
"""

import json
import sys
from pathlib import Path

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
        metrics[eye] = {
            "mean_absolute_channel_error": round(float(values.mean()), 3),
            "pixels_with_channel_error_over_32_percent": round(
                float((values.max(axis=2) > 32).mean() * 100), 3),
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
