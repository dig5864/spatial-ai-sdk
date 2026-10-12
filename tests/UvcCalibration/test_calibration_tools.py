import argparse
import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("uvc_tools", ROOT / "tools/CalibrateUvcCamera.py")
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)


class CalibrationChecks(unittest.TestCase):
    def setUp(self):
        (ROOT / "build/validation").mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / "build/validation", prefix="uvc-tools-synthetic-")
        self.path = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_real_corner_detection_and_held_out_intrinsic_fit(self):
        random = np.random.default_rng(37)
        camera = np.array([[520., 0, 320], [0, 510., 240], [0, 0, 1]])
        pixels = np.zeros((280, 400), np.uint8)
        for y in range(7):
            for x in range(10):
                pixels[y*40:(y+1)*40, x*40:(x+1)*40] = 255 if (x+y) % 2 else 0
        outer = np.array([[-.02, -.02, 0], [.18, -.02, 0], [.18, .12, 0], [-.02, .12, 0]], np.float32)
        source = np.array([[0, 0], [400, 0], [400, 280], [0, 280]], np.float32)
        for i in range(25):
            rotation = random.uniform(-.45, .45, 3)
            translation = np.array([random.uniform(-.10, -.035), random.uniform(-.07, -.025), random.uniform(.42, .62)])
            target, _ = cv2.projectPoints(outer, rotation, translation, camera, np.zeros(5))
            transform = cv2.getPerspectiveTransform(source, target.reshape(-1, 2))
            image = cv2.warpPerspective(pixels, transform, (640, 480), borderValue=190)
            cv2.imwrite(str(self.path / f"board-{i:03}.png"), image)
        args = argparse.Namespace(columns=9, rows=6, square_mm=20, images=str(self.path), camera_id="uvc:11cb:281b",
                                  runtime_size=[640, 480], version="synthetic-test-only", max_rms=.7, output=str(self.path / "intrinsics.json"))
        self.assertEqual(tool.intrinsic(args), 0)
        profile = json.loads(Path(args.output).read_text())
        report = json.loads(Path(args.output+".report.json").read_text())
        self.assertTrue(profile["intrinsicsCalibrated"])
        self.assertFalse(profile["extrinsicsMeasured"])
        self.assertFalse(profile["validatedForHardware"])
        self.assertAlmostEqual(profile["fx"], 520, delta=15)
        self.assertAlmostEqual(profile["fy"], 510, delta=15)
        self.assertGreaterEqual(sum(v["set"] == "held-out" for v in report["views"]), 4)
        self.assertFalse(report["hardwareHandAccuracyPassed"])
        args.runtime_size = [800, 480]
        with self.assertRaises(ValueError):
            tool.intrinsic(args)

    def test_rigid_extrinsics_units_and_degeneracy(self):
        camera = np.random.default_rng(4).uniform(-.2, .2, (16, 3))
        rotation, _ = cv2.Rodrigues(np.array([.3, -.2, .1]))
        translation = np.array([.025, -.05, .02])
        head = camera @ rotation.T + translation
        points = self.path / "points.json"
        profile = self.path / "profile.json"
        output = self.path / "result.json"
        tool.write_json(points, {"referenceSource": "synthetic-test-only", "cameraPointsMetres": camera.tolist(), "physicalHeadPointsMetres": head.tolist()})
        tool.write_json(profile, {"schemaVersion": 1})
        args = argparse.Namespace(points=str(points), profile=str(profile), output=str(output), version="synthetic", max_error_metres=.01, confirm_independent_reference=False)
        with self.assertRaises(ValueError):
            tool.fit_extrinsics(args)
        args.confirm_independent_reference = True
        self.assertEqual(tool.fit_extrinsics(args), 0)
        result = json.loads(output.read_text())
        np.testing.assert_allclose(result["headFromCameraTranslationMetres"], translation, atol=1e-8)
        quaternion = np.array(result["headFromCameraRotationXyzw"])
        axis = quaternion[:3] / np.linalg.norm(quaternion[:3])
        recovered, _ = cv2.Rodrigues(axis * (2*np.arccos(quaternion[3])))
        np.testing.assert_allclose(recovered, rotation, atol=1e-8)
        self.assertFalse(result["validatedForHardware"])
        camera[:, 2] = 0
        tool.write_json(points, {"referenceSource": "synthetic", "cameraPointsMetres": camera.tolist(), "physicalHeadPointsMetres": head.tolist()})
        with self.assertRaises(ValueError):
            tool.fit_extrinsics(args)

    def test_accuracy_reports_failures_coverage_and_synthetic_provenance(self):
        names = ["side", "joint", "condition", "positionValid", "referenceX", "referenceY", "referenceZ", "estimatedX", "estimatedY", "estimatedZ",
                 "scaleValid", "expectedSizeMetres", "observedSizeMetres", "orientationValid", "orientationErrorDegrees"]
        rows = []
        for side in ("Left", "Right"):
            for joint in ("Wrist", "IndexTip"):
                for _ in range(100):
                    rows.append(dict(zip(names, [side, joint, "synthetic-0.5m", "true", 0, 0, .5, .005, .005, .5, "true", .06, .061, "true", 4])))
        path = self.path / "accuracy.csv"
        def save():
            with path.open("w", newline="", encoding="utf-8") as handle:
                writer = csv.DictWriter(handle, fieldnames=names)
                writer.writeheader()
                writer.writerows(rows)
        save()
        args = argparse.Namespace(csv=str(path), output=str(self.path / "report.json"), data_kind="synthetic", evidence="")
        self.assertEqual(tool.accuracy(args), 0)
        report = json.loads(Path(args.output).read_text())
        self.assertFalse(report["hardwareReviewEligible"])
        self.assertFalse(report["validatedForHardware"])
        for row in rows[:30]:
            row["positionValid"] = "false"
        save()
        self.assertEqual(tool.accuracy(args), 2)
        for row in rows[:30]:
            row["positionValid"] = "true"
        rows[0]["estimatedX"] = "nan"
        save()
        with self.assertRaises(ValueError):
            tool.accuracy(args)

    def test_board_has_physical_size_and_inner_corner_counts(self):
        output = self.path / "board.svg"
        tool.make_board(argparse.Namespace(columns=9, rows=6, square_mm=20, output=str(output)))
        text = output.read_text()
        self.assertIn('width="240mm"', text)
        self.assertIn('height="180mm"', text)
        self.assertEqual(text.count('fill="black"'), 35)


if __name__ == "__main__":
    unittest.main()
