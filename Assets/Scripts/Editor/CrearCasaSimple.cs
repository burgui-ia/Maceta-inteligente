using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú "Maceta > Crear casa simple": construye un cuarto low-poly alrededor de la maceta
/// (piso, paredes, techo, ventana, puerta, tapete, lámpara, repisa y cuadro).
///
/// OPTIMIZADO PARA MÓVIL: toda la casa se genera como solo 2 mallas que comparten
/// 1 material (Simple Lit) con una textura-paleta de colores:
///   - Casa_Estructura: paredes, piso, techo, marcos (no proyecta sombras)
///   - Casa_Muebles: repisa y macetas pequeñas (sí proyecta sombras)
/// Así la casa cuesta ~2 draw calls en lugar de ~35.
/// </summary>
public static class CrearCasaSimple
{
    const string NombreRaiz = "Casa_Simple";
    const string Carpeta = "Assets/Materiales/Casa";
    const float Alto = 2.8f;
    const float Grosor = 0.1f;

    // Paleta: cada color es un píxel de la textura
    static readonly Color[] Paleta =
    {
        new Color(0.55f, 0.38f, 0.24f), // 0 piso madera
        new Color(0.93f, 0.89f, 0.80f), // 1 pared
        new Color(0.97f, 0.97f, 0.95f), // 2 techo
        new Color(0.96f, 0.96f, 0.96f), // 3 marco / zoclo
        new Color(0.42f, 0.27f, 0.16f), // 4 puerta / repisa
        new Color(0.36f, 0.48f, 0.42f), // 5 tapete / macetas mini
        new Color(0.20f, 0.20f, 0.22f), // 6 metal
        new Color(0.85f, 0.55f, 0.25f), // 7 cuadro
    };
    const int PISO = 0, PARED = 1, TECHO = 2, MARCO = 3, MADERA = 4, TAPETE = 5, METAL = 6, CUADRO = 7;

    /// Acumula cajas en una sola malla.
    class Constructor
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> tri = new List<int>();

        static readonly Vector3[] Normales =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        public void Caja(Vector3 centro, Vector3 tam, int color, bool sinCaraInferior = false)
        {
            Vector3 h = tam * 0.5f;
            var uvColor = new Vector2((color + 0.5f) / Paleta.Length, 0.5f);
            foreach (var nor in Normales)
            {
                if (sinCaraInferior && nor == Vector3.down) continue;
                // a, b perpendiculares a la normal con Cross(a, b) = -n (triángulos con orden horario de Unity)
                Vector3 a = nor.y != 0 ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(a, nor);
                Vector3 p = centro + Vector3.Scale(nor, h);
                Vector3 sa = Vector3.Scale(a, h), sb = Vector3.Scale(b, h);
                int i = v.Count;
                v.Add(p - sa - sb); v.Add(p - sa + sb); v.Add(p + sa + sb); v.Add(p + sa - sb);
                for (int k = 0; k < 4; k++) { n.Add(nor); uv.Add(uvColor); }
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
                tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            }
        }

        public Mesh CrearMalla(string nombre)
        {
            var m = new Mesh { name = nombre };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            m.UploadMeshData(false);
            return m;
        }
    }

    [MenuItem("Maceta/Crear casa simple")]
    public static void Crear()
    {
        // ── Medir maceta y cámara para que el cuarto las contenga ──
        var maceta = GameObject.Find("Maceta_Tarea");
        Bounds b = new Bounds(new Vector3(1.23f, 0.8f, 4.61f), new Vector3(1.6f, 1.6f, 1.1f));
        if (maceta != null)
        {
            var rs = maceta.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
            }
        }
        Vector3 cam = Camera.main != null ? Camera.main.transform.position : b.center + Vector3.forward * 2.5f;

        float minX = Mathf.Min(b.min.x, cam.x) - 2.2f;
        float maxX = Mathf.Max(b.max.x, cam.x) + 2.2f;
        float minZ = b.min.z - 1.2f;                       // pared con ventana detrás de la maceta
        float maxZ = Mathf.Max(b.max.z, cam.z) + 1.5f;     // pared detrás de la cámara
        float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
        float ancho = maxX - minX, fondo = maxZ - minZ;

        var est = new Constructor();   // sin sombras
        var mue = new Constructor();   // con sombras

        // ── Piso y techo ──
        est.Caja(new Vector3(cx, -Grosor / 2, cz), new Vector3(ancho, Grosor, fondo), PISO);
        est.Caja(new Vector3(cx, Alto + Grosor / 2, cz), new Vector3(ancho, Grosor, fondo), TECHO);

        // ── Pared trasera con ventana (hueco abierto hacia el cielo) ──
        float vAncho = Mathf.Min(2.2f, ancho - 1f), vAlto = 1.3f, vBase = 0.9f;
        float zT = minZ - Grosor / 2;
        float lado = (ancho - vAncho) / 2;
        est.Caja(new Vector3(minX + lado / 2, Alto / 2, zT), new Vector3(lado, Alto, Grosor), PARED);
        est.Caja(new Vector3(maxX - lado / 2, Alto / 2, zT), new Vector3(lado, Alto, Grosor), PARED);
        est.Caja(new Vector3(cx, vBase / 2, zT), new Vector3(vAncho, vBase, Grosor), PARED);
        float arriba = Alto - (vBase + vAlto);
        est.Caja(new Vector3(cx, Alto - arriba / 2, zT), new Vector3(vAncho, arriba, Grosor), PARED);

        // Marco de la ventana
        float m = 0.06f, yC = vBase + vAlto / 2, gm = Grosor + 0.04f;
        est.Caja(new Vector3(cx, vBase, zT), new Vector3(vAncho + m, m, gm), MARCO);
        est.Caja(new Vector3(cx, vBase + vAlto, zT), new Vector3(vAncho + m, m, gm), MARCO);
        est.Caja(new Vector3(cx - vAncho / 2, yC, zT), new Vector3(m, vAlto, gm), MARCO);
        est.Caja(new Vector3(cx + vAncho / 2, yC, zT), new Vector3(m, vAlto, gm), MARCO);
        est.Caja(new Vector3(cx, yC, zT), new Vector3(m * 0.6f, vAlto, Grosor * 0.6f), MARCO);
        est.Caja(new Vector3(cx, vBase - 0.02f, zT + 0.1f), new Vector3(vAncho + 0.2f, 0.04f, 0.22f), MARCO);

        // ── Otras paredes ──
        est.Caja(new Vector3(cx, Alto / 2, maxZ + Grosor / 2), new Vector3(ancho, Alto, Grosor), PARED);
        est.Caja(new Vector3(minX - Grosor / 2, Alto / 2, cz), new Vector3(Grosor, Alto, fondo + 2 * Grosor), PARED);
        est.Caja(new Vector3(maxX + Grosor / 2, Alto / 2, cz), new Vector3(Grosor, Alto, fondo + 2 * Grosor), PARED);

        // Zoclos
        est.Caja(new Vector3(minX + 0.015f, 0.05f, cz), new Vector3(0.03f, 0.1f, fondo), MARCO, true);
        est.Caja(new Vector3(maxX - 0.015f, 0.05f, cz), new Vector3(0.03f, 0.1f, fondo), MARCO, true);

        // ── Puerta en la pared izquierda ──
        float zPuerta = Mathf.Lerp(minZ, maxZ, 0.7f);
        est.Caja(new Vector3(minX + 0.025f, 1.05f, zPuerta), new Vector3(0.05f, 2.1f, 0.9f), MADERA, true);
        est.Caja(new Vector3(minX + 0.075f, 1.0f, zPuerta + 0.33f), new Vector3(0.05f, 0.04f, 0.12f), METAL);

        // ── Tapete bajo la maceta ──
        est.Caja(new Vector3(b.center.x, 0.005f, b.center.z), new Vector3(b.size.x + 0.9f, 0.01f, b.size.z + 0.9f), TAPETE, true);

        // ── Cuadro en la pared izquierda ──
        float zCuadro = Mathf.Lerp(minZ, maxZ, 0.3f);
        est.Caja(new Vector3(minX + 0.02f, 1.6f, zCuadro), new Vector3(0.04f, 0.7f, 1.0f), MADERA);
        est.Caja(new Vector3(minX + 0.045f, 1.6f, zCuadro), new Vector3(0.01f, 0.58f, 0.88f), CUADRO);

        // ── Lámpara de techo ──
        est.Caja(new Vector3(cx, Alto - 0.03f, cz), new Vector3(0.5f, 0.06f, 0.5f), MARCO);

        // ── Repisa con macetas pequeñas en la pared derecha (proyectan sombra) ──
        float zRep = Mathf.Lerp(minZ, maxZ, 0.35f);
        mue.Caja(new Vector3(maxX - 0.15f, 1.5f, zRep), new Vector3(0.28f, 0.04f, 1.2f), MADERA);
        for (int i = 0; i < 3; i++)
            mue.Caja(new Vector3(maxX - 0.15f, 1.6f, zRep - 0.4f + i * 0.4f), new Vector3(0.14f, 0.16f, 0.14f), TAPETE, true);

        // ── Assets: textura paleta, material y mallas ──
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Materiales", "Casa");
        BorrarMaterialesViejos();
        var material = CrearMaterialPaleta();
        var mallaEst = GuardarMalla(est.CrearMalla("Casa_Estructura"));
        var mallaMue = GuardarMalla(mue.CrearMalla("Casa_Muebles"));

        // ── GameObjects ──
        var viejo = GameObject.Find(NombreRaiz);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);
        var raiz = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear casa simple");

        CrearRenderer(raiz, "Casa_Estructura", mallaEst, material, ShadowCastingMode.Off);
        CrearRenderer(raiz, "Casa_Muebles", mallaMue, material, ShadowCastingMode.On);

        // Lámpara: luz cálida sin sombras. En móvil se calcula por vértice (barata).
        var goLuz = new GameObject("Lampara_Foco");
        goLuz.transform.SetParent(raiz.transform, false);
        goLuz.transform.position = new Vector3(cx, Alto - 0.25f, cz);
        var luz = goLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.88f, 0.72f);
        luz.intensity = 1.2f;
        luz.range = Mathf.Max(ancho, fondo);
        luz.shadows = LightShadows.None;
        luz.renderMode = LightRenderMode.ForceVertex;

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        Selection.activeGameObject = raiz;
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"Casa simple creada ({ancho:0.0} x {fondo:0.0} m): 2 mallas, 1 material, " +
                  $"{(est.tri.Count + mue.tri.Count) / 3} triángulos. Guarda la escena con Ctrl+S.");
    }

    static void CrearRenderer(GameObject padre, string nombre, Mesh malla, Material mat, ShadowCastingMode sombras)
    {
        var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(padre.transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = malla;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = sombras;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    static Mesh GuardarMalla(Mesh malla)
    {
        string ruta = $"{Carpeta}/{malla.name}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(malla, ruta);
        return malla;
    }

    static Material CrearMaterialPaleta()
    {
        // Textura de 8x1 píxeles con los colores de la casa
        string rutaTex = $"{Carpeta}/Casa_Paleta.png";
        var tex = new Texture2D(Paleta.Length, 1, TextureFormat.RGBA32, false);
        tex.SetPixels(Paleta);
        tex.Apply();
        System.IO.File.WriteAllBytes(rutaTex, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(rutaTex);
        var imp = (TextureImporter)AssetImporter.GetAtPath(rutaTex);
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        var paleta = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex);

        // Simple Lit: mucho más barato que Lit en celulares
        string rutaMat = $"{Carpeta}/Casa_Paleta.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, rutaMat);
        }
        mat.SetTexture("_BaseMap", paleta);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.enableInstancing = false;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BorrarMaterialesViejos()
    {
        string[] viejos = { "Casa_Piso_Madera", "Casa_Pared", "Casa_Techo", "Casa_Marco", "Casa_Puerta", "Casa_Tapete", "Casa_Metal", "Casa_Cuadro" };
        foreach (var n in viejos) AssetDatabase.DeleteAsset($"{Carpeta}/{n}.mat");
    }
}
