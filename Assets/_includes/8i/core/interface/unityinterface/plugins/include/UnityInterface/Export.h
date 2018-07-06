#ifndef _UNITY_API_EXPORT_H_
#define _UNITY_API_EXPORT_H_

#ifdef __cplusplus
	#define EXTERN_C extern "C"
#else
	#define EXTERN_C
#endif

#if defined(_MSC_VER) || defined(__ORBIS__)

	#ifdef EXPORT_UNITY_API
		#define API_INTERFACE EXTERN_C __declspec( dllexport )
	#else
		#define API_INTERFACE EXTERN_C __declspec( dllimport )
	#endif

#elif defined(__GNUC__) || defined(COMPILER_GCC) || defined(__APPLE__)

	#ifdef EXPORT_UNITY_API
		#define API_INTERFACE EXTERN_C __attribute__((visibility("default")))
	#else
		#define API_INTERFACE EXTERN_C 
	#endif

#elif defined (EMSCRIPTEN)

	#ifdef EXPORT_PLAYER_API
		#define API_INTERFACE EXTERN_C __attribute__((used))
	#else
		#define API_INTERFACE EXTERN_C 
	#endif

#else
	#error "Unsupported Platform."
#endif

#endif // _UNITY_API_EXPORT_H_