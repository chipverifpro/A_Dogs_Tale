using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class EmoteIconVisualFactory
{
    const string InstanceName = "EmoteIconVisual";
    const float DefaultSize = 0.9f;
    const float DefaultThickness = 0.04f;
    const float DefaultLifetimeSeconds = 10f;
    const float DefaultTopPadding = 0.12f;
    const float OverheadEmoteAlpha = 0.5f;

    static readonly Vector3 DefaultLocalOffset = new(0f, 1.35f, 0f);
    static readonly Vector3 DefaultSpinDegreesPerSecond = new(0f, 180f, 0f);
    static readonly Quaternion OverheadEmoteVisualRotation = Quaternion.Euler(-90f, 0f, 0f);
    static readonly Dictionary<string, Material> materialsByTextureAndAlpha = new();
    static readonly Dictionary<string, Mesh> meshesBySpriteAndShape = new();

    public static GameObject Show(WorldObject agent, string emote)
    {
        if (agent == null ||
            (!SpriteServer.TryGetEmojiSprite(emote, out Sprite sprite, out _) &&
             !SpriteServer.TryGetHumanEmojiSprite(emote, out sprite, out _)))
        {
            return null;
        }

        return Show(agent.transform, sprite);
    }

    public static GameObject Show(WorldObject agent, Sprite sprite)
    {
        return agent != null ? Show(agent.transform, sprite) : null;
    }

    public static GameObject ShowOverhead(WorldObject agent, Sprite sprite, float size = DefaultSize, string instanceName = InstanceName)
    {
        if (agent == null)
            return null;

        return Show(
            agent.transform,
            sprite,
            localOffset: GetOverheadLocalOffset(agent, size),
            size: size,
            alpha: OverheadEmoteAlpha,
            visualLocalRotation: OverheadEmoteVisualRotation,
            instanceName: instanceName);
    }

    public static GameObject Show(
        Transform anchor,
        Sprite sprite,
        Vector3? localOffset = null,
        float size = DefaultSize,
        float thickness = DefaultThickness,
        float lifetimeSeconds = DefaultLifetimeSeconds,
        Vector3? spinDegreesPerSecond = null,
        float alpha = 1f,
        Quaternion? visualLocalRotation = null,
        string instanceName = InstanceName)
    {
        if (anchor == null || sprite == null || sprite.texture == null)
            return null;

        Transform existing = anchor.Find(instanceName);
        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            Object.Destroy(existing.gameObject);
        }

        GameObject iconObject = new GameObject(instanceName);
        iconObject.transform.SetParent(anchor, false);
        iconObject.transform.localPosition = localOffset ?? DefaultLocalOffset;
        iconObject.transform.localRotation = Quaternion.identity;
        iconObject.transform.localScale = Vector3.one;

        GameObject visualObject = new GameObject("IconVisual");
        visualObject.transform.SetParent(iconObject.transform, false);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = visualLocalRotation ?? Quaternion.identity;
        visualObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateSquareCardMesh(sprite, size, thickness);

        MeshRenderer meshRenderer = visualObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetMaterial(sprite.texture, alpha);
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        EmoteIconSpinner spinner = iconObject.AddComponent<EmoteIconSpinner>();
        spinner.SpinDegreesPerSecond = spinDegreesPerSecond ?? DefaultSpinDegreesPerSecond;

        if (lifetimeSeconds > 0f)
            Object.Destroy(iconObject, lifetimeSeconds);

        return iconObject;
    }

    public static Vector3 GetOverheadLocalOffset(WorldObject target, float iconSize = DefaultSize, float topPadding = DefaultTopPadding)
    {
        if (target == null)
            return DefaultLocalOffset;

        if (!TryGetRenderableBounds(target, out Bounds bounds))
            return DefaultLocalOffset;

        float iconCenterY = bounds.max.y + (Mathf.Max(0.01f, iconSize) * 0.5f) + topPadding;
        Vector3 localOffset = target.transform.InverseTransformPoint(new Vector3(
            target.transform.position.x,
            iconCenterY,
            target.transform.position.z));

        localOffset.x = 0f;
        localOffset.z = 0f;
        return localOffset;
    }

    public static void Hide(Transform anchor, string instanceName = InstanceName)
    {
        if (anchor == null)
            return;

        Transform existing = anchor.Find(instanceName);
        if (existing == null)
            return;

        existing.gameObject.SetActive(false);
        Object.Destroy(existing.gameObject);
    }

    static bool TryGetRenderableBounds(WorldObject target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
            return false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.GetComponentInParent<EmoteIconSpinner>() != null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    static Mesh CreateSquareCardMesh(Sprite sprite, float size, float thickness)
    {
        string meshKey = $"{sprite.GetInstanceID()}:{size:0.###}:{thickness:0.###}";
        if (meshesBySpriteAndShape.TryGetValue(meshKey, out Mesh cachedMesh))
            return cachedMesh;

        float halfSize = Mathf.Max(0.01f, size) * 0.5f;
        float halfDepth = Mathf.Max(0.001f, thickness) * 0.5f;
        Rect uvRect = GetNormalizedTextureRect(sprite);

        Vector2 uvBottomLeft = new(uvRect.xMin, uvRect.yMin);
        Vector2 uvBottomRight = new(uvRect.xMax, uvRect.yMin);
        Vector2 uvTopRight = new(uvRect.xMax, uvRect.yMax);
        Vector2 uvTopLeft = new(uvRect.xMin, uvRect.yMax);

        Vector3 leftBottomFront = new(-halfSize, -halfSize, -halfDepth);
        Vector3 rightBottomFront = new(halfSize, -halfSize, -halfDepth);
        Vector3 rightTopFront = new(halfSize, halfSize, -halfDepth);
        Vector3 leftTopFront = new(-halfSize, halfSize, -halfDepth);

        Vector3 leftBottomBack = new(-halfSize, -halfSize, halfDepth);
        Vector3 rightBottomBack = new(halfSize, -halfSize, halfDepth);
        Vector3 rightTopBack = new(halfSize, halfSize, halfDepth);
        Vector3 leftTopBack = new(-halfSize, halfSize, halfDepth);

        List<Vector3> vertices = new(24);
        List<Vector2> uvs = new(24);
        List<int> triangles = new(36);

        AddFace(vertices, uvs, triangles, leftBottomFront, rightBottomFront, rightTopFront, leftTopFront, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft);
        AddFace(vertices, uvs, triangles, rightBottomBack, leftBottomBack, leftTopBack, rightTopBack, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft);
        AddFace(vertices, uvs, triangles, leftBottomBack, leftBottomFront, leftTopFront, leftTopBack, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft);
        AddFace(vertices, uvs, triangles, rightBottomFront, rightBottomBack, rightTopBack, rightTopFront, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft);
        AddFace(vertices, uvs, triangles, leftTopFront, rightTopFront, rightTopBack, leftTopBack, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft);
        AddFace(vertices, uvs, triangles, leftBottomBack, rightBottomBack, rightBottomFront, leftBottomFront, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft);

        Mesh mesh = new()
        {
            name = "EmoteIconSquareMesh"
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.DontSave;
        meshesBySpriteAndShape[meshKey] = mesh;
        return mesh;
    }

    static void AddFace(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 bottomLeft,
        Vector3 bottomRight,
        Vector3 topRight,
        Vector3 topLeft,
        Vector2 uvBottomLeft,
        Vector2 uvBottomRight,
        Vector2 uvTopRight,
        Vector2 uvTopLeft)
    {
        int start = vertices.Count;

        vertices.Add(bottomLeft);
        vertices.Add(bottomRight);
        vertices.Add(topRight);
        vertices.Add(topLeft);

        uvs.Add(uvBottomLeft);
        uvs.Add(uvBottomRight);
        uvs.Add(uvTopRight);
        uvs.Add(uvTopLeft);

        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    static Rect GetNormalizedTextureRect(Sprite sprite)
    {
        Rect textureRect = GetTextureRect(sprite);
        Texture texture = sprite.texture;

        return new Rect(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);
    }

    static Rect GetTextureRect(Sprite sprite)
    {
        try
        {
            return sprite.textureRect;
        }
        catch (UnityException)
        {
            return sprite.rect;
        }
    }

    static Material GetMaterial(Texture texture, float alpha = 1f)
    {
        alpha = Mathf.Clamp01(alpha);
        string materialKey = $"{texture.GetInstanceID()}:{alpha:0.###}";
        if (materialsByTextureAndAlpha.TryGetValue(materialKey, out Material material))
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Standard");

        material = new Material(shader)
        {
            name = $"EmoteIcon_{texture.name}",
            hideFlags = HideFlags.DontSave
        };

        SetTexture(material, texture, alpha);
        ConfigureTransparentMaterial(material);
        materialsByTextureAndAlpha[materialKey] = material;
        return material;
    }

    static void SetTexture(Material material, Texture texture, float alpha)
    {
        material.mainTexture = texture;
        Color color = new(1f, 1f, 1f, Mathf.Clamp01(alpha));

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }

    static void ConfigureTransparentMaterial(Material material)
    {
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
    }
}
