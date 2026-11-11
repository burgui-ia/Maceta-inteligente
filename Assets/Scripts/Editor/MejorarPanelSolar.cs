using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Maceta > Mejorar panel solar": le da al panel solar aspecto de panel real
/// (celdas fotovoltaicas, líneas de contacto y marco de aluminio) SIN costo de rendimiento:
///  - Genera una sola textura de 512x256 (~100 KB comprimida).
///  - Crea una copia de la malla del panel con UVs proyectadas desde arriba,
///    para que las celdas cubran la cara superior y los costados se vean como marco.
///  - Usa el mismo material "Panel solar" (URP Lit) con su brillo y reflejo del cielo.
/// </summary>
public static class MejorarPanelSolar
{
    const string Carpeta = "Assets/Materiales/PanelSolar";
    const string NombrePanel = "07A_Panel_Solar_Autosuficiente";
    const int Ancho = 512, Alto = 256;
    const int Columnas = 8, Filas = 4, Marco = 8, Separacion = 3;

    [MenuItem("Maceta/Mejorar panel solar")]
    public static void Mejorar()
    {
        var maceta = GameObject.Find("Maceta_Tarea");
        MeshFilter mf = null;
        if (maceta != null)
            foreach (var f in maceta.GetComponentsInChildren<MeshFilter>(true))
                if (f.name.StartsWith(NombrePanel)) { mf = f; break; }
        if (mf == null) { Debug.LogError($"No se encontró '{NombrePanel}' dentro de Maceta_Tarea."); return; }

        // Malla original del FBX (aunque ya se haya ejecutado antes)
        var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(mf);
        Mesh fuente = original != null ? original.sharedMesh : mf.sharedMesh;

        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Materiales", "PanelSolar");

        var malla = CrearMallaConUV(fuente, mf.transform);
        var textura = CrearTextura();

        // Material del panel
        var r = mf.GetComponent<MeshRenderer>();
        var mat = r.sharedMaterial;
        Undo.RecordObject(mat, "Mejorar panel solar");
        mat.SetTexture("_BaseMap", textura);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0.5f);
        mat.SetFloat("_Smoothness", 0.85f);
        EditorUtility.SetDirty(mat);

        Undo.RecordObject(mf, "Mejorar panel solar");
        mf.sharedMesh = malla;
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);
        Debug.Log("Panel solar mejorado: textura de celdas 512x256 + UVs desde arriba. Guarda la escena con Ctrl+S.");
    }

    /// Copia la malla y proyecta UVs desde arriba: caras superiores = celdas, resto = marco.
    static Mesh CrearMallaConUV(Mesh fuente, Transform t)
    {
        var m = Object.Instantiate(fuente);
        m.name = "Panel_Solar_Celdas";
        var v = m.vertices;
        var n = m.normals;
        var uv = new Vector2[v.Length];

        // Límites (en mundo) de las caras que miran hacia arriba
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < v.Length; i++)
        {
            if (t.TransformDirection(n[i]).y < 0.5f) continue;
            Vector3 w = t.TransformPoint(v[i]);
            minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
            minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
        }
        bool horizontalEnX = (maxX - minX) >= (maxZ - minZ);
        var uvMarco = new Vector2(1.5f / Ancho, 0.5f); // píxel del marco de aluminio

        for (int i = 0; i < v.Length; i++)
        {
            if (t.TransformDirection(n[i]).y < 0.5f) { uv[i] = uvMarco; continue; }
            Vector3 w = t.TransformPoint(v[i]);
            float a = Mathf.InverseLerp(minX, maxX, w.x);
            float b = Mathf.InverseLerp(minZ, maxZ, w.z);
            uv[i] = horizontalEnX ? new Vector2(a, b) : new Vector2(b, a); // el lado largo usa el ancho de la textura
        }
        m.uv = uv;

        string ruta = $"{Carpeta}/{m.name}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    /// Dibuja celdas azul oscuro con líneas de contacto plateadas y marco de aluminio.
    static Texture2D CrearTextura()
    {
        var marco = new Color(0.72f, 0.74f, 0.78f);
        var separador = new Color(0.80f, 0.82f, 0.86f);
        var celda = new Color(0.05f, 0.09f, 0.22f);
        var celdaClara = new Color(0.08f, 0.14f, 0.32f);
        var linea = new Color(0.55f, 0.60f, 0.68f);
        var dedo = new Color(0.10f, 0.15f, 0.30f);

        var px = new Color[Ancho * Alto];
        float anchoCelda = (Ancho - 2f * Marco) / Columnas;
        float altoCelda = (Alto - 2f * Marco) / Filas;

        for (int y = 0; y < Alto; y++)
        for (int x = 0; x < Ancho; x++)
        {
            Color c;
            if (x < Marco || y < Marco || x >= Ancho - Marco || y >= Alto - Marco)
            {
                // Marco con un bisel ligero
                bool borde = x == 0 || y == 0 || x == Ancho - 1 || y == Alto - 1 || x == Marco - 1 || y == Marco - 1 || x == Ancho - Marco || y == Alto - Marco;
                c = borde ? marco * 0.8f : marco;
            }
            else
            {
                float cx = (x - Marco) % anchoCelda, cy = (y - Marco) % altoCelda;
                if (cx < Separacion || cy < Separacion) c = separador;
                else
                {
                    // Degradado sutil para que cada celda tenga volumen
                    float gy = (cy - Separacion) / (altoCelda - Separacion);
                    c = Color.Lerp(celdaClara, celda, gy);
                    // 3 líneas de contacto (busbars) verticales
                    float u = (cx - Separacion) / (anchoCelda - Separacion);
                    if (Mathf.Abs(u - 0.25f) < 0.012f || Mathf.Abs(u - 0.5f) < 0.012f || Mathf.Abs(u - 0.75f) < 0.012f) c = linea;
                    // Dedos finos horizontales
                    else if (((int)cy) % 5 == 0) c = Color.Lerp(c, dedo, 0.6f);
                }
            }
            c.a = 1f;
            px[y * Ancho + x] = c;
        }

        var tex = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        string ruta = $"{Carpeta}/Panel_Solar_Celdas.png";
        System.IO.File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.mipmapEnabled = true;
        imp.anisoLevel = 2;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 512;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }
}
