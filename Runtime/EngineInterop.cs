using System.Runtime.InteropServices;

namespace NewLauncher.Runtime;

internal static class EngineInterop
{
    private const string EngineDll = "NewEngine.dll";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ScriptInitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ScriptTickDelegate(float deltaTime);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ScriptShutdownDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ScriptCreateDelegate(ulong entityId, [MarshalAs(UnmanagedType.LPStr)] string className);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ScriptDestroyDelegate(ulong entityId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ScriptDestroyByNameDelegate(ulong entityId, [MarshalAs(UnmanagedType.LPStr)] string className);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_SetScriptCallbacksWithDestroyByName(
        ScriptInitDelegate init,
        ScriptTickDelegate tick,
        ScriptShutdownDelegate shutdown,
        ScriptCreateDelegate create,
        ScriptDestroyDelegate destroy,
        ScriptDestroyByNameDelegate destroyByName);

    [DllImport(EngineDll, EntryPoint = "Engine_SetScriptCallbacksWithDestroyByName", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_SetScriptCallbackPointersWithDestroyByName(
        IntPtr init,
        IntPtr tick,
        IntPtr shutdown,
        IntPtr create,
        IntPtr destroy,
        IntPtr destroyByName);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Engine_InitGlobal();

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void Engine_SetAssetsRoot(string assetsRoot);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Engine_RegisterViewport(IntPtr windowHandle, int width, int height, [MarshalAs(UnmanagedType.U1)] bool isGameView);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_QueueResize([MarshalAs(UnmanagedType.U1)] bool isGameView, int width, int height);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_Tick();

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_Shutdown();

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_SetPlayMode([MarshalAs(UnmanagedType.U1)] bool playing);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_SetPauseMode([MarshalAs(UnmanagedType.U1)] bool paused);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Engine_ClearScene();

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern ulong Engine_CreateGameObject(string name);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void Engine_AddComponent(ulong entityId, string className);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Transform_SetPosition(ulong entityId, float x, float y, float z);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Transform_SetRotation(ulong entityId, float x, float y, float z);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Transform_SetScale(ulong entityId, float x, float y, float z);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void MeshRenderer_SetMesh(ulong entityId, string meshName);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void MeshRenderer_SetMaterial(ulong entityId, string materialName);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Camera_SetFOV(ulong entityId, float fov);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Camera_SetOrtho(ulong entityId, [MarshalAs(UnmanagedType.U1)] bool ortho);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Camera_SetRenderOutputID(ulong entityId, int id);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Light_SetType(ulong entityId, int type);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Light_SetColor(ulong entityId, float r, float g, float b);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Light_SetIntensity(ulong entityId, float intensity);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Light_SetRange(ulong entityId, float range);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Material_SetColor(ulong entityId, float r, float g, float b, float a);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Material_SetSpecular(ulong entityId, float r, float g, float b, float shininess);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Material_SetCustomProperty(ulong entityId, int index, float x, float y, float z, float w);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void Material_SetMainTexture(ulong entityId, string name);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void Material_SetNormalTexture(ulong entityId, string name);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void Engine_LoadTexture(string path, string name);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void SkyBox_SetCubeMap(ulong entityId, string path);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SkyBox_SetIntensity(ulong entityId, float intensity);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Rigidbody_SetMass(ulong entityId, float mass);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Rigidbody_SetUseGravity(ulong entityId, [MarshalAs(UnmanagedType.U1)] bool useGravity);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Rigidbody_SetIsKinematic(ulong entityId, [MarshalAs(UnmanagedType.U1)] bool isKinematic);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Rigidbody_SetVelocity(ulong entityId, float x, float y, float z);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Rigidbody_SetDamping(ulong entityId, float damping);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void BoxCollider_SetCenter(ulong entityId, float x, float y, float z);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void BoxCollider_SetSize(ulong entityId, float x, float y, float z);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void BoxCollider_SetIsTrigger(ulong entityId, [MarshalAs(UnmanagedType.U1)] bool isTrigger);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void VoxelGrid_CreateOrResize(ulong entityId, int resolutionX,
        int resolutionY, int resolutionZ, float voxelSize, float isoLevel);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void VoxelGrid_SetMode(ulong entityId, int mode);

    [DllImport(EngineDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void VoxelGrid_SetSurfaceOptions(ulong entityId,
        float smoothing, int normalMode);
}
