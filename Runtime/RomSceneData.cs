namespace NewLauncher.Runtime;

internal sealed class SceneData
{
    public List<EntityData> Entities { get; set; } = new();
}

internal sealed class EntityData
{
    public string Name { get; set; } = "GameObject";
    public TransformData Transform { get; set; } = new();
    public MeshRendererData? MeshRenderer { get; set; }
    public CameraData? Camera { get; set; }
    public LightData? Light { get; set; }
    public MaterialData? Material { get; set; }
    public SkyBoxData? SkyBox { get; set; }
    public RigidbodyData? Rigidbody { get; set; }
    public BoxColliderData? BoxCollider { get; set; }
    public VoxelGridData? VoxelGrid { get; set; }
    public List<BehaviorData> Behaviors { get; set; } = new();
}

internal sealed class TransformData
{
    public float[] Position { get; set; } = { 0f, 0f, 0f };
    public float[] Rotation { get; set; } = { 0f, 0f, 0f };
    public float[] Scale { get; set; } = { 1f, 1f, 1f };
}

internal sealed class MeshRendererData
{
    public string? MeshName { get; set; }
    public string? MaterialName { get; set; }
}

internal sealed class BehaviorData
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Properties { get; set; } = new();
}

internal sealed class CameraData
{
    public float Fov { get; set; } = 60f;
    public bool IsOrthographic { get; set; }
    public int RenderOutputId { get; set; }
}

internal sealed class LightData
{
    public int Type { get; set; }
    public float[] Color { get; set; } = { 1f, 1f, 1f };
    public float Intensity { get; set; } = 1f;
    public float Range { get; set; } = 10f;
}

internal sealed class MaterialData
{
    public float[] BaseColor { get; set; } = { 1f, 1f, 1f, 1f };
    public float[] SpecularColor { get; set; } = { 1f, 1f, 1f };
    public float Shininess { get; set; } = 32f;
    public string? MainTextureName { get; set; }
    public string? NormalTextureName { get; set; }
    public Dictionary<string, float> CustomProperties { get; set; } = new();
}

internal sealed class SkyBoxData
{
    public string CubeMapPath { get; set; } = string.Empty;
    public float Intensity { get; set; } = 1f;
}

internal sealed class RigidbodyData
{
    public float Mass { get; set; } = 1f;
    public bool UseGravity { get; set; } = true;
    public bool IsKinematic { get; set; }
    public float Damping { get; set; }
    public float[] Velocity { get; set; } = { 0f, 0f, 0f };
}

internal sealed class BoxColliderData
{
    public float[] Center { get; set; } = { 0f, 0f, 0f };
    public float[] Size { get; set; } = { 1f, 1f, 1f };
    public bool IsTrigger { get; set; }
}

internal sealed class VoxelGridData
{
    public int ResolutionX { get; set; } = 65;
    public int ResolutionY { get; set; } = 65;
    public int ResolutionZ { get; set; } = 65;
    public float VoxelSize { get; set; } = 0.125f;
    public float IsoLevel { get; set; }
    public int Mode { get; set; }
    public float Smoothing { get; set; } = 1.0f;
    public int NormalMode { get; set; } = 1;
}
