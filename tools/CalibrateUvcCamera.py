"""HXQ calibration and stage-2 accuracy measurements. No synthetic hardware acceptance."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import math
import sys

import cv2
import numpy as np


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + "\n", encoding="utf-8")


def board_points(columns, rows, square_mm):
    if columns < 3 or rows < 3 or square_mm <= 0:
        raise ValueError("Use at least 3x3 inner corners and a measured positive square size")
    points = np.zeros((columns * rows, 3), np.float32)
    points[:, :2] = np.mgrid[0:columns, 0:rows].T.reshape(-1, 2) * (square_mm / 1000)
    return points


def make_board(args):
    board_points(args.columns, args.rows, args.square_mm)
    margin = args.square_mm
    width, height = (args.columns + 3) * margin, (args.rows + 3) * margin
    content = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}mm" height="{height}mm" viewBox="0 0 {width} {height}">',
               f'<rect width="{width}" height="{height}" fill="white"/>']
    for y in range(args.rows + 1):
        for x in range(args.columns + 1):
            if (x + y) % 2 == 0:
                content.append(f'<rect x="{(x+1)*margin}" y="{(y+1)*margin}" width="{margin}" height="{margin}" fill="black"/>')
    content.append('</svg>')
    path = Path(args.output)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(content), encoding="utf-8")
    print(f"Board: {args.columns}x{args.rows} INNER corners; {args.square_mm} mm squares. Print at 100%, measure after printing. Size {width}x{height} mm.")


def reprojection(points, corners, rotation, translation, camera, distortion):
    predicted, _ = cv2.projectPoints(points, rotation, translation, camera, distortion)
    error = predicted.reshape(-1, 2) - corners.reshape(-1, 2)
    return float(np.sqrt(np.mean(np.sum(error * error, axis=1))))


def intrinsic(args):
    pattern = (args.columns, args.rows)
    points = board_points(*pattern, args.square_mm)
    found, rejected, size, metadata, hashes = [], [], None, [], set()
    for path in sorted(Path(args.images).glob("*.png")):
        image = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
        if image is None:
            rejected.append({"file": path.name, "reason": "cannot read"})
            continue
        current = (image.shape[1], image.shape[0])
        if size is not None and size != current:
            raise ValueError("Mixed image sizes: collect one camera/mode per session")
        size = current
        sidecar = path.with_suffix(".json")
        if sidecar.exists():
            meta = json.loads(sidecar.read_text(encoding="utf-8-sig"))
            if meta.get("schemaVersion") != 1:
                raise ValueError("Unsupported capture metadata schema")
            if meta.get("cameraId") != args.camera_id or not meta.get("fullFieldOfView"):
                raise ValueError(f"Wrong camera or unknown crop: {sidecar}")
            if (meta.get("imageWidth"), meta.get("imageHeight")) != current or meta.get("imageRotationClockwise") != 0 or meta.get("inputMirrored"):
                raise ValueError("Calibrate canonical unrotated/unmirrored input only")
            if (meta.get("nativeWidth"), meta.get("nativeHeight")) != tuple(args.runtime_size):
                raise ValueError("Recorded native mode differs from requested runtime size")
            metadata.append(meta)
        elif current != tuple(args.runtime_size):
            raise ValueError("Scaling lower-resolution images requires recorder sidecars proving full field of view")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest in hashes:
            rejected.append({"file": path.name, "reason": "duplicate image bytes"})
            continue
        hashes.add(digest)
        ok, corners = cv2.findChessboardCornersSB(image, pattern, flags=cv2.CALIB_CB_NORMALIZE_IMAGE | cv2.CALIB_CB_EXHAUSTIVE)
        if not ok:
            rejected.append({"file": path.name, "reason": "all inner corners not found"})
            continue
        found.append((path, corners.astype(np.float32)))
    if len(found) < 18:
        raise ValueError(f"Only {len(found)} usable images; collect >=18, preferably 25-30 varied views")
    if args.runtime_size[0] <= 0 or args.runtime_size[1] <= 0 or abs(size[0]/size[1] - args.runtime_size[0]/args.runtime_size[1]) > .001:
        raise ValueError("Runtime size must preserve calibrated field of view/aspect ratio")
    # A held-out image is never part of intrinsics fitting. Its board pose still
    # requires solvePnP, so this tests projection consistency, not hand accuracy.
    holdout = [view for i, view in enumerate(found) if i % 5 == 0]
    training = [view for i, view in enumerate(found) if i % 5 != 0]
    rms, camera, distortion, rotations, translations = cv2.calibrateCamera(
        [points] * len(training), [v[1] for v in training], size, None, None)
    if not np.isfinite(camera).all() or not np.isfinite(distortion).all():
        raise ValueError("Nonfinite fit")
    views = []
    for view, rotation, translation in zip(training, rotations, translations):
        views.append({"file": view[0].name, "set": "training", "rmsPixels": reprojection(points, view[1], rotation, translation, camera, distortion)})
    for path, corners in holdout:
        ok, rotation, translation = cv2.solvePnP(points, corners, camera, distortion)
        if not ok:
            raise ValueError(f"Held-out PnP failed: {path}")
        views.append({"file": path.name, "set": "held-out", "rmsPixels": reprojection(points, corners, rotation, translation, camera, distortion)})
    sx, sy = args.runtime_size[0]/size[0], args.runtime_size[1]/size[1]
    max_heldout = max(v["rmsPixels"] for v in views if v["set"] == "held-out")
    normals = [cv2.Rodrigues(rotation)[0][:, 2] for rotation in rotations]
    max_normal_angle = max(math.degrees(math.acos(float(np.clip(abs(a @ b), 0, 1)))) for a in normals for b in normals)
    plausible = (0 < camera[0, 0] < size[0]*10 and 0 < camera[1, 1] < size[1]*10 and
                 0 <= camera[0, 2] <= size[0] and 0 <= camera[1, 2] <= size[1])
    passed = bool(plausible and max_normal_angle >= 10 and rms <= args.max_rms and max_heldout <= args.max_rms)
    profile = {"schemaVersion": 1, "cameraId": args.camera_id, "calibrationVersion": args.version,
               "source": "HXQ chessboard fit; see adjacent report; preview scaling is an approximation",
               "imageWidth": args.runtime_size[0], "imageHeight": args.runtime_size[1],
               "fx": float(camera[0, 0]*sx), "fy": float(camera[1, 1]*sy), "cx": float(camera[0, 2]*sx), "cy": float(camera[1, 2]*sy),
               "distortion": distortion.flatten().tolist(), "imageRotationClockwise": 0, "inputMirrored": False, "swapHandedness": False,
               "intrinsicsCalibrated": passed, "extrinsicsMeasured": False, "handednessVerified": False, "palmDimensionsMeasured": False,
               "headFromCameraTranslationMetres": [0, 0, 0], "headFromCameraRotationXyzw": [0, 0, 0, 1],
               "palmWidthMetres": 0, "wristToMiddleMetres": 0, "minDepthMetres": .2, "maxDepthMetres": 1.2,
               "validatedForHardware": False}
    report = {"opencvVersion": cv2.__version__, "cameraId": args.camera_id, "version": args.version,
              "model": "Brown-Conrady k1,k2,p1,p2,k3", "squareMm": args.square_mm, "innerCorners": pattern,
              "calibratedImageSize": size, "runtimeSize": args.runtime_size, "scale": [sx, sy],
              "trainingRmsPixels": rms, "runtimeEquivalentTrainingRmsPixels": rms*max(sx, sy),
              "maxHeldOutRmsPixels": max_heldout, "maxAllowedRmsAtCaptureResolution": args.max_rms,
              "intrinsicsQualityPassed": passed, "hardwareHandAccuracyPassed": False,
              "maxBoardNormalAngleDegrees": max_normal_angle, "minRequiredBoardNormalAngleDegrees": 10,
              "views": views, "rejected": rejected,
              "sha256": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p, _ in found}}
    write_json(args.output, profile)
    write_json(str(args.output) + ".report.json", report)
    print(f"Intrinsics quality: {'PASS' if passed else 'FAIL'}; train RMS={rms:.3f}px, held-out max={max_heldout:.3f}px. Installation/hand scale/handedness still unset.")
    return 0 if passed else 2


def quaternion(matrix):
    # Rodrigues handles near-180 rotations without the trace-only singularity.
    rotation, _ = cv2.Rodrigues(matrix)
    angle = float(np.linalg.norm(rotation))
    if angle < 1e-12:
        return [0, 0, 0, 1]
    return (rotation.flatten()/angle * math.sin(angle/2)).tolist() + [math.cos(angle/2)]


def fit_extrinsics(args):
    if not args.confirm_independent_reference:
        raise ValueError("Use independently measured physical-head correspondences; pass --confirm-independent-reference after measuring")
    data = json.loads(Path(args.points).read_text(encoding="utf-8-sig"))
    if not data.get("referenceSource"):
        raise ValueError("Record how physical head positions were independently measured")
    camera = np.asarray(data["cameraPointsMetres"], np.float64)
    head = np.asarray(data["physicalHeadPointsMetres"], np.float64)
    if camera.ndim != 2 or camera.shape[1:] != (3,) or camera.shape != head.shape or len(camera) < 6 or not np.isfinite(camera).all() or not np.isfinite(head).all():
        raise ValueError("Need >=6 matching finite XYZ points, in metres")
    a, b = camera - camera.mean(axis=0), head - head.mean(axis=0)
    if np.linalg.matrix_rank(a, tol=1e-6) < 3 or np.linalg.matrix_rank(b, tol=1e-6) < 3:
        raise ValueError("Use noncoplanar anchors spanning a volume")
    u, _, vt = np.linalg.svd(a.T @ b)
    fix = np.eye(3)
    fix[2, 2] = np.linalg.det(vt.T @ u.T)
    rotation = vt.T @ fix @ u.T
    translation = head.mean(axis=0) - rotation @ camera.mean(axis=0)
    errors = np.linalg.norm(camera @ rotation.T + translation - head, axis=1)
    rms, p95 = float(np.sqrt(np.mean(errors**2))), float(np.percentile(errors, 95))
    passed = p95 <= args.max_error_metres
    report = {"referenceSource": data["referenceSource"], "samples": len(camera), "rmsMetres": rms, "p95Metres": p95,
              "properRotationDeterminant": float(np.linalg.det(rotation)), "passed": passed,
              "pointsSha256": hashlib.sha256(Path(args.points).read_bytes()).hexdigest()}
    write_json(str(args.output) + ".extrinsics-report.json", report)
    if not passed:
        print(f"Extrinsics FAIL: P95={p95:.4f}m; profile not written")
        return 2
    profile = json.loads(Path(args.profile).read_text(encoding="utf-8-sig"))
    profile.update(headFromCameraTranslationMetres=translation.tolist(), headFromCameraRotationXyzw=quaternion(rotation),
                   extrinsicsMeasured=True, validatedForHardware=False, calibrationVersion=args.version)
    write_json(args.output, profile)
    print(f"Extrinsics fit P95={p95:.4f}m. Handedness/palm scale/optical alignment still require verification.")
    return 0


def finite(row, name):
    value = float(row[name])
    if not math.isfinite(value):
        raise ValueError(f"Nonfinite {name}")
    return value


def truth(value):
    if value not in ("true", "false", "1", "0"):
        raise ValueError("Validity flags must be true/false or 1/0")
    return value in ("true", "1")


def accuracy(args):
    with Path(args.csv).open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        raise ValueError("No measurement rows")
    groups = {}
    for row in rows:
        if row["side"] not in ("Left", "Right") or row["joint"] not in ("Wrist", "IndexTip") or not row["condition"]:
            raise ValueError("Each row needs Left/Right, Wrist/IndexTip and a named test condition")
        key = "/".join((row["side"], row["joint"], row["condition"]))
        group = groups.setdefault(key, {"samples": 0, "position": [], "scale": [], "angle": []})
        group["samples"] += 1
        if truth(row["positionValid"]):
            ref = np.array([finite(row, "reference"+axis) for axis in "XYZ"])
            est = np.array([finite(row, "estimated"+axis) for axis in "XYZ"])
            group["position"].append(float(np.linalg.norm(ref-est)))
        if truth(row["scaleValid"]):
            ref, observed = finite(row, "expectedSizeMetres"), finite(row, "observedSizeMetres")
            if ref <= 0 or observed <= 0:
                raise ValueError("Size must be positive metres")
            group["scale"].append(abs(observed/ref-1))
        if truth(row["orientationValid"]):
            angle = finite(row, "orientationErrorDegrees")
            if not 0 <= angle <= 180:
                raise ValueError("Orientation error must be in [0,180] degrees")
            group["angle"].append(angle)
    results = {}
    for key, group in groups.items():
        item = {"samples": group["samples"]}
        passed = group["samples"] >= 100
        for metric, threshold in (("position", .03), ("scale", .10), ("angle", 15)):
            values = group[metric]
            coverage = len(values)/group["samples"]
            p95 = float(np.percentile(values, 95)) if values else None
            item[metric] = {"validRate": coverage, "p50": float(np.median(values)) if values else None, "p95": p95, "maxAllowedP95": threshold}
            passed = passed and coverage >= .90 and p95 is not None and p95 <= threshold
        item["passed"] = bool(passed)
        results[key] = item
    sides_joints = {"/".join(key.split("/")[:2]) for key in results}
    both = {"Left/Wrist", "Left/IndexTip", "Right/Wrist", "Right/IndexTip"}.issubset(sides_joints)
    numerical_pass = bool(both and all(v["passed"] for v in results.values()))
    evidence = args.evidence.strip()
    report = {"dataKind": args.data_kind, "referenceEvidence": evidence,
              "csvSha256": hashlib.sha256(Path(args.csv).read_bytes()).hexdigest(),
              "positionUnits": "metres", "scaleUnits": "relative", "angleUnits": "degrees",
              "groups": results, "allNumericalTargetsPassed": numerical_pass,
              "hardwareReviewEligible": bool(numerical_pass and args.data_kind == "hardware" and evidence),
              "validatedForHardware": False,
              "note": "Final lens alignment and dynamic tests require human acceptance. No profile flag is changed."}
    write_json(args.output, report)
    print(f"Numerical targets: {'PASS' if numerical_pass else 'FAIL/INSUFFICIENT'}; data={args.data_kind}. Hardware validation remains manual.")
    return 0 if numerical_pass else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name, handler in (("board", make_board), ("intrinsics", intrinsic)):
        cmd = commands.add_parser(name)
        cmd.set_defaults(handler=handler)
        cmd.add_argument("--columns", type=int, default=9, help="Inner corners, not squares")
        cmd.add_argument("--rows", type=int, default=6)
        cmd.add_argument("--square-mm", type=float, default=20)
        cmd.add_argument("--output", required=True)
        if name == "intrinsics":
            cmd.add_argument("--images", required=True)
            cmd.add_argument("--camera-id", default="uvc:11cb:281b")
            cmd.add_argument("--runtime-size", type=int, nargs=2, default=[640, 480])
            cmd.add_argument("--version", required=True)
            cmd.add_argument("--max-rms", type=float, default=.7, help="Pixels at CAPTURE resolution; projection check only")
    cmd = commands.add_parser("extrinsics")
    cmd.set_defaults(handler=fit_extrinsics)
    for name in ("profile", "points", "output", "version"):
        cmd.add_argument("--"+name, required=True)
    cmd.add_argument("--confirm-independent-reference", action="store_true")
    cmd.add_argument("--max-error-metres", type=float, default=.01)
    cmd = commands.add_parser("accuracy")
    cmd.set_defaults(handler=accuracy)
    cmd.add_argument("--csv", required=True)
    cmd.add_argument("--output", required=True)
    cmd.add_argument("--data-kind", choices=("synthetic", "hardware"), default="synthetic")
    cmd.add_argument("--evidence", default="", help="Independent reference measurement procedure/file, not the converted hand itself")
    args = parser.parse_args()
    try:
        return args.handler(args) or 0
    except (ValueError, KeyError, OSError, cv2.error) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
