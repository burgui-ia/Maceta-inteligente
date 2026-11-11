using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Maceta > Mejorar plantas": da vida a las plantas SIN costo de rendimiento.
///  - Degradado por altura: verde oscuro en la base → verde-amarillo en las puntas.
///  - Cada planta con un tono de verde ligeramente distinto.
/// Usa una textura de 6x64 píxeles y copias de las mallas (alta y baja resolución)
/// con UVs nuevas. Mismos triángulos, mismo material, mismos draw calls.
/// </summary>
public static class MejorarPlantas
{
    const string Carpeta = "Assets/Materiales/Plantas";
    const string NombrePlantas = "03_Plantas_Follaje";
    const int Columnas = 3, Filas = 2;              // acomodo de las plantas en la maceta
    const int Variantes = Columnas * Filas, AltoTex = 64;

    // Tono de cada planta: (base oscura, punta clara)
    static readonly (Color baseC, Color punta)[] Tonos =
    {
        (new Color(0.04f, 0.22f, 0.09f), new Color(0.50f, 0.86f, 0.32f)),
        (new Color(0.03f, 0.20f, 0.10f), new Color(0.38f, 0.82f, 0.40f)),
        (new Color(0.05f, 0.24f, 0.08f), new Color(0.58f, 0.88f, 0.28f)),
        (new Color(0.03f, 0.21f, 0.11f), new Color(0.34f, 0.80f, 0.44f)),
        (new Color(0.05f, 0.23f, 0.08f), new Color(0.54f, 0.85f, 0.30f)),
        (new Color(0.04f, 0.20f, 0.09f), new Color(0.44f, 0.84f, 0.36f)),
    };

    [MenuItem("Maceta/Mejorar plantas")]
    public static void Mejorar()
    {
        var maceta = GameObject.Find("Maceta_Tarea");
        MeshFilter mf = null;
        if (maceta != null)
            foreach (var f in maceta.GetComponentsInChildren<MeshFilter>(true))
                if (f.name == NombrePlantas) { mf = f; break; }
        if (mf == null) { Debug.LogError($"No se encontró '{NombrePlantas}' dentro de Maceta_Tarea."); return; }

        // Mallas originales del FBX: LOD0 (alta) y LOD2 (baja)
        var origen = PrefabUtility.GetCorrespondingObjectFromOriginalSource(mf);
        if (origen == null) { Debug.LogError("No se encontró la malla original del FBX."); return; }
        MeshFilter origenBaja = null;
        foreach (var f in origen.transform.root.GetComponentsInChildren<MeshFilter>(true))
            if (f.transform.parent != null && f.transform.parent.name.Contains("LOD2") &&
                Regex.Replace(f.name, @"\.\d+$", "") == NombrePlantas) { origenBaja = f; break; }

        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Materiales", "Plantas");

        var alta = CrearMallaConUV(origen.sharedMesh, origen.transform, "Plantas_Follaje_Color");
        if (origenBaja != null) CrearMallaConUV(origenBaja.sharedMesh, origenBaja.transform, "Plantas_Follaje_Color_Baja");
        var textura = CrearTextura();

        var mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
        Undo.RecordObject(mat, "Mejorar plantas");
        mat.SetTexture("_BaseMap", textura);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(mat);

        Undo.RecordObject(mf, "Mejorar plantas");
        mf.sharedMesh = alta;
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);
        Debug.Log($"Plantas mejoradas: degradado + {Variantes} tonos (LOD bajo {(origenBaja != null ? "también" : "no encontrado")}). Guarda la escena con Ctrl+S.");
    }

    /// U = qué planta (columna de la textura), V = altura dentro de esa planta.
    static Mesh CrearMallaConUV(Mesh fuente, Transform t, string nombre)
    {
        var m = Object.Instantiate(fuente);
        m.name = nombre;
        var v = m.vertices;
        var w = new Vector3[v.Length];
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < v.Length; i++)
        {
            w[i] = t.TransformPoint(v[i]);
            min = Vector3.Min(min, w[i]); max = Vector3.Max(max, w[i]);
        }

        // Altura mínima y máxima de cada planta
        var bajo = new float[Variantes];
        var alto = new float[Variantes];
        var celda = new int[v.Length];
        for (int k = 0; k < Variantes; k++) { bajo[k] = float.MaxValue; alto[k] = float.MinValue; }
        for (int i = 0; i < v.Length; i++)
        {
            int col = Mathf.Clamp((int)(Mathf.InverseLerp(min.x, max.x, w[i].x) * Columnas), 0, Columnas - 1);
            int fil = Mathf.Clamp((int)(Mathf.InverseLerp(min.z, max.z, w[i].z) * Filas), 0, Filas - 1);
            int c = fil * Columnas + col;
            celda[i] = c;
            bajo[c] = Mathf.Min(bajo[c], w[i].y);
            alto[c] = Mathf.Max(alto[c], w[i].y);
        }

        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            int c = celda[i];
            float h = alto[c] > bajo[c] ? (w[i].y - bajo[c]) / (alto[c] - bajo[c]) : 0.5f;
            uv[i] = new Vector2((c + 0.5f) / Variantes, Mathf.Lerp(0.5f, AltoTex - 0.5f, h) / AltoTex);
        }
        m.uv = uv;

        string ruta = $"{Carpeta}/{nombre}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Texture2D CrearTextura()
    {
        var px = new Color[Variantes * AltoTex];
        for (int y = 0; y < AltoTex; y++)
        {
            float h = y / (AltoTex - 1f);
            // Curva: la base oscura sube rápido, las puntas se aclaran al final
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Pow(h, 0.8f));
            for (int x = 0; x < Variantes; x++)
            {
                var c = Color.Lerp(Tonos[x].baseC, Tonos[x].punta, t);
                c.a = 1f;
                px[y * Variantes + x] = c;
            }
        }
        var tex = new Texture2D(Variantes, AltoTex, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        string ruta = $"{Carpeta}/Plantas_Degradado.png";
        System.IO.File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.mipmapEnabled = false;          // así cada planta no se mezcla con el tono de la de al lado
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.textureCompression = TextureImporterCompression.Uncompressed; // 1.5 KB: no vale la pena comprimir
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }
}
