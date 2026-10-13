using System;
using SpatialAI.Core;

static class ScreenLayoutChecks
{
    public static void Run(Action<bool, string> check)
    {
        var reference = new float[] { 268.220f,157.045f,865.054f,865.054f,1266.727f,157.045f,865.054f,865.054f };
        var screen = new PhoneScreenCalibration { phoneKey = "reference", widthMm = 144, heightMm = 64.8f, measured = true };
        check(ScreenLayout.FromReference(reference, 2400, 1080, screen, "rig", true, out var rig).Success, "physical rig generated from measured reference");
        check(!rig.opticsValidated && rig.referenceScreenMeasured, "screen measurement does not validate optics");
        check(ScreenLayout.Calculate(rig, screen, 2400, 1080, out var original).Success, "reference layout resolves");
        for (int i = 0; i < 8; i++) check(Math.Abs(original[i] - reference[i]) < .001f, "reference round-trip " + i);
        check(ScreenLayout.Calculate(rig, screen, 1920, 864, out var reduced).Success, "same panel reduced resolution accepted");
        for (int i = 0; i < 8; i++) check(Math.Abs(reduced[i] - reference[i] * .8f) < .001f, "downsample preserves physical layout " + i);
        var larger = new PhoneScreenCalibration { phoneKey = "larger", widthMm = 160, heightMm = 72 };
        check(ScreenLayout.Calculate(rig, larger, 2400, 1080, out var largeRects).Success, "larger phone resolves");
        check(Math.Abs(largeRects[2] / (2400 / 160f) - original[2] / (2400 / 144f)) < .001f,
            "different phone at same resolution preserves millimetres");
        check(Math.Abs(largeRects[2] - original[2]) > 80, "same resolution does not imply same rectangle");
        larger.offsetXmm = 2; larger.offsetYmm = -1;
        check(ScreenLayout.Calculate(rig, larger, 2400, 1080, out var shifted).Success, "mount offsets accepted");
        check(Math.Abs(shifted[0] - largeRects[0] + 30) < .001f && Math.Abs(shifted[1] - largeRects[1] - 15) < .001f,
            "mount translation maps with right/down signs");
        screen.offsetXmm = 3; screen.offsetYmm = 2;
        check(ScreenLayout.FromReference(reference, 2400, 1080, screen, "offset-rig", false, out var offsetRig).Success &&
            ScreenLayout.Calculate(offsetRig, screen, 2400, 1080, out var offsetOutput).Success && Math.Abs(offsetOutput[0] - reference[0]) < .001f,
            "reference mount offset included only once");
        var small = new PhoneScreenCalibration { phoneKey = "too-small", widthMm = 80, heightMm = 35 };
        check(!ScreenLayout.Calculate(rig, small, 2400, 1080, out _).Success, "undersized screen rejected without shrinking");
        screen.widthMm = float.NaN;
        check(!ScreenLayout.Calculate(rig, screen, 2400, 1080, out _).Success, "invalid physical size rejected");
        screen.widthMm = 144; rig.eyeRectsMm[0] = float.PositiveInfinity;
        check(!ScreenLayout.Calculate(rig, screen, 2400, 1080, out _).Success, "nonfinite rig rejected");
        check(!ScreenLayout.FromReference(new float[7], 2400, 1080, screen, "invalid", false, out _).Success, "invalid reference shape rejected");
    }
}
