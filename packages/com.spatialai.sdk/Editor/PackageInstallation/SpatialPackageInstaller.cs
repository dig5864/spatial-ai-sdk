using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpatialAI.SDKInstallation
{
    // This assembly must compile before any Spatial AI / XR dependency exists.
    [InitializeOnLoad]
    public static class SpatialPackageInstaller
    {
        private static readonly string[] ExpectedNames = {
            "com.spatialai.xr-hands",
            "com.spatialai.xr-interaction", "com.google.xr.cardboard"
        };
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string StatePath => Path.Combine(ProjectRoot, "ProjectSettings/SpatialAISdkInstallationState.json");
        private static InstallPlan plan;
        private static InstallState state;
        private static string payloadRoot, releaseId, relativeDirectory, installerArchive, installerFilename;
        private static ListRequest listRequest;
        private static AddAndRemoveRequest installRequest;
        private static bool retryRequested, legacyInstallerInstalled;
        private static double nextCheck;

        static SpatialPackageInstaller()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearProgress;
            Schedule();
        }

        [MenuItem("Spatial AI/SDK Installation/Retry Installation")]
        public static void RetryInstallation()
        {
            if (installRequest != null || listRequest != null)
            {
                Debug.Log("SpatialAIInstaller: an operation is already running.");
                return;
            }
            plan = null;
            retryRequested = true;
            Schedule();
        }

        private static void Schedule()
        {
            nextCheck = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextCheck)
                return;
            try
            {
                if (installRequest != null)
                {
                    if (!installRequest.IsCompleted) return;
                    var request = installRequest;
                    installRequest = null;
                    if (request.Status == StatusCode.Failure)
                        throw new InvalidOperationException(request.Error.message);
                    // AddAndRemove results can precede package registration and contain only changed packages.
                    state.phase = "Verifying";
                    SaveState();
                    nextCheck = EditorApplication.timeSinceStartup + 1;
                    listRequest = Client.List(true, true);
                    return;
                }
                if (listRequest != null)
                {
                    if (!listRequest.IsCompleted) return;
                    var request = listRequest;
                    listRequest = null;
                    if (request.Status == StatusCode.Failure)
                        throw new InvalidOperationException(request.Error.message);
                    legacyInstallerInstalled = request.Result.Any(x => x.name == "com.spatialai.installer");
                    if (MatchesInstalledPackages(request.Result)) Complete();
                    else if (state.phase == "Verifying")
                    {
                        foreach (var package in request.Result.Where(x => ExpectedNames.Contains(x.name) || x.name == "com.spatialai.sdk" || x.name == "com.spatialai.installer"))
                            Debug.Log("SpatialAIInstaller: registered " + package.name + " version=" + package.version + " source=" + package.source + " id=" + package.packageId);
                        throw new InvalidOperationException("UPM did not activate the bundled tarballs. Check Console for registered package identities.");
                    }
                    else BeginInstall();
                    return;
                }
                if (!LoadPlan()) return;
                if (state.releaseId == releaseId && (state.phase == "Completed" || state.phase == "Failed"))
                {
                    Stop();
                    return;
                }
                PrepareArchives();
                if (state.releaseId != releaseId) state = new InstallState { releaseId = releaseId };
                SaveState();
                ShowProgress("Checking installed packages", 0.2f);
                // Serialize all Client operations; after a domain reload, reconcile before adding again.
                listRequest = Client.List(true, true);
            }
            catch (Exception error) { Fail(error); }
        }

        private static bool LoadPlan()
        {
            if (plan != null) return true;
            var package = PackageInfo.FindForAssembly(typeof(SpatialPackageInstaller).Assembly);
            if (package == null) { nextCheck = EditorApplication.timeSinceStartup + 1; return false; }
            payloadRoot = Path.Combine(package.resolvedPath, "Payload~");
            string manifest = Path.Combine(payloadRoot, "install-manifest.json");
            // A source checkout has no generated payload; only the release SDK runs automatically.
            if (!File.Exists(manifest)) { Stop(); return false; }
            if (package.source != PackageSource.LocalTarball)
                throw new InvalidOperationException("Install the release SDK with Package Manager > Add package from tarball.");
            installerArchive = TarballPath(package);
            installerFilename = package.name + "-" + package.version + ".tgz";
            string json = File.ReadAllText(manifest);
            plan = JsonUtility.FromJson<InstallPlan>(json);
            if (plan == null || !SafeName(plan.version) || plan.packages == null || plan.packages.Length != ExpectedNames.Length ||
                plan.packages.Select(x => x.name).Distinct().Count() != ExpectedNames.Length ||
                plan.packages.Any(x => !ExpectedNames.Contains(x.name) || !SafeName(x.version) ||
                    !SafeName(x.file) || !x.file.EndsWith(".tgz", StringComparison.Ordinal) ||
                    x.sha256 == null || !Regex.IsMatch(x.sha256, "^[a-f0-9]{64}$")))
                throw new InvalidDataException("Invalid bundled installation manifest.");
            releaseId = HashFile(installerArchive);
            relativeDirectory = "SDKPackages/SpatialAI/" + plan.version + "/" + releaseId.Substring(0, 12);
            state = !retryRequested && File.Exists(StatePath)
                ? JsonUtility.FromJson<InstallState>(File.ReadAllText(StatePath)) : new InstallState();
            if (state == null) throw new InvalidDataException("Installer state is invalid. Use Spatial AI > SDK Installation > Retry Installation.");
            retryRequested = false;
            return true;
        }

        private static bool SafeName(string value) => value != null && Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]*$");

        private static void PrepareArchives()
        {
            // Validate every source and existing destination before copying or modifying dependencies.
            foreach (var package in plan.packages)
            {
                string embedded = Path.Combine(ProjectRoot, "Packages", package.name);
                var existing = PackageInfo.FindForAssetPath("Packages/" + package.name);
                // Unity virtualizes Packages paths even for System.IO; use UPM's actual source kind.
                if (existing != null && existing.source == PackageSource.Embedded)
                    throw new InvalidOperationException("Move the existing embedded package outside Packages before installing: " + embedded);
                if (HashFile(Path.Combine(payloadRoot, package.file)) != package.sha256)
                    throw new InvalidDataException("Bundled archive checksum mismatch: " + package.file);
                string target = ArchivePath(package);
                if (File.Exists(target) && HashFile(target) != package.sha256)
                    throw new InvalidDataException("Existing project archive has different content: " + target);
            }
            Directory.CreateDirectory(Path.Combine(ProjectRoot, relativeDirectory));
            foreach (var package in plan.packages)
                if (!File.Exists(ArchivePath(package))) File.Copy(Path.Combine(payloadRoot, package.file), ArchivePath(package));
            string localInstaller = Path.Combine(ProjectRoot, relativeDirectory, installerFilename);
            if (File.Exists(localInstaller) && HashFile(localInstaller) != releaseId)
                throw new InvalidDataException("Existing installer archive has different content: " + localInstaller);
            if (!File.Exists(localInstaller)) File.Copy(installerArchive, localInstaller);
        }

        private static string ArchivePath(PackageEntry package) => Path.GetFullPath(Path.Combine(ProjectRoot, relativeDirectory, package.file));
        private static string Reference(PackageEntry package) => "file:../" + relativeDirectory + "/" + package.file;

        private static bool MatchesInstalledPackages(IEnumerable<PackageInfo> installed)
        {
            var packages = installed.ToArray();
            foreach (var expected in plan.packages)
            {
                var actual = packages.FirstOrDefault(x => x.name == expected.name);
                if (actual == null || actual.version != expected.version || actual.source != PackageSource.LocalTarball)
                    return false;
                if (!string.Equals(TarballPath(actual), ArchivePath(expected),
                    Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    return false;
            }
            if (legacyInstallerInstalled) return false;
            var installer = packages.FirstOrDefault(x => x.name == "com.spatialai.sdk");
            return installer != null && installer.source == PackageSource.LocalTarball &&
                string.Equals(TarballPath(installer), Path.GetFullPath(Path.Combine(ProjectRoot, relativeDirectory, installerFilename)),
                    Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static string TarballPath(PackageInfo package)
        {
            string prefix = package.name + "@file:";
            if (!package.packageId.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Expected a local tarball identifier: " + package.name);
            return Path.GetFullPath(Path.Combine(ProjectRoot, "Packages", package.packageId.Substring(prefix.Length)));
        }

        private static void BeginInstall()
        {
            if (state.attempts >= 3)
                throw new InvalidOperationException("Installation could not resume after repeated assembly reloads. Check Console, then use Spatial AI > SDK Installation > Retry Installation.");
            string backup = Path.Combine(ProjectRoot, relativeDirectory, "manifest-before-install.json");
            if (!File.Exists(backup)) File.Copy(Path.Combine(ProjectRoot, "Packages/manifest.json"), backup);
            state.phase = "Installing";
            state.attempts++;
            SaveState();
            Debug.Log("SpatialAIInstaller: installing three bundled dependencies, release=" + plan.version);
            ShowProgress("Installing SDK and resolving Unity XR dependencies", 0.5f);
            // Retain the entry tarball too, so the project no longer depends on the user's download directory.
            var references = plan.packages.Select(Reference).Concat(new[] { "file:../" + relativeDirectory + "/" + installerFilename }).ToArray();
            installRequest = Client.AddAndRemove(references, legacyInstallerInstalled ? new[] { "com.spatialai.installer" } : new string[0]);
        }

        private static void Complete()
        {
            state.phase = "Completed";
            state.error = "";
            SaveState();
            Debug.Log("SpatialAIInstaller: completed release=" + plan.version + ". Continue with Spatial AI > Setup > Configure Android XR; see the SDK installation documentation for scene setup.");
            Stop();
        }

        private static void Fail(Exception error)
        {
            if (!string.IsNullOrEmpty(releaseId))
            {
                state = state ?? new InstallState();
                state.releaseId = releaseId;
                state.phase = "Failed";
                state.error = error.Message;
                try { SaveState(); } catch (Exception saveError) { Debug.LogWarning("SpatialAIInstaller: cannot save state: " + saveError.Message); }
            }
            Debug.LogError("SpatialAIInstaller: " + error.Message + "\nAfter correcting the issue, use Spatial AI > SDK Installation > Retry Installation.");
            listRequest = null;
            installRequest = null;
            Stop();
        }

        private static void SaveState()
        {
            string temporary = StatePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(state, true) + "\n", new UTF8Encoding(false));
            if (File.Exists(StatePath)) File.Replace(temporary, StatePath, null);
            else File.Move(temporary, StatePath);
        }

        private static string HashFile(string path)
        {
            using (var input = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        private static void ShowProgress(string message, float progress)
        {
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("Spatial AI SDK installation", message, progress);
        }
        private static void ClearProgress() { if (!Application.isBatchMode) EditorUtility.ClearProgressBar(); }
        private static void Stop() { EditorApplication.update -= Tick; ClearProgress(); }

        [Serializable] private sealed class InstallPlan { public string version; public PackageEntry[] packages; }
        [Serializable] private sealed class PackageEntry { public string name, version, file, sha256; }
        [Serializable] private sealed class InstallState { public string releaseId, phase, error; public int attempts; }
    }
}
