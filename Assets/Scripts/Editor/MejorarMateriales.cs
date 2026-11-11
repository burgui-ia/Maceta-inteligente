using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú "Maceta > Mejorar materiales": afina los Surface Inputs (URP Lit) de la maceta
/// manteniendo sus colores y su estética, pensando en celulares de gama baja:
///  - Sin texturas nuevas, sin normal maps ni occlusion maps (costarían memoria y lecturas extra).
///  - Agua y tanque NPK pasan de Transparent (con alfa 1, se veían opacos igual) a Opaque:
///    se ven igual pero ya no necesitan pasada transparente y proyectan sombra correctamente.
///  - Botones con Emission suave: parecen LEDs encendidos (solo suma un color, sin textura).
///  - Tierra y hojas sin reflejos de entorno ni brillo especular: más realistas (mate)
///    y además más baratos de calcular.
/// </summary>
public static class MejorarMateriales
{
    const string Carpeta = "Assets/Materiales/";

    [MenuItem("Maceta/Mejorar materiales")]
    public static void Mejorar()
    {
        var log = new System.Text.StringBuilder("Materiales mejorados:\n");

        // ── Carcasa y estructura: plástico/composite oscuro satinado ──
        Ajustar("Carcasa", log, m =>
        {
            m.SetFloat("_Metallic", 0.25f);     // menos "metal oscuro", más plástico técnico
            m.SetFloat("_Smoothness", 0.55f);   // satinado: brillo suave sin parecer espejo
        }, "metálico 0.25, suavidad 0.55 (plástico satinado)");

        // ── Agua (sistema de riego y botella): plástico/vidrio azul brillante ──
        Ajustar("Agua", log, m =>
        {
            HacerOpaco(m);
            m.SetFloat("_Metallic", 0.05f);
            m.SetFloat("_Smoothness", 0.92f);   // reflejo tipo vidrio/PET
        }, "Opaque (antes Transparent), suavidad 0.92 tipo vidrio");

        // ── Tanque de nutrientes NPK ──
        Ajustar("Tanque_Nutrientes_NPK", log, m =>
        {
            HacerOpaco(m);
            m.SetFloat("_Metallic", 0.05f);
            m.SetFloat("_Smoothness", 0.85f);
        }, "Opaque (antes Transparent), plástico brillante");

        // ── Botones: LEDs encendidos ──
        foreach (var boton in new[] { "Boton_1", "Boton_2", "Boton_3" })
        {
            Ajustar(boton, log, m =>
            {
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.75f);   // botón de plástico pulido
                Color c = m.GetColor("_BaseColor");
                m.SetColor("_EmissionColor", c * 0.45f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }, "emisión suave del mismo color (LED encendido)");
        }

        // ── Tierra: totalmente mate ──
        Ajustar("Tierra", log, m =>
        {
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.05f);
            SinReflejos(m);
        }, "mate, sin reflejos ni brillo especular (más barato)");

        // ── Hojas: mate, sin reflejos del cielo ──
        Ajustar("Follaje_plantas", log, m =>
        {
            m.SetFloat("_Metallic", 0f);
            SinReflejos(m, conservarBrillo: true);
        }, "sin reflejos de entorno (las hojas no reflejan el cielo)");

        AssetDatabase.SaveAssets();
        log.Append("Panel solar y Luz_solar no se tocaron.");
        Debug.Log(log.ToString());
    }

    static void Ajustar(string nombre, System.Text.StringBuilder log, System.Action<Material> cambios, string descripcion)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}{nombre}.mat");
        if (m == null) { Debug.LogWarning($"No se encontró el material {nombre}"); return; }
        Undo.RecordObject(m, "Mejorar materiales");
        cambios(m);
        EditorUtility.SetDirty(m);
        log.AppendLine($"  • {nombre}: {descripcion}");
    }

    /// Superficie opaca de URP Lit (equivale a elegir Surface Type = Opaque en el Inspector).
    static void HacerOpaco(Material m)
    {
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.SetOverrideTag("RenderType", "Opaque");
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");
        m.SetShaderPassEnabled("ShadowCaster", true);
        m.SetShaderPassEnabled("DepthOnly", true);
        m.renderQueue = -1; // usa la cola del shader (Geometry)
        Color c = m.GetColor("_BaseColor");
        c.a = 1f;
        m.SetColor("_BaseColor", c);
    }

    static void SinReflejos(Material m, bool conservarBrillo = false)
    {
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (!conservarBrillo)
        {
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
    }
}
