using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewLauncher.Runtime;

internal static class RomSceneLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString
    };

    public static void LoadScene(string scenePath, string assetsRoot, IRomScriptRuntime scriptRuntime)
    {
        if (!File.Exists(scenePath))
        {
            throw new FileNotFoundException("Startup scene was not found.", scenePath);
        }

        SceneData? scene = JsonSerializer.Deserialize<SceneData>(File.ReadAllText(scenePath), JsonOptions);
        if (scene?.Entities == null)
        {
            throw new InvalidOperationException("Startup scene is invalid.");
        }

        EngineInterop.Engine_ClearScene();
        scriptRuntime.ClearLoadedBehaviors();
        foreach (EntityData entity in scene.Entities)
        {
            ulong entityId = EngineInterop.Engine_CreateGameObject(entity.Name ?? "GameObject");
            ApplyTransform(entityId, entity.Transform);
            ApplyNativeComponents(entityId, entity, assetsRoot, scenePath);

            if (entity.Behaviors.Count == 0)
            {
                continue;
            }

            scriptRuntime.SetLoadedBehaviors(entityId, entity.Behaviors);
            foreach (BehaviorData behavior in entity.Behaviors)
            {
                if (!string.IsNullOrWhiteSpace(behavior.Name))
                {
                    EngineInterop.Engine_AddComponent(entityId, behavior.Name);
                }
            }
        }
    }

    private static void ApplyTransform(ulong entityId, TransformData? transform)
    {
        if (transform == null)
        {
            return;
        }

        float[] position = EnsureVector3(transform.Position, 0f);
        float[] rotation = EnsureVector3(transform.Rotation, 0f);
        float[] scale = EnsureVector3(transform.Scale, 1f);
        EngineInterop.Transform_SetPosition(entityId, position[0], position[1], position[2]);
        EngineInterop.Transform_SetRotation(entityId, rotation[0], rotation[1], rotation[2]);
        EngineInterop.Transform_SetScale(entityId, scale[0], scale[1], scale[2]);
    }

    private static void ApplyNativeComponents(ulong entityId, EntityData entity, string assetsRoot, string scenePath)
    {
        if (entity.MeshRenderer != null)
        {
            EngineInterop.Engine_AddComponent(entityId, "MeshRenderer");
            if (!string.IsNullOrWhiteSpace(entity.MeshRenderer.MeshName))
            {
                EngineInterop.MeshRenderer_SetMesh(entityId, entity.MeshRenderer.MeshName);
            }

            if (!string.IsNullOrWhiteSpace(entity.MeshRenderer.MaterialName))
            {
                EngineInterop.MeshRenderer_SetMaterial(entityId, entity.MeshRenderer.MaterialName);
            }
        }

        if (entity.Camera != null)
        {
            EngineInterop.Engine_AddComponent(entityId, "Camera");
            EngineInterop.Camera_SetFOV(entityId, entity.Camera.Fov);
            EngineInterop.Camera_SetOrtho(entityId, entity.Camera.IsOrthographic);
            EngineInterop.Camera_SetRenderOutputID(entityId, entity.Camera.RenderOutputId);
        }

        if (entity.Light != null)
        {
            EngineInterop.Engine_AddComponent(entityId, "Light");
            float[] color = EnsureVector3(entity.Light.Color, 1f);
            EngineInterop.Light_SetType(entityId, entity.Light.Type);
            EngineInterop.Light_SetColor(entityId, color[0], color[1], color[2]);
            EngineInterop.Light_SetIntensity(entityId, entity.Light.Intensity);
            EngineInterop.Light_SetRange(entityId, entity.Light.Range);
        }

        if (entity.Material != null)
        {
            EngineInterop.Engine_AddComponent(entityId, "Material");
            float[] baseColor = EnsureVector4(entity.Material.BaseColor, 1f);
            float[] specular = EnsureVector3(entity.Material.SpecularColor, 1f);
            EngineInterop.Material_SetColor(entityId, baseColor[0], baseColor[1], baseColor[2], baseColor[3]);
            EngineInterop.Material_SetSpecular(entityId, specular[0], specular[1], specular[2], entity.Material.Shininess);

            if (!string.IsNullOrWhiteSpace(entity.Material.MainTextureName))
            {
                TryLoadTexture(entity.Material.MainTextureName, assetsRoot, scenePath);
                EngineInterop.Material_SetMainTexture(entityId, GetRuntimeTextureName(entity.Material.MainTextureName));
            }

            if (!string.IsNullOrWhiteSpace(entity.Material.NormalTextureName))
            {
                TryLoadTexture(entity.Material.NormalTextureName, assetsRoot, scenePath);
                EngineInterop.Material_SetNormalTexture(entityId, GetRuntimeTextureName(entity.Material.NormalTextureName));
            }

            for (int index = 0; index < 8; index++)
            {
                float x = entity.Material.CustomProperties.GetValueOrDefault($"extra[{index}].x");
                float y = entity.Material.CustomProperties.GetValueOrDefault($"extra[{index}].y");
                float z = entity.Material.CustomProperties.GetValueOrDefault($"extra[{index}].z");
                float w = entity.Material.CustomProperties.GetValueOrDefault($"extra[{index}].w");
                if (x != 0 || y != 0 || z != 0 || w != 0)
                {
                    EngineInterop.Material_SetCustomProperty(entityId, index, x, y, z, w);
                }
            }
        }

        if (entity.SkyBox != null)
        {
            EngineInterop.Engine_AddComponent(entityId, "SkyBox");
            EngineInterop.SkyBox_SetCubeMap(entityId, entity.SkyBox.CubeMapPath ?? string.Empty);
            EngineInterop.SkyBox_SetIntensity(entityId, entity.SkyBox.Intensity);
        }

        if (entity.Rigidbody != null)
        {
            float[] velocity = EnsureVector3(entity.Rigidbody.Velocity, 0f);
            EngineInterop.Engine_AddComponent(entityId, "Rigidbody");
            EngineInterop.Rigidbody_SetMass(entityId, entity.Rigidbody.Mass);
            EngineInterop.Rigidbody_SetUseGravity(entityId, entity.Rigidbody.UseGravity);
            EngineInterop.Rigidbody_SetIsKinematic(entityId, entity.Rigidbody.IsKinematic);
            EngineInterop.Rigidbody_SetDamping(entityId, entity.Rigidbody.Damping);
            EngineInterop.Rigidbody_SetVelocity(entityId, velocity[0], velocity[1], velocity[2]);
        }

        if (entity.BoxCollider != null)
        {
            float[] center = EnsureVector3(entity.BoxCollider.Center, 0f);
            float[] size = EnsureVector3(entity.BoxCollider.Size, 1f);
            EngineInterop.Engine_AddComponent(entityId, "BoxCollider");
            EngineInterop.BoxCollider_SetCenter(entityId, center[0], center[1], center[2]);
            EngineInterop.BoxCollider_SetSize(entityId, size[0], size[1], size[2]);
            EngineInterop.BoxCollider_SetIsTrigger(entityId, entity.BoxCollider.IsTrigger);
        }

        if (entity.VoxelGrid != null)
        {
            EngineInterop.Engine_AddComponent(entityId, "VoxelGrid");
            EngineInterop.VoxelGrid_CreateOrResize(
                entityId,
                entity.VoxelGrid.ResolutionX,
                entity.VoxelGrid.ResolutionY,
                entity.VoxelGrid.ResolutionZ,
                entity.VoxelGrid.VoxelSize,
                entity.VoxelGrid.IsoLevel);
            EngineInterop.VoxelGrid_SetMode(entityId, entity.VoxelGrid.Mode);
            EngineInterop.VoxelGrid_SetSurfaceOptions(entityId,
                entity.VoxelGrid.Smoothing, entity.VoxelGrid.NormalMode);
        }
    }

    private static void TryLoadTexture(string textureName, string assetsRoot, string scenePath)
    {
        string texturePath = Path.IsPathRooted(textureName)
            ? textureName
            : Path.Combine(assetsRoot, textureName);
        if (!File.Exists(texturePath))
        {
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath) ?? assetsRoot;
            texturePath = Path.Combine(sceneDirectoryPath, textureName);
        }

        if (File.Exists(texturePath))
        {
            EngineInterop.Engine_LoadTexture(texturePath, GetRuntimeTextureName(textureName));
        }
    }

    private static string GetRuntimeTextureName(string textureNameOrPath)
    {
        return Path.GetFileNameWithoutExtension(textureNameOrPath);
    }

    private static float[] EnsureVector3(float[]? values, float defaultValue)
    {
        return new[]
        {
            SafeFloat(values is { Length: > 0 } ? values[0] : defaultValue, defaultValue),
            SafeFloat(values is { Length: > 1 } ? values[1] : defaultValue, defaultValue),
            SafeFloat(values is { Length: > 2 } ? values[2] : defaultValue, defaultValue)
        };
    }

    private static float[] EnsureVector4(float[]? values, float defaultValue)
    {
        return new[]
        {
            SafeFloat(values is { Length: > 0 } ? values[0] : defaultValue, defaultValue),
            SafeFloat(values is { Length: > 1 } ? values[1] : defaultValue, defaultValue),
            SafeFloat(values is { Length: > 2 } ? values[2] : defaultValue, defaultValue),
            SafeFloat(values is { Length: > 3 } ? values[3] : defaultValue, defaultValue)
        };
    }

    private static float SafeFloat(float value, float defaultValue)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? defaultValue : value;
    }
}
