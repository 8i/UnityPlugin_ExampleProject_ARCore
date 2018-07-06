#ifndef _COMMON_HEADERS_TYPES_H_
#define _COMMON_HEADERS_TYPES_H_

#include <stdint.h>
#include <stdbool.h>

typedef struct Vec2
{
	float x, y;
} Vec2;

typedef struct Vec3
{
	float x, y, z;
} Vec3;

typedef struct Vec4
{
	float x, y, z, w;
} Vec4;

typedef struct Mat33
{
	// Values should be stored in column-major order.
	float m[9];
} Mat33;

typedef struct Mat44
{
	// Values should be stored in column-major order.
	float m[16];
} Mat44;

typedef struct Bounds
{
	Vec3 center;
	Vec3 halfDims;
} Bounds;

typedef struct MemoryStats
{
	// Total allocations since initialisation
	uint64_t allocBytes;
	uint64_t allocBlocks;

	// Total frees since initialisation
	uint64_t freeBytes;
	uint64_t freeBlocks;

	// Currently used
	int64_t usedBytes;
	int64_t usedBlocks;
} MemoryStats;

typedef struct NetworkStats
{
	uint64_t receivedBits;
	uint64_t sentBits;
	uint64_t bitsPerSecond;
} NetworkStats;

typedef struct HVRAdaptationSet
{
	const char* mimeType;
	const char* codec;
} HVRAdaptationSet;

typedef struct HVRRepresentation
{
	float maxFps;
	uint32_t bandwidth;
} HVRRepresentation;

typedef struct FrameContext
{
	uint32_t structSize;
	uint32_t pixelFormat;
	void* commandQueue;
	void* commandEncoder;
} FrameContext;

#endif
