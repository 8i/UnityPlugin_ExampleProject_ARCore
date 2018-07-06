#ifndef _HVR_PLAYER_INTERFACE_H_
#define _HVR_PLAYER_INTERFACE_H_

#include "Export.h"
#include "Types.h"
#include <stdbool.h>
#include <string.h>

//-----------------------------------------------------------------------------
// HVR Player Interface

API_INTERFACE bool	Interface_Initialise(const InterfaceInitialiseInfo* info);
API_INTERFACE void	Interface_Shutdown();
API_INTERFACE bool	Interface_IsInitialised();
API_INTERFACE void	Interface_Update();
API_INTERFACE void	Interface_Reconnect();

const int INTERFACE_LOG_TYPE_ALL = 0;
const int INTERFACE_LOG_TYPE_DEBUG = 0;
const int INTERFACE_LOG_TYPE_INFO = 1;
const int INTERFACE_LOG_TYPE_WARNING = 2;
const int INTERFACE_LOG_TYPE_ERROR = 3;
const int INTERFACE_LOG_TYPE_NONE = 4;

// System values that can be queried
//"VERSION_MAJOR", "VERSION_MINOR", "VERSION_REVISION"
//"VERSION_CHANGES", "VERSION_EDIT", "VERSION"
//"BUILD_PLATFORM", "BUILD_CONFIG", "BUILD_NUMBER", 
//"BUILD_HOST", "BUILD_DATE", "BUILD_INFO"
//"GIT_BRANCH", "GIT_HASH", "GIT_MODIFIED", "GIT_INFO"
API_INTERFACE bool	Interface_GetInfo(const char* key, char* value, int valueSize);
API_INTERFACE bool	Interface_PopLogEntry(int* logLevel, char* value, int valueSize);
API_INTERFACE void	Interface_SetLogCallback(LogCallback callback);
API_INTERFACE void	Interface_SetLogLevel(int logLevel);
API_INTERFACE MemoryStats Interface_GetMemoryStats();
API_INTERFACE NetworkStats Interface_GetNetworkStats();

API_INTERFACE size_t Interface_GetRenderMethodTypeCount();
API_INTERFACE bool	Interface_GetRenderMethodType(size_t idx, char* value, int valueSize);
API_INTERFACE bool	Interface_GetRenderMethodDefault(char* value, int valueSize);

//-----------------------------------------------------------------------------
// Player Functions

const int RENDERER_TYPE_OPENGL = 0;
const int RENDERER_TYPE_DIRECT3D11 = 1;
const int RENDERER_TYPE_GNMX = 2;
const int RENDERER_TYPE_METAL = 3;

API_INTERFACE HVRID Player_Create(int rendererType, void* device);
API_INTERFACE void	Player_Delete(HVRID player);
API_INTERFACE bool	Player_IsValid(HVRID player);

// Delete all Render data
API_INTERFACE void Player_Detach(HVRID player);

// Specifies an actor / viewport pair will be rendered this frame.
API_INTERFACE void	Player_WillRender(HVRID player, HVRID actor, HVRID viewport);

// Calculates LOD information from all WillRender calls, Prepares for Render calls and Decodes frames. 
// All WillRender calls must be completed first.
API_INTERFACE void	Player_PrepareRender(HVRID player, const FrameContext* frameContext);

// Renders an actor / viewport pair. Must be called after WillRender and PrepareRender.
API_INTERFACE void	Player_Render(HVRID player, HVRID actor, HVRID viewport, const FrameContext* frameContext);

//-----------------------------------------------------------------------------
// Actor Functions

API_INTERFACE HVRID Actor_Create();
API_INTERFACE void	Actor_Delete(HVRID actor);
API_INTERFACE bool	Actor_IsValid(HVRID actor);

API_INTERFACE void	Actor_SetAsset(HVRID actor, HVRID asset);
API_INTERFACE void	Actor_SetRenderMethod(HVRID actor, HVRID renderMethod);
API_INTERFACE void	Actor_SetTransform(HVRID actor, const Mat44* transform);

API_INTERFACE void	Actor_SetSubroutineUniformInt(HVRID actor, const char* uniformName, int value);
API_INTERFACE void	Actor_SetSubroutineUniformFloat(HVRID actor, const char* uniformName, float value);
API_INTERFACE void	Actor_SetSubroutineUniformVec2(HVRID actor, const char* uniformName, const Vec2* value);
API_INTERFACE void	Actor_SetSubroutineUniformVec3(HVRID actor, const char* uniformName, const Vec3* value);
API_INTERFACE void	Actor_SetSubroutineUniformVec4(HVRID actor, const char* uniformName, const Vec4* value);
API_INTERFACE void	Actor_SetSubroutineUniformMat3x3(HVRID actor, const char* uniformName, const Mat33* value);
API_INTERFACE void	Actor_SetSubroutineUniformMat4x4(HVRID actor, const char* uniformName, const Mat44* value);
// The textureNativeHandle argument should be either the GLuint texture ID for OpenGL, or the ID3D11Resource* for D3D11.
API_INTERFACE void	Actor_SetSubroutineUniformTexture2D(HVRID actor, const char* uniformName, HVRID player, const void* textureNativeHandle);

//-----------------------------------------------------------------------------
// Asset Functions

API_INTERFACE HVRID Asset_Create(const char* fileFolder);
API_INTERFACE HVRID Asset_CreateFromInfo(const AssetCreationInfo* info);
API_INTERFACE void	Asset_Delete(HVRID asset);
API_INTERFACE bool	Asset_IsValid(HVRID asset);

API_INTERFACE void	Asset_Update(HVRID asset, float absoluteTime);

API_INTERFACE void	Asset_Play(HVRID asset);
API_INTERFACE void	Asset_Pause(HVRID asset);
API_INTERFACE void	Asset_Seek(HVRID asset, float time);
API_INTERFACE void	Asset_Step(HVRID asset, int frames);
API_INTERFACE void	Asset_SetLooping(HVRID asset, bool looping);

const int ASSET_STATE_INITIALISING	= 1 << 0;
const int ASSET_STATE_PLAYING		= 1 << 1;
const int ASSET_STATE_SEEKING		= 1 << 2;
const int ASSET_STATE_CACHING		= 1 << 3;
const int ASSET_STATE_OFFLINE		= 1 << 4;
const int ASSET_STATE_FULLY_CACHED	= 1 << 5;
const int ASSET_STATE_INVALID		= 1 << 6;
API_INTERFACE int	Asset_GetState(HVRID asset);
API_INTERFACE Bounds Asset_GetBounds(HVRID asset);
API_INTERFACE float Asset_GetBufferFillRatio(HVRID asset);
API_INTERFACE float	Asset_GetCurrentTime(HVRID asset);
API_INTERFACE float	Asset_GetDecodeTime(HVRID asset);
API_INTERFACE float	Asset_GetDuration(HVRID asset);
API_INTERFACE bool	Asset_GetFrameMeta(HVRID asset, char* json, size_t jsonSize);
API_INTERFACE bool	Asset_GetTrackMeta(HVRID asset, char* json, size_t jsonSize);
API_INTERFACE size_t Asset_GetVoxelCount(HVRID asset);

//-----------------------------------------------------------------------------
// RenderMethod Functions

API_INTERFACE HVRID RenderMethod_Create(const char* type);
API_INTERFACE void	RenderMethod_Delete(HVRID renderMethod);
API_INTERFACE bool	RenderMethod_IsValid(HVRID renderMethod);
API_INTERFACE void	RenderMethod_SetShaderSubroutines(HVRID renderMethod, const char* code);
API_INTERFACE void	RenderMethod_SetShaderSubroutinesArray(HVRID renderMethod, const char* const* strings, int nStrings);

//-----------------------------------------------------------------------------
// Viewport Functions

API_INTERFACE HVRID Viewport_Create();
API_INTERFACE void	Viewport_Delete(HVRID viewport);
API_INTERFACE bool	Viewport_IsValid(HVRID viewport);

API_INTERFACE void	Viewport_SetViewMatrix(HVRID viewport, const Mat44* viewMat44);
API_INTERFACE void	Viewport_SetProjMatrix(HVRID viewport, const Mat44* projMat44);
API_INTERFACE void	Viewport_SetDimensions(HVRID viewport, float x, float y, float width, float height);

const int COLOUR_SPACE_GAMMA = 0;
const int COLOUR_SPACE_LINEAR = 1;
API_INTERFACE void	Viewport_SetColourSpace(HVRID viewport, int colourspace);
API_INTERFACE void	Viewport_SetReverseDepthEnabled(HVRID viewport, int reverseDepthEnabled);

#endif // _HVR_PLAYER_INTERFACE_H_
