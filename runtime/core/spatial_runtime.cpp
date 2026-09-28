#include "spatial_runtime.h"
#include <new>
#include <cstring>

// The device/Cardboard adapters will be owned here. The foundation does not
// advertise a hardware session until a real adapter can start successfully.
struct spatial_runtime { bool running = false; };
uint32_t spatial_get_abi_version(void) { return SPATIAL_ABI_VERSION; }
spatial_result spatial_create(uint32_t abi_version, spatial_runtime** output) {
    if (!output) return SPATIAL_INVALID_ARGUMENT;
    *output = nullptr;
    if (abi_version != SPATIAL_ABI_VERSION) return SPATIAL_ABI_MISMATCH;
    *output = new (std::nothrow) spatial_runtime();
    return *output ? SPATIAL_OK : SPATIAL_INTERNAL_ERROR;
}
spatial_result spatial_start(spatial_runtime* runtime) {
    if (!runtime) return SPATIAL_INVALID_ARGUMENT;
    return SPATIAL_UNSUPPORTED;
}
spatial_result spatial_stop(spatial_runtime* runtime) {
    if (!runtime) return SPATIAL_INVALID_ARGUMENT;
    runtime->running = false;
    return SPATIAL_OK;
}
spatial_result spatial_get_capabilities(spatial_runtime* runtime, uint32_t* caps) {
    if (!caps) return SPATIAL_INVALID_ARGUMENT;
    *caps = 0;
    return runtime ? SPATIAL_OK : SPATIAL_INVALID_ARGUMENT;
}
spatial_result spatial_get_head_pose(spatial_runtime* runtime, spatial_pose* pose) {
    if (!pose) return SPATIAL_INVALID_ARGUMENT;
    std::memset(pose, 0, sizeof(*pose));
    pose->orientation_xyzw[3] = 1.f;
    if (!runtime) return SPATIAL_INVALID_ARGUMENT;
    return SPATIAL_NOT_RUNNING;
}
void spatial_destroy(spatial_runtime* runtime) { delete runtime; }
