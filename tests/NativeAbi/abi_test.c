#include "spatial_runtime.h"
#include <stdio.h>
#include <string.h>
#define CHECK(x) do { if (!(x)) { fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #x); return 1; } } while (0)
int main(void) {
    spatial_runtime* runtime = 0;
    spatial_pose pose;
    uint32_t caps = 999;
    CHECK(sizeof(spatial_pose) == 32);
    CHECK(spatial_get_abi_version() == SPATIAL_ABI_VERSION);
    CHECK(spatial_create(999, &runtime) == SPATIAL_ABI_MISMATCH && runtime == 0);
    CHECK(spatial_create(SPATIAL_ABI_VERSION, 0) == SPATIAL_INVALID_ARGUMENT);
    CHECK(spatial_create(SPATIAL_ABI_VERSION, &runtime) == SPATIAL_OK && runtime != 0);
    CHECK(spatial_get_capabilities(runtime, &caps) == SPATIAL_OK && caps == 0);
    CHECK(spatial_start(runtime) == SPATIAL_UNSUPPORTED);
    memset(&pose, 0xFF, sizeof(pose));
    CHECK(spatial_get_head_pose(runtime, &pose) == SPATIAL_NOT_RUNNING);
    CHECK(pose.is_tracked == 0 && pose.timestamp_ns == 0 && pose.orientation_xyzw[3] == 1.f);
    CHECK(spatial_stop(runtime) == SPATIAL_OK && spatial_stop(runtime) == SPATIAL_OK);
    CHECK(spatial_get_head_pose(runtime, 0) == SPATIAL_INVALID_ARGUMENT);
    spatial_destroy(runtime);
    spatial_destroy(0);
    puts("PASS: C ABI version, ownership, unsupported hardware, invalid output clearing.");
    return 0;
}
