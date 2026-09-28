using System;
using System.Runtime.InteropServices;

namespace SpatialAI.Core
{
    // Integration probe for the native ABI, not a working hardware backend.
    public static class NativeRuntimeProbe
    {
        private const string Library = "spatial_runtime";
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint spatial_get_abi_version();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int spatial_create(uint abi, out IntPtr runtime);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int spatial_get_capabilities(IntPtr runtime, out uint caps);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int spatial_start(IntPtr runtime);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void spatial_destroy(IntPtr runtime);

        public static Result Check(out uint capabilityMask)
        {
            capabilityMask = 0;
            IntPtr handle = IntPtr.Zero;
            try
            {
                if (spatial_get_abi_version() != 1) return new Result(ErrorCode.Unsupported, "Native ABI mismatch.");
                if (spatial_create(1, out handle) != 0) return new Result(ErrorCode.BackendFailure, "Native runtime creation failed.");
                if (spatial_get_capabilities(handle, out capabilityMask) != 0) return new Result(ErrorCode.BackendFailure, "Native capability query failed.");
                int start = spatial_start(handle);
                if (start == 3) return new Result(ErrorCode.Unsupported, "Native ABI loaded; hardware adapter is not implemented.");
                return start == 0 ? Result.Ok : new Result(ErrorCode.BackendFailure, "Native start failed: " + start);
            }
            catch (DllNotFoundException e) { return new Result(ErrorCode.NativeLibraryMissing, e.Message); }
            catch (EntryPointNotFoundException e) { return new Result(ErrorCode.Unsupported, e.Message); }
            catch (BadImageFormatException e) { return new Result(ErrorCode.Unsupported, e.Message); }
            finally { if (handle != IntPtr.Zero) spatial_destroy(handle); }
        }
    }
}
