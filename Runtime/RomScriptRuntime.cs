using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace NewLauncher.Runtime;

internal sealed class RomScriptRuntime : IRomScriptRuntime
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Type> _behaviorTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, List<object>> _activeBehaviors = new();
    private readonly HashSet<object> _startedBehaviors = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ulong, List<BehaviorData>> _loadedBehaviorData = new();
    private AssemblyLoadContext? _assemblyLoadContext;
    private Assembly? _scriptAssembly;
    private Type? _sceneManagerTypeWithHooks;
    private Type? _localizationType;

    private readonly EngineInterop.ScriptInitDelegate _initDelegate;
    private readonly EngineInterop.ScriptTickDelegate _tickDelegate;
    private readonly EngineInterop.ScriptShutdownDelegate _shutdownDelegate;
    private readonly EngineInterop.ScriptCreateDelegate _createDelegate;
    private readonly EngineInterop.ScriptDestroyDelegate _destroyDelegate;
    private readonly EngineInterop.ScriptDestroyByNameDelegate _destroyByNameDelegate;

    public RomScriptRuntime()
    {
        _initDelegate = OnScriptInit;
        _tickDelegate = OnScriptTick;
        _shutdownDelegate = OnScriptShutdown;
        _createDelegate = OnScriptCreate;
        _destroyDelegate = OnScriptDestroy;
        _destroyByNameDelegate = OnScriptDestroyByName;
    }

    public void LoadApplicationAssembly(string applicationAssemblyPath)
    {
        if (!File.Exists(applicationAssemblyPath))
        {
            return;
        }

        var context = new ScriptAssemblyLoadContext(applicationAssemblyPath);
        Assembly assembly = context.LoadFromAssemblyPath(applicationAssemblyPath);
        _assemblyLoadContext = context;
        _scriptAssembly = assembly;
        RebuildBehaviorTypeCache(assembly);
        ConfigureRuntimeLogSink();
    }

    public void Load(RomManifest manifest)
    {
        LoadApplicationAssembly(manifest.ResolveApplicationAssembly());
    }

    public void ConfigureLocalization(string assetsRoot)
    {
        Type? localizationType = ResolveScriptCoreType("New.Localization")
            ?? ResolveScriptCoreType("New.Messages");
        if (localizationType == null)
        {
            return;
        }

        _localizationType = localizationType;
        MethodInfo? setAssetsRootMethod = localizationType.GetMethod(
            "SetAssetsRoot",
            BindingFlags.Public | BindingFlags.Static);
        setAssetsRootMethod?.Invoke(null, new object?[] { assetsRoot });

        MethodInfo? loadAllMethod = localizationType.GetMethod(
            "LoadAllMessageAssets",
            BindingFlags.Public | BindingFlags.Static);
        loadAllMethod?.Invoke(null, null);
    }

    public void ConfigureSceneManager(Action<string> loadScene, Action unloadScene)
    {
        Type? sceneManagerType = ResolveScriptCoreType("New.SceneManager");
        MethodInfo? setRuntimeHooksMethod = sceneManagerType?.GetMethod(
            "SetRuntimeHooks",
            BindingFlags.Public | BindingFlags.Static);
        if (setRuntimeHooksMethod == null)
        {
            return;
        }

        _sceneManagerTypeWithHooks = sceneManagerType;
        setRuntimeHooksMethod.Invoke(
            null,
            new object?[] { loadScene, unloadScene, new Func<object[]>(GetActiveBehaviorObjects) });
    }

    public void AttachToEngine()
    {
        EngineInterop.Engine_SetScriptCallbacksWithDestroyByName(
            _initDelegate,
            _tickDelegate,
            _shutdownDelegate,
            _createDelegate,
            _destroyDelegate,
            _destroyByNameDelegate);
    }

    public void SetLoadedBehaviors(ulong entityId, List<BehaviorData> behaviors)
    {
        if (behaviors.Count == 0)
        {
            return;
        }

        _loadedBehaviorData[entityId] = behaviors;
    }

    public void ClearLoadedBehaviors()
    {
        lock (_lock)
        {
            _loadedBehaviorData.Clear();
        }
    }

    private bool OnScriptInit() => true;

    private void OnScriptTick(float deltaTime)
    {
        lock (_lock)
        {
            foreach (object behavior in _activeBehaviors.Values.SelectMany(list => list).ToArray())
            {
                if (!IsBehaviorEnabled(behavior))
                {
                    continue;
                }

                if (_startedBehaviors.Add(behavior))
                {
                    InvokeBehaviorMethod(behavior, "Start");
                }

                if (IsBehaviorEnabled(behavior))
                {
                    InvokeBehaviorMethod(behavior, "Update", deltaTime);
                }
            }
        }
    }

    private void OnScriptShutdown()
    {
        lock (_lock)
        {
            foreach (object behavior in _activeBehaviors.Values.SelectMany(list => list).ToArray())
            {
                EndBehavior(behavior);
            }

            _activeBehaviors.Clear();
            _loadedBehaviorData.Clear();
            _behaviorTypes.Clear();
            ClearSceneManagerHooks();
            _localizationType = null;
            _scriptAssembly = null;
            _assemblyLoadContext?.Unload();
            _assemblyLoadContext = null;
        }
    }

    public void Dispose()
    {
        OnScriptShutdown();
    }

    private void OnScriptCreate(ulong entityId, string className)
    {
        CreateBehaviorInstance(entityId, className);
    }

    private void OnScriptDestroy(ulong entityId)
    {
        lock (_lock)
        {
            if (!_activeBehaviors.Remove(entityId, out List<object>? behaviors))
            {
                return;
            }

            foreach (object behavior in behaviors.ToArray())
            {
                EndBehavior(behavior);
            }

            _loadedBehaviorData.Remove(entityId);
        }
    }

    private void OnScriptDestroyByName(ulong entityId, string className)
    {
        lock (_lock)
        {
            if (!_activeBehaviors.TryGetValue(entityId, out List<object>? behaviors))
            {
                return;
            }

            object? behavior = behaviors.FirstOrDefault(instance => instance.GetType().Name == className);
            if (behavior == null)
            {
                return;
            }

            EndBehavior(behavior);
            behaviors.Remove(behavior);
            RemoveLoadedBehavior(entityId, className);
            if (behaviors.Count == 0)
            {
                _activeBehaviors.Remove(entityId);
            }
        }
    }

    private bool CreateBehaviorInstance(ulong entityId, string className)
    {
        lock (_lock)
        {
            if (_scriptAssembly == null)
            {
                return false;
            }

            if (!_behaviorTypes.TryGetValue(className, out Type? type))
            {
                type = _scriptAssembly.GetTypes().FirstOrDefault(candidate => candidate.Name == className);
            }

            if (type == null)
            {
                return false;
            }

            if (_activeBehaviors.TryGetValue(entityId, out List<object>? existing) &&
                existing.Any(instance => instance.GetType().Name == className))
            {
                return true;
            }

            object? instance = CreateAttachedBehaviorInstance(type);
            if (instance == null)
            {
                return false;
            }

            type.GetProperty("EntityId", BindingFlags.Public | BindingFlags.Instance)?.SetValue(instance, entityId);
            RestoreBehaviorProperties(entityId, className, type, instance);

            if (!_activeBehaviors.TryGetValue(entityId, out List<object>? behaviors))
            {
                behaviors = new List<object>();
                _activeBehaviors[entityId] = behaviors;
            }

            behaviors.Add(instance);
            InvokeBehaviorMethod(instance, "Awake");
            return true;
        }
    }

    private static object? CreateAttachedBehaviorInstance(Type behaviorType)
    {
        using IDisposable? constructionScope = BeginComponentAttachmentConstruction(behaviorType);
        return Activator.CreateInstance(behaviorType);
    }

    private static IDisposable? BeginComponentAttachmentConstruction(Type behaviorType)
    {
        Type? componentType = ResolveNewBaseType(behaviorType, "New.Component")
            ?? ResolveNewBaseType(behaviorType, "New.Behavior");
        MethodInfo? enterMethod = componentType?.GetMethod(
            "EnterAttachmentConstruction",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Type) },
            modifiers: null);
        if (enterMethod != null)
        {
            return enterMethod.Invoke(null, new object[] { behaviorType }) as IDisposable;
        }

        enterMethod = componentType?.GetMethod(
            "EnterAttachmentConstruction",
            BindingFlags.Static | BindingFlags.NonPublic);

        return enterMethod?.Invoke(null, null) as IDisposable;
    }

    private static Type? ResolveNewBaseType(Type type, string fullName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            Type? resolved = current.Assembly.GetType(fullName, throwOnError: false);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    private void RestoreBehaviorProperties(ulong entityId, string className, Type type, object instance)
    {
        if (!_loadedBehaviorData.TryGetValue(entityId, out List<BehaviorData>? behaviors))
        {
            return;
        }

        BehaviorData? behaviorData = behaviors.FirstOrDefault(behavior => behavior.Name == className);
        if (behaviorData == null)
        {
            return;
        }

        if (!behaviorData.Enabled)
        {
            PropertyInfo? enabledProperty = type.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabledProperty != null && enabledProperty.CanWrite)
            {
                enabledProperty.SetValue(instance, false);
            }
        }

        foreach ((string memberName, string serializedValue) in behaviorData.Properties)
        {
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                AssignScriptMemberValue(property.PropertyType, serializedValue, value => property.SetValue(instance, value));
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                AssignScriptMemberValue(field.FieldType, serializedValue, value => field.SetValue(instance, value));
            }
        }
    }

    private void RebuildBehaviorTypeCache(Assembly assembly)
    {
        _behaviorTypes.Clear();
        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            if (type.GetProperty("EntityId", BindingFlags.Public | BindingFlags.Instance) == null ||
                type.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance) == null)
            {
                continue;
            }

            _behaviorTypes[type.Name] = type;
        }
    }

    private object[] GetActiveBehaviorObjects()
    {
        lock (_lock)
        {
            return _activeBehaviors.Values.SelectMany(behaviors => behaviors).ToArray();
        }
    }

    private Type? ResolveScriptCoreType(string fullName)
    {
        Type? type = _behaviorTypes.Values
            .Select(behaviorType => behaviorType.BaseType?.Assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(candidate => candidate != null);

        type ??= _assemblyLoadContext?.Assemblies
            .Select(assembly => assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(candidate => candidate != null);

        return type;
    }

    private void ClearSceneManagerHooks()
    {
        MethodInfo? setRuntimeHooksMethod = _sceneManagerTypeWithHooks?.GetMethod(
            "SetRuntimeHooks",
            BindingFlags.Public | BindingFlags.Static);
        setRuntimeHooksMethod?.Invoke(null, new object?[] { null, null, null });
        _sceneManagerTypeWithHooks = null;
    }

    private void RemoveLoadedBehavior(ulong entityId, string className)
    {
        if (!_loadedBehaviorData.TryGetValue(entityId, out List<BehaviorData>? behaviors))
        {
            return;
        }

        behaviors.RemoveAll(behavior => string.Equals(behavior.Name, className, StringComparison.Ordinal));
        if (behaviors.Count == 0)
        {
            _loadedBehaviorData.Remove(entityId);
        }
    }

    private void ConfigureRuntimeLogSink()
    {
        Type? runtimeLogType = _scriptAssembly?.GetTypes()
            .Select(type => type.BaseType?.Assembly.GetType("New.Log", throwOnError: false))
            .FirstOrDefault(type => type != null);
        runtimeLogType ??= _scriptAssembly?.GetTypes()
            .Select(type => type.BaseType?.Assembly.GetType("New.RuntimeLog", throwOnError: false))
            .FirstOrDefault(type => type != null);
        runtimeLogType ??= _scriptAssembly?.GetReferencedAssemblies()
            .Select(name => _assemblyLoadContext?.Assemblies.FirstOrDefault(assembly => assembly.GetName().Name == name.Name))
            .Where(assembly => assembly != null)
            .Select(assembly => assembly!.GetType("New.Log", throwOnError: false))
            .FirstOrDefault(type => type != null);
        runtimeLogType ??= _scriptAssembly?.GetReferencedAssemblies()
            .Select(name => _assemblyLoadContext?.Assemblies.FirstOrDefault(assembly => assembly.GetName().Name == name.Name))
            .Where(assembly => assembly != null)
            .Select(assembly => assembly!.GetType("New.RuntimeLog", throwOnError: false))
            .FirstOrDefault(type => type != null);

        MethodInfo? setSinkMethod = runtimeLogType?.GetMethod("SetSink", BindingFlags.Public | BindingFlags.Static);
        setSinkMethod?.Invoke(null, new object?[] { new Action<string>(message => System.Diagnostics.Debug.WriteLine(message)) });
    }

    private static bool IsBehaviorEnabled(object behavior)
    {
        PropertyInfo? enabledProperty = behavior.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
        return enabledProperty == null || (bool)(enabledProperty.GetValue(behavior) ?? true);
    }

    private static void InvokeBehaviorMethod(object behavior, string methodName, params object[] parameters)
    {
        MethodInfo? method = behavior.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method?.Invoke(behavior, parameters.Length == 0 ? null : parameters);
    }

    private void EndBehavior(object behavior)
    {
        InvokeBehaviorMethod(behavior, "End");
        _startedBehaviors.Remove(behavior);
    }

    private static void AssignScriptMemberValue(Type valueType, string serializedValue, Action<object?> assign)
    {
        if (TryConvertScriptMemberValue(valueType, serializedValue, out object? convertedValue))
        {
            assign(convertedValue);
        }
    }

    private static bool TryConvertScriptMemberValue(Type memberType, string text, out object? convertedValue)
    {
        convertedValue = null;
        Type? nullableType = Nullable.GetUnderlyingType(memberType);
        Type valueType = nullableType ?? memberType;
        if (nullableType != null && string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string trimmed = text.Trim();
        if (valueType == typeof(string))
        {
            convertedValue = text;
            return true;
        }

        if (valueType.IsEnum)
        {
            if (Enum.TryParse(valueType, trimmed, ignoreCase: true, out object? enumValue))
            {
                convertedValue = enumValue;
                return true;
            }

            return false;
        }

        if (valueType == typeof(bool))
        {
            if (bool.TryParse(trimmed, out bool boolValue))
            {
                convertedValue = boolValue;
                return true;
            }

            if (trimmed is "1")
            {
                convertedValue = true;
                return true;
            }

            if (trimmed is "0")
            {
                convertedValue = false;
                return true;
            }
        }

        return TryConvertNumericScriptMemberValue(valueType, trimmed, out convertedValue);
    }

    private static bool TryConvertNumericScriptMemberValue(Type valueType, string text, out object? convertedValue)
    {
        convertedValue = null;
        CultureInfo[] cultures = { CultureInfo.InvariantCulture, CultureInfo.CurrentCulture };
        foreach (CultureInfo culture in cultures)
        {
            if (valueType == typeof(float) && float.TryParse(text, NumberStyles.Float, culture, out float floatValue))
            {
                convertedValue = floatValue;
                return true;
            }

            if (valueType == typeof(double) && double.TryParse(text, NumberStyles.Float, culture, out double doubleValue))
            {
                convertedValue = doubleValue;
                return true;
            }

            if (valueType == typeof(int) && int.TryParse(text, NumberStyles.Integer, culture, out int intValue))
            {
                convertedValue = intValue;
                return true;
            }

            if (valueType == typeof(uint) && uint.TryParse(text, NumberStyles.Integer, culture, out uint uintValue))
            {
                convertedValue = uintValue;
                return true;
            }

            if (valueType == typeof(long) && long.TryParse(text, NumberStyles.Integer, culture, out long longValue))
            {
                convertedValue = longValue;
                return true;
            }

            if (valueType == typeof(ulong) && ulong.TryParse(text, NumberStyles.Integer, culture, out ulong ulongValue))
            {
                convertedValue = ulongValue;
                return true;
            }
        }

        return false;
    }

    private sealed class ScriptAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _mainAssemblyDirectory;

        public ScriptAssemblyLoadContext(string mainAssemblyPath)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
            _mainAssemblyDirectory = Path.GetDirectoryName(mainAssemblyPath) ?? AppContext.BaseDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "NewScriptCore")
            {
                foreach (string candidate in new[]
                {
                    Path.Combine(_mainAssemblyDirectory, "NewScriptCore.Managed.dll"),
                    Path.Combine(AppContext.BaseDirectory, "NewScriptCore.Managed.dll")
                })
                {
                    if (File.Exists(candidate))
                    {
                        return LoadFromAssemblyPath(candidate);
                    }
                }
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath);
        }
    }
}
