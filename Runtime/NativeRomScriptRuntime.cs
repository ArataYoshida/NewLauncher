using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NewLauncher.Runtime;

internal sealed unsafe class NativeRomScriptRuntime : IRomScriptRuntime
{
    private static NativeRomScriptRuntime? s_current;

    private IntPtr _libraryHandle;
    private IntPtr _initializePointer;
    private IntPtr _tickPointer;
    private IntPtr _shutdownPointer;
    private IntPtr _createPointer;
    private IntPtr _destroyPointer;
    private IntPtr _destroyByNamePointer;
    private NativeInitializeDelegate? _initialize;
    private NativeShutdownDelegate? _shutdown;
    private NativeSetSceneBridgeDelegate? _setSceneBridge;
    private NativeSetLocalizationAssetsRootDelegate? _setLocalizationAssetsRoot;
    private NativeClearLoadedBehaviorDataDelegate? _clearLoadedBehaviorData;
    private NativeSetBehaviorDefinitionDelegate? _setBehaviorDefinition;
    private NativeSetBehaviorPropertyDelegate? _setBehaviorProperty;
    private Action<string>? _loadScene;
    private Action? _unloadScene;
    private bool _initialized;
    private bool _disposed;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool NativeInitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeShutdownDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeSetSceneBridgeDelegate(IntPtr loadScene, IntPtr unloadScene);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeSetLocalizationAssetsRootDelegate(IntPtr assetsRoot);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeClearLoadedBehaviorDataDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeSetBehaviorDefinitionDelegate(ulong entityId, IntPtr className, byte enabled);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeSetBehaviorPropertyDelegate(ulong entityId, IntPtr className, IntPtr memberName, IntPtr serializedValue);

    public void Load(RomManifest manifest)
    {
        string nativeLibraryPath = manifest.ResolveNativeScriptLibrary();
        if (!File.Exists(nativeLibraryPath))
        {
            throw new FileNotFoundException("NativeAOT script library was not found.", nativeLibraryPath);
        }

        _libraryHandle = NativeLibrary.Load(nativeLibraryPath);
        _initializePointer = GetRequiredExport("ScriptEngine_Initialize");
        _tickPointer = GetRequiredExport("ScriptEngine_Tick");
        _shutdownPointer = GetRequiredExport("ScriptEngine_Shutdown");
        _createPointer = GetRequiredExport("Behavior_Create");
        _destroyPointer = GetRequiredExport("Behavior_Destroy");
        _destroyByNamePointer = GetRequiredExport("Behavior_DestroyByName");
        _initialize = Marshal.GetDelegateForFunctionPointer<NativeInitializeDelegate>(_initializePointer);
        _shutdown = Marshal.GetDelegateForFunctionPointer<NativeShutdownDelegate>(_shutdownPointer);
        _setSceneBridge = GetOptionalDelegate<NativeSetSceneBridgeDelegate>("NewRom_SetSceneBridge");
        _setLocalizationAssetsRoot = GetOptionalDelegate<NativeSetLocalizationAssetsRootDelegate>("NewRom_SetLocalizationAssetsRoot");
        _clearLoadedBehaviorData = GetOptionalDelegate<NativeClearLoadedBehaviorDataDelegate>("NewRom_ClearLoadedBehaviorData");
        _setBehaviorDefinition = GetOptionalDelegate<NativeSetBehaviorDefinitionDelegate>("NewRom_SetBehaviorDefinition");
        _setBehaviorProperty = GetOptionalDelegate<NativeSetBehaviorPropertyDelegate>("NewRom_SetBehaviorProperty");
        s_current = this;
    }

    public void ConfigureLocalization(string assetsRoot)
    {
        if (_setLocalizationAssetsRoot == null)
        {
            return;
        }

        using NativeUtf8String nativeAssetsRoot = new(assetsRoot);
        _setLocalizationAssetsRoot(nativeAssetsRoot.Pointer);
    }

    public void ConfigureSceneManager(Action<string> loadScene, Action unloadScene)
    {
        _loadScene = loadScene;
        _unloadScene = unloadScene;
        if (_setSceneBridge == null)
        {
            return;
        }

        IntPtr loadScenePointer = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&OnNativeLoadScene;
        IntPtr unloadScenePointer = (IntPtr)(delegate* unmanaged[Cdecl]<void>)&OnNativeUnloadScene;
        _setSceneBridge(loadScenePointer, unloadScenePointer);
        EnsureInitialized();
    }

    public void AttachToEngine()
    {
        EngineInterop.Engine_SetScriptCallbackPointersWithDestroyByName(
            _initializePointer,
            _tickPointer,
            _shutdownPointer,
            _createPointer,
            _destroyPointer,
            _destroyByNamePointer);
        EnsureInitialized();
    }

    public void SetLoadedBehaviors(ulong entityId, List<BehaviorData> behaviors)
    {
        if (_setBehaviorDefinition == null || _setBehaviorProperty == null)
        {
            return;
        }

        foreach (BehaviorData behavior in behaviors)
        {
            if (string.IsNullOrWhiteSpace(behavior.Name))
            {
                continue;
            }

            using NativeUtf8String className = new(behavior.Name);
            _setBehaviorDefinition(entityId, className.Pointer, behavior.Enabled ? (byte)1 : (byte)0);
            foreach ((string memberName, string serializedValue) in behavior.Properties)
            {
                using NativeUtf8String nativeMemberName = new(memberName);
                using NativeUtf8String nativeSerializedValue = new(serializedValue);
                _setBehaviorProperty(entityId, className.Pointer, nativeMemberName.Pointer, nativeSerializedValue.Pointer);
            }
        }
    }

    public void ClearLoadedBehaviors()
    {
        _clearLoadedBehaviorData?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (ReferenceEquals(s_current, this))
        {
            s_current = null;
        }

        if (_initialized && _shutdown != null)
        {
            try
            {
                _shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            _initialized = false;
        }

        if (_libraryHandle != IntPtr.Zero)
        {
            // NativeAOT DLL の明示アンロードは未サポートのため、ROM プロセス終了まで OS に保持させる。
            _libraryHandle = IntPtr.Zero;
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        if (_initialize == null)
        {
            throw new InvalidOperationException("NativeAOT script runtime is not loaded.");
        }

        if (!_initialize())
        {
            throw new InvalidOperationException("NativeAOT script runtime initialization failed.");
        }

        _initialized = true;
    }

    private IntPtr GetRequiredExport(string exportName)
    {
        if (NativeLibrary.TryGetExport(_libraryHandle, exportName, out IntPtr address))
        {
            return address;
        }

        throw new EntryPointNotFoundException($"NativeAOT script export '{exportName}' was not found.");
    }

    private TDelegate? GetOptionalDelegate<TDelegate>(string exportName)
        where TDelegate : Delegate
    {
        return NativeLibrary.TryGetExport(_libraryHandle, exportName, out IntPtr address)
            ? Marshal.GetDelegateForFunctionPointer<TDelegate>(address)
            : null;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnNativeLoadScene(IntPtr scenePathPointer)
    {
        try
        {
            string scenePath = Marshal.PtrToStringUTF8(scenePathPointer) ?? string.Empty;
            s_current?._loadScene?.Invoke(scenePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnNativeUnloadScene()
    {
        try
        {
            s_current?._unloadScene?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private readonly struct NativeUtf8String : IDisposable
    {
        public NativeUtf8String(string value)
        {
            Pointer = Marshal.StringToCoTaskMemUTF8(value);
        }

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(Pointer);
            }
        }
    }
}
