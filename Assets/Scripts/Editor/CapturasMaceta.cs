using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú "Maceta > Tomar capturas para la presentación": renderiza la escena desde
/// 6 ángulos con la cámara principal (sin la interfaz) y guarda PNG de 1920x1080
/// en la carpeta "Capturas" junto al proyecto (fuera de Assets, ignorada por git).
/// </summary>
public static class CapturasMaceta
{
    const int Ancho = 1920, Alto = 1080;

    [MenuItem("Maceta/Tomar capturas para la presentación")]
    public static void Capturar()
    {
        var cam = Camera.main;
        var maceta = GameObject.Find("Maceta_Tarea");
        if (cam == null || maceta == null) { Debug.LogError("Falta la Main Camera o Maceta_Tarea en la escena."); return; }

        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Capturas");
        Directory.CreateDirectory(carpeta);

        Bounds todo = Limites(maceta.transform, "");
        Bounds plantas = Limites(maceta.transform, "03_Plantas_Follaje");
        Bounds panel = Limites(maceta.transform, "07A_Panel_Solar");
        Bounds plafon = Limites(maceta.transform, "07B_Plafon_Luz");
        Bounds botones = Limites(maceta.transform, "_Boton_");

        var pos0 = cam.transform.position;
        var rot0 = cam.transform.rotation;
        float near0 = cam.nearClipPlane;
        Vector3 frente = pos0 - todo.center; frente.y = 0; frente.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);

        // Límites de la casa para no salir del cuarto
        Bounds cuarto = new Bounds(todo.center, Vector3.one * 100f);
        var casa = GameObject.Find("Patio_Estructura");
        if (casa == null) casa = GameObject.Find("Casa_Estructura");
        if (casa != null && casa.TryGetComponent<Renderer>(out var rc)) { cuarto = rc.bounds; cuarto.Expand(-0.4f); }

        var rt = new RenderTexture(Ancho, Alto, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
        var tex = new Texture2D(Ancho, Alto, TextureFormat.RGB24, false);
        int hechas = 0;
        try
        {
            cam.nearClipPlane = 0.03f;

            // 1. Vista general (la de la presentación inicial)
            Tomar(cam, rt, tex, carpeta, "1_vista_general", pos0, rot0); hechas++;

            // 2. Tres cuartos, un poco desde arriba
            Vector3 p2 = todo.center + (frente * 0.8f + lado * 0.6f + Vector3.up * 0.55f).normalized * todo.extents.magnitude * 2.4f;
            Tomar(cam, rt, tex, carpeta, "2_tres_cuartos", Dentro(p2, cuarto), Mirar(Dentro(p2, cuarto), todo.center)); hechas++;

            // 3. Plantas de cerca
            Vector3 p3 = plantas.center + (frente + Vector3.up * 0.8f).normalized * plantas.extents.magnitude * 1.5f;
            Tomar(cam, rt, tex, carpeta, "3_plantas", p3, Mirar(p3, plantas.center)); hechas++;

            // 4. Panel solar desde arriba
            Vector3 p4 = panel.center + Vector3.up * Mathf.Min(1.1f, cuarto.max.y - panel.center.y);
            Tomar(cam, rt, tex, carpeta, "4_panel_solar", p4, Quaternion.Euler(90f, maceta.transform.eulerAngles.y + 180f, 0f)); hechas++;

            // 5. Consola de tres botones
            Vector3 p5 = botones.center + (frente + Vector3.up * 0.25f).normalized * 0.55f;
            Tomar(cam, rt, tex, carpeta, "5_botones", p5, Mirar(p5, botones.center)); hechas++;

            // 6. Luz de crecimiento vista desde las plantas
            Vector3 p6 = plantas.center + frente * 0.25f;
            Tomar(cam, rt, tex, carpeta, "6_luz_crecimiento", p6, Quaternion.LookRotation(plafon.center - p6, -frente)); hechas++;
        }
        finally
        {
            cam.targetTexture = null;
            cam.transform.SetPositionAndRotation(pos0, rot0);
            cam.nearClipPlane = near0;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        Debug.Log($"{hechas} capturas guardadas en: {carpeta}");
        EditorUtility.RevealInFinder(carpeta);
    }

    static void Tomar(Camera cam, RenderTexture rt, Texture2D tex, string carpeta, string nombre, Vector3 pos, Quaternion rot)
    {
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
    }

    static Quaternion Mirar(Vector3 desde, Vector3 hacia) => Quaternion.LookRotation(hacia - desde, Vector3.up);

    static Vector3 Dentro(Vector3 p, Bounds b) =>
        new Vector3(Mathf.Clamp(p.x, b.min.x, b.max.x), Mathf.Clamp(p.y, b.min.y, b.max.y), Mathf.Clamp(p.z, b.min.z, b.max.z));

    static Bounds Limites(Transform raiz, string contiene)
    {
        bool hay = false;
        Bounds b = new Bounds(raiz.position, Vector3.one * 0.3f);
        foreach (var r in raiz.GetComponentsInChildren<Renderer>())
        {
            if (contiene.Length > 0 && !r.name.Contains(contiene)) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
