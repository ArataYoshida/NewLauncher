namespace NewLauncher.Runtime;

internal interface IRomScriptRuntime : IDisposable
{
    void Load(RomManifest manifest);

    void ConfigureLocalization(string assetsRoot);

    void ConfigureSceneManager(Action<string> loadScene, Action unloadScene);

    void AttachToEngine();

    void SetLoadedBehaviors(ulong entityId, List<BehaviorData> behaviors);

    void ClearLoadedBehaviors();
}
