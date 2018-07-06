#ifndef _PLAYER_TYPES_H
#define _PLAYER_TYPES_H

#include <stdint.h>

#include "CommonTypes.h"
#include "ErrorCodes.h"

typedef int32_t HVRID;
const HVRID INVALID_HANDLE = 0;

typedef void(*LogCallback)(int messageType, const char* str);

typedef struct InterfaceInitialiseInfo
{
	uint32_t stuctSize;
	const char* appId;
	const char* appVersion;
	const char* apiKey;
	const char* extensionPath;
	int32_t threadPoolSize;
	LogCallback logCallback;
	int32_t logLevel;
} InterfaceInitialiseInfo;

typedef void(*OnAssetInitialised)(
	HVRError /*errorCode*/, 
	void* /*userData*/);

typedef bool(*OnAssetSelectRepresentation)(
	const HVRAdaptationSet* /*adaptationSet*/,
	uint32_t /*representationIndex*/,
	const HVRRepresentation* /*representations*/,
	uint32_t /*representationCount*/,
	void* /*userData*/);

typedef void(*OnAssetRepresentationDataRecieved)(
	const char* /*mimeType*/, 
	const char* /*codec*/, 
	float /*startTime*/, 
	uint8_t* /*data*/, 
	uint32_t /*dataSze*/, 
	void* /*userData*/
);

typedef struct AssetCreationInfo
{
	uint32_t stuctSize;
	const char* assetPath;
	const char* cacheDir;
	void* userData;
	OnAssetInitialised onInitialised;
	OnAssetSelectRepresentation onSelectRepresentation;
	OnAssetRepresentationDataRecieved onRepresentationDataRecieved;
	float bufferTime;
} AssetCreationInfo;

#endif // _TYPES_H
