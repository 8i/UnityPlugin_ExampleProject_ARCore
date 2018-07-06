#ifndef _UNITY_INTERFACE_H_
#define _UNITY_INTERFACE_H_

#include "Export.h"

#include <string>
// --------------------------------------------------------------------------------------------------------------------------------------------------
// Unity specific functions
// --------------------------------------------------------------------------------------------------------------------------------------------------
API_INTERFACE void	RegisterUnityPlugin();
API_INTERFACE void*	GetUnityRenderDevice();

typedef struct FrameContext FrameContext;
#ifdef _WIN32
typedef void(__stdcall *GLEvent)();
typedef void(__stdcall *GLEventI)(int);
typedef void(__stdcall *GLEventIII)(int, int, int);

typedef void(__stdcall *GLEventWithFrameContextI)(int, const FrameContext*);
typedef void(__stdcall *GLEventWithFrameContextIII)(int, int, int, const FrameContext*);
#else
typedef void(*GLEvent)();
typedef void(*GLEventI)(int);
typedef void(*GLEventIII)(int, int, int);

typedef void(*GLEventWithFrameContextI)(int, const FrameContext*);
typedef void(*GLEventWithFrameContextIII)(int, int, int, const FrameContext*);
#endif

typedef void(*UnityEventFunction)(int);
API_INTERFACE void UnityRenderEvent(int eventID);
API_INTERFACE UnityEventFunction UnityRenderEventFunc();

// --------------------------------------------------------------------------------------------------------------------------------------------------
// Rendering queue
// --------------------------------------------------------------------------------------------------------------------------------------------------

API_INTERFACE int QueueGLEvent(GLEvent func);
API_INTERFACE int QueueGLEventI(GLEventI func, int a0);
API_INTERFACE int QueueGLEventIII(GLEventIII func, int a0, int a1, int a2);
API_INTERFACE int QueueGLEventPrepFrameContextI(GLEventWithFrameContextI func, int a0, void* unityColorRenderBuffer, void* unityDepthRenderBuffer);
API_INTERFACE int QueueGLEventPrepFrameContextIII(GLEventWithFrameContextIII func, int a0, int a1, int a2, void* unityColorRenderBuffer, void* unityDepthRenderBuffer);
// --------------------------------------------------------------------------------------------------------------------------------------------------
// Locks
// --------------------------------------------------------------------------------------------------------------------------------------------------

API_INTERFACE void Lock();
API_INTERFACE void Unlock();

// --------------------------------------------------------------------------------------------------------------------------------------------------
// Scene Management
// --------------------------------------------------------------------------------------------------------------------------------------------------

API_INTERFACE void Scene_Objects_Add(int handle, const char* name, char* type);
API_INTERFACE void Scene_Objects_Remove(int handle);

API_INTERFACE int Scene_Objects_GetHandleAtIndex(int index);
API_INTERFACE int Scene_Objects_GetHandleFromName(const char* name);
API_INTERFACE int Scene_Objects_GetCount();

API_INTERFACE bool Scene_Objects_GetNameFromHandle(int handle, char* value, int valueSize);
API_INTERFACE bool Scene_Objects_GetTypeFromHandle(int handle, char* value, int valueSize);

// --------------------------------------------------------------------------------------------------------------------------------------------------
// Interface Communication
// --------------------------------------------------------------------------------------------------------------------------------------------------

API_INTERFACE void SetFunc_InterfaceShutdown(GLEvent func);

// --------------------------------------------------------------------------------------------------------------------------------------------------
// Key Value Map
// --------------------------------------------------------------------------------------------------------------------------------------------------

API_INTERFACE void Map_Add(const char* key, int value);
API_INTERFACE void Map_Remove(const char* key);
API_INTERFACE bool Map_Contains(const char* key);
API_INTERFACE int  Map_GetValue(const char* key);
API_INTERFACE void Map_SetValue(const char* key, int value);

// --------------------------------------------------------------------------------------------------------------------------------------------------
// Log Buffer
// --------------------------------------------------------------------------------------------------------------------------------------------------

struct LogMessage_Return;

API_INTERFACE void LogBuffer_Add(int type, const char* message);
API_INTERFACE LogMessage_Return* LogBuffer_Get();
API_INTERFACE void LogBuffer_Pop();

#endif // _UNITY_INTERFACE_H_R
