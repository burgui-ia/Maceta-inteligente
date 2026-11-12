using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú "Maceta > Crear patio exterior": pone la maceta al aire libre, en el patio trasero
/// de una casa, donde el panel solar sí recibe el sol. Estilo low-poly con cajas:
/// pasto, losa de piedra, pared de la casa con ventana y puerta, cerca de madera,
/// arbustos, una banca y un camino de piedras.
///
/// OPTIMIZADO PARA MÓVIL: todo el patio son solo 2 mallas con 1 material (Simple Lit)
/// y una textura-paleta de colores. Sin luces extra: lo ilumina el sol (Directional Light).
///   - Patio_Estructura: pasto, losa, pared, cerca (no proyecta sombras)
///   - Patio_Detalles: arbustos, banca y maceta pequeña (sí proyectan sombra)
/// Reemplaza a la casa anterior (Casa_Simple) y borra sus archivos.
/// </summary>
public static class CrearPatioExterior
{
    const string NombreRaiz = "Patio_Exterior";
    const string Carpeta = "Assets/Materiales/Patio";

    static readonly Color[] Paleta =
    {
        new Color(0.36f, 0.55f, 0.25f), //  0 pasto
        new Color(0.72f, 0.70f, 0.66f), //  1 losa de piedra
        new Color(0.90f, 0.85f, 0.75f), //  2 pared exterior
        new Color(0.35f, 0.36f, 0.40f), //  3 alero / techo
        new Color(0.95f, 0.95f, 0.95f), //  4 marcos
        new Color(0.30f, 0.45f, 0.55f), //  5 vidrio
        new Color(0.48f, 0.32f, 0.20f), //  6 madera oscura (puerta, banca)
        new Color(0.20f, 0.20f, 0.22f), //  7 metal
        new Color(0.20f, 0.42f, 0.18f), //  8 hojas
        new Color(0.36f, 0.24f, 0.15f), //  9 tronco
        new Color(0.65f, 0.50f, 0.34f), // 10 cerca de madera
        new Color(0.72f, 0.38f, 0.24f), // 11 barro (maceta pequeña)
    };
    const int PASTO = 0, LOSA = 1, PARED = 2, ALERO = 3, MARCO = 4, VIDRIO = 5, MADERA = 6,
              METAL = 7, HOJAS = 8, TRONCO = 9, CERCA = 10, BARRO = 11;

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

        public void Caja(Vector3 centro, Vector3 tam, int color, bool sinCaraInferior = true)
        {
            Vector3 h = tam * 0.5f;
            var uvColor = new Vector2((color + 0.5f) / Paleta.Length, 0.5f);
            foreach (var nor in Normales)
            {
                if (sinCaraInferior && nor == Vector3.down) continue; // la cara de abajo nunca se ve
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

    [MenuItem("Maceta/Crear patio exterior")]
    public static void Crear()
    {
        // ── Medir maceta y cámara ──
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

        float cx = b.center.x, cz = b.center.z;
        float zPared = b.min.z - 1.6f;                    // cara de la pared de la casa (detrás de la maceta)
        float minX = cx - 7f, maxX = cx + 7f;
        float minZ = zPared - 0.3f;
        float maxZ = Mathf.Max(cam.z, b.max.z) + 5f;
        float largo = maxZ - minZ;

        var est = new Constructor();   // sin sombras
        var det = new Constructor();   // con sombras

        // ── Suelo: pasto + losa de piedra bajo la maceta + camino ──
        est.Caja(new Vector3(cx, -0.08f, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX, 0.1f, largo), PASTO);
        float zLosa1 = b.max.z + 1.4f;
        est.Caja(new Vector3(cx, -0.015f, (zPared + zLosa1) * 0.5f), new Vector3(b.size.x + 3f, 0.03f, zLosa1 - zPared), LOSA);
        for (int i = 0; i < 4; i++)
            est.Caja(new Vector3(cx + (i % 2 == 0 ? -0.15f : 0.15f), -0.025f, zLosa1 + 0.5f + i * 0.8f), new Vector3(0.6f, 0.03f, 0.5f), LOSA);

        // ── Pared trasera de la casa, con alero, ventana y puerta ──
        float altoPared = 3f, anchoPared = 9f;
        est.Caja(new Vector3(cx, altoPared * 0.5f - 0.03f, zPared - 0.1f), new Vector3(anchoPared, altoPared, 0.2f), PARED);
        est.Caja(new Vector3(cx, altoPared + 0.05f, zPared + 0.1f), new Vector3(anchoPared + 0.4f, 0.15f, 0.6f), ALERO);

        float vAncho = 2f, vAlto = 1.2f, vBase = 1.0f, yC = vBase + vAlto * 0.5f, m = 0.08f;
        est.Caja(new Vector3(cx, yC, zPared + 0.01f), new Vector3(vAncho, vAlto, 0.04f), VIDRIO);
        est.Caja(new Vector3(cx, vBase, zPared + 0.03f), new Vector3(vAncho + m, m, 0.08f), MARCO);
        est.Caja(new Vector3(cx, vBase + vAlto, zPared + 0.03f), new Vector3(vAncho + m, m, 0.08f), MARCO);
        est.Caja(new Vector3(cx - vAncho * 0.5f, yC, zPared + 0.03f), new Vector3(m, vAlto, 0.08f), MARCO);
        est.Caja(new Vector3(cx + vAncho * 0.5f, yC, zPared + 0.03f), new Vector3(m, vAlto, 0.08f), MARCO);
        est.Caja(new Vector3(cx, yC, zPared + 0.03f), new Vector3(m * 0.6f, vAlto, 0.06f), MARCO);
        est.Caja(new Vector3(cx, vBase - 0.03f, zPared + 0.09f), new Vector3(vAncho + 0.3f, 0.06f, 0.18f), MARCO);

        float xPuerta = cx + 3.2f;
        est.Caja(new Vector3(xPuerta, 1.05f, zPared + 0.03f), new Vector3(1.0f, 2.1f, 0.06f), MADERA);
        est.Caja(new Vector3(xPuerta, 2.13f, zPared + 0.04f), new Vector3(1.16f, 0.08f, 0.08f), MARCO);
        est.Caja(new Vector3(xPuerta - 0.35f, 1.0f, zPared + 0.08f), new Vector3(0.12f, 0.04f, 0.05f), METAL);

        // ── Cerca de madera en los lados y al frente ──
        Cerca(est, new Vector3(minX + 0.3f, 0, zPared), new Vector3(minX + 0.3f, 0, maxZ - 0.3f));
        Cerca(est, new Vector3(maxX - 0.3f, 0, zPared), new Vector3(maxX - 0.3f, 0, maxZ - 0.3f));
        Cerca(est, new Vector3(minX + 0.3f, 0, maxZ - 0.3f), new Vector3(maxX - 0.3f, 0, maxZ - 0.3f));

        // ── Detalles con sombra: arbustos, banca, maceta pequeña ──
        det.Caja(new Vector3(cx - 2.5f, 0.35f, zPared + 0.4f), new Vector3(0.9f, 0.7f, 0.6f), HOJAS);
        det.Caja(new Vector3(cx - 1.7f, 0.25f, zPared + 0.35f), new Vector3(0.6f, 0.5f, 0.5f), HOJAS);
        det.Caja(new Vector3(cx + 2.2f, 0.3f, zPared + 0.35f), new Vector3(0.7f, 0.6f, 0.5f), HOJAS);

        float xBanca = cx + 2.4f, zBanca = cz + 0.4f;
        det.Caja(new Vector3(xBanca, 0.45f, zBanca), new Vector3(0.45f, 0.06f, 1.4f), MADERA);
        det.Caja(new Vector3(xBanca, 0.21f, zBanca - 0.6f), new Vector3(0.4f, 0.42f, 0.08f), MADERA);
        det.Caja(new Vector3(xBanca, 0.21f, zBanca + 0.6f), new Vector3(0.4f, 0.42f, 0.08f), MADERA);

        det.Caja(new Vector3(xPuerta + 0.85f, 0.2f, zPared + 0.35f), new Vector3(0.4f, 0.4f, 0.4f), BARRO);
        det.Caja(new Vector3(xPuerta + 0.85f, 0.5f, zPared + 0.35f), new Vector3(0.36f, 0.25f, 0.36f), HOJAS);

        // ── Assets: textura paleta, material y mallas ──
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Materiales", "Patio");
        var material = CrearMaterialPaleta();
        var mallaEst = GuardarMalla(est.CrearMalla("Patio_Estructura"));
        var mallaDet = GuardarMalla(det.CrearMalla("Patio_Detalles"));

        // ── Quitar la casa anterior y crear el patio ──
        foreach (var nombre in new[] { "Casa_Simple", NombreRaiz })
        {
            var viejo = GameObject.Find(nombre);
            if (viejo != null) Undo.DestroyObjectImmediate(viejo);
        }
        if (AssetDatabase.IsValidFolder("Assets/Materiales/Casa")) AssetDatabase.DeleteAsset("Assets/Materiales/Casa");

        var raiz = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear patio exterior");
        CrearRenderer(raiz, "Patio_Estructura", mallaEst, material, ShadowCastingMode.Off);
        CrearRenderer(raiz, "Patio_Detalles", mallaDet, material, ShadowCastingMode.On);

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

        Selection.activeGameObject = raiz;
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"Patio exterior creado: 2 mallas, 1 material, {(est.tri.Count + det.tri.Count) / 3} triángulos, " +
                  $"{est.v.Count + det.v.Count} vértices. Guarda la escena con Ctrl+S.");
    }

    /// Postes cada ~1.5 m y dos travesaños entre dos puntos.
    static void Cerca(Constructor c, Vector3 desde, Vector3 hasta)
    {
        Vector3 d = hasta - desde;
        float largo = d.magnitude;
        int postes = Mathf.Max(2, Mathf.RoundToInt(largo / 1.5f) + 1);
        bool enX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
        for (int i = 0; i < postes; i++)
        {
            Vector3 p = Vector3.Lerp(desde, hasta, i / (postes - 1f));
            c.Caja(new Vector3(p.x, 0.45f, p.z), new Vector3(0.1f, 0.9f, 0.1f), CERCA);
        }
        Vector3 medio = (desde + hasta) * 0.5f;
        Vector3 tam = enX ? new Vector3(largo, 0.08f, 0.05f) : new Vector3(0.05f, 0.08f, largo);
        c.Caja(new Vector3(medio.x, 0.35f, medio.z), tam, CERCA, false);
        c.Caja(new Vector3(medio.x, 0.75f, medio.z), tam, CERCA, false);
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
        string rutaTex = $"{Carpeta}/Patio_Paleta.png";
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

        string rutaMat = $"{Carpeta}/Patio_Paleta.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, rutaMat);
        }
        mat.SetTexture("_BaseMap", paleta);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
