using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú "Maceta > Optimizar para Android": deja el proyecto listo para celulares de gama baja
/// sin tocar el diseño de la maceta (mismos modelos, materiales y colores).
/// </summary>
public static class OptimizarAndroid
{
    [MenuItem("Maceta/Optimizar para Android")]
    public static void Optimizar()
    {
        var log = new System.Text.StringBuilder("Optimización para Android aplicada:\n");

        // ── 1. URP para celular (lo que usa Android: nivel de calidad "Mobile") ──
        if (Ajustar("Assets/Settings/Mobile_RPAsset.asset", so =>
        {
            Set(so, "m_SupportsHDR", 0);                    // menos memoria y ancho de banda
            Set(so, "m_MSAA", 1);                           // sin MSAA
            Set(so, "m_RenderScale", 0.8f);                 // 80 % de la resolución nativa
            Set(so, "m_RequireDepthTexture", 0);
            Set(so, "m_RequireOpaqueTexture", 0);
            Set(so, "m_MainLightShadowmapResolution", 1024);
            Set(so, "m_ShadowDistance", 8f);                // la escena mide ~6 m: 50 m era desperdicio
            Set(so, "m_ShadowCascadeCount", 1);
            Set(so, "m_SoftShadowsSupported", 0);
            Set(so, "m_AdditionalLightsRenderingMode", 2);  // luces extra por vértice
            Set(so, "m_AdditionalLightsPerObjectLimit", 2);
            Set(so, "m_AdditionalLightShadowsSupported", 0);
        })) log.AppendLine("  • Mobile_RPAsset: HDR off, sombras 8 m / 1 cascada / duras, luces extra por vértice, escala 0.8");

        // ── 2. URP de PC: sombras ajustadas al tamaño real de la escena ──
        if (Ajustar("Assets/Settings/PC_RPAsset.asset", so =>
        {
            Set(so, "m_ShadowDistance", 10f);
            Set(so, "m_ShadowCascadeCount", 2);
        })) log.AppendLine("  • PC_RPAsset: sombras 10 m / 2 cascadas (más nítidas y baratas)");

        // ── 3. Importación de modelos: sin rig, animación, cámaras, luces ni tangentes ──
        int modelos = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/FBX" }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(ruta) is not ModelImporter imp) continue;
            imp.animationType = ModelImporterAnimationType.None; // quita el Animator innecesario
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.importTangents = ModelImporterTangents.None;     // no hay normal maps
            imp.isReadable = false;
            imp.optimizeMeshPolygons = true;
            imp.optimizeMeshVertices = true;
            imp.SaveAndReimport();
            modelos++;
        }
        log.AppendLine($"  • {modelos} modelos FBX: sin rig/animación/cámaras/luces/tangentes");

        // ── 4. Player Settings de Android ──
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
        PlayerSettings.Android.optimizedFramePacing = true;
        PlayerSettings.SetMobileMTRendering(android, true);
        log.AppendLine("  • Android: IL2CPP, ARMv7 + ARM64, OpenGL ES 3 primero (más compatible en gama baja), Frame Pacing");

        // ── 5. Quitar del APK el código que no se usa (Assets/link.xml protege lo necesario) ──
        PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.High);
        PlayerSettings.stripEngineCode = true;
        log.AppendLine("  • Code stripping: High + Strip Engine Code (APK más ligero, menos RAM)");

        // ── 6. Física apagada (el proyecto no la usa) ──
        Physics.simulationMode = SimulationMode.Script;
        log.AppendLine("  • Física: simulación desactivada");

        AssetDatabase.SaveAssets();
        log.AppendLine("Listo. Guarda la escena (Ctrl+S) y revisa Stats en la vista Game.");
        Debug.Log(log.ToString());
    }

    static bool Ajustar(string ruta, System.Action<SerializedObject> cambios)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ruta);
        if (asset == null) { Debug.LogWarning($"No se encontró {ruta}"); return false; }
        var so = new SerializedObject(asset);
        cambios(so);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        return true;
    }

    static void Set(SerializedObject so, string prop, float valor)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"Propiedad {prop} no encontrada"); return; }
        switch (p.propertyType)
        {
            case SerializedPropertyType.Float: p.floatValue = valor; break;
            case SerializedPropertyType.Boolean: p.boolValue = valor != 0; break;
            case SerializedPropertyType.Enum: p.intValue = (int)valor; break;
            default: p.intValue = (int)valor; break;
        }
    }
}
