#ifndef SPATIAL_RUNTIME_H
#define SPATIAL_RUNTIME_H
#include <stdint.h>
#if defined(_WIN32)
# if defined(SPATIAL_RUNTIME_EXPORTS)
#  define SPATIAL_API __declspec(dllexport)
# else
#  define SPATIAL_API __declspec(dllimport)
# endif
#else
# define SPATIAL_API __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif
#define SPATIAL_ABI_VERSION 1u
typedef struct spatial_runtime spatial_runtime;
typedef int32_t spatial_result;
enum { SPATIAL_OK = 0, SPATIAL_INVALID_ARGUMENT = 1, SPATIAL_ABI_MISMATCH = 2,
       SPATIAL_UNSUPPORTED = 3, SPATIAL_NOT_RUNNING = 4, SPATIAL_INTERNAL_ERROR = 5 };
typedef struct spatial_pose {
    int64_t timestamp_ns;
    float orientation_xyzw[4];
    uint32_t is_tracked;
    uint32_t reserved;
} spatial_pose;
// Handles are owned by the caller. Calls are serialized by the host.
// No C++ objects/exceptions cross this ABI. Destroy once; null is accepted.
SPATIAL_API uint32_t spatial_get_abi_version(void);
SPATIAL_API spatial_result spatial_create(uint32_t abi_version, spatial_runtime** out_runtime);
SPATIAL_API spatial_result spatial_start(spatial_runtime* runtime);
SPATIAL_API spatial_result spatial_stop(spatial_runtime* runtime);
SPATIAL_API spatial_result spatial_get_capabilities(spatial_runtime* runtime, uint32_t* out_caps);
SPATIAL_API spatial_result spatial_get_head_pose(spatial_runtime* runtime, spatial_pose* out_pose);
SPATIAL_API void spatial_destroy(spatial_runtime* runtime);
#ifdef __cplusplus
}
#endif
#endif
