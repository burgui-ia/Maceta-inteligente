using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Baja el detalle de todo lo que NO se está inspeccionando:
///  - Cambia las partes de la maceta que no están en foco a su malla de baja resolución (LOD2 del FBX).
///  - Quita las sombras de las partes fuera de foco y de la casa.
///  - Desenfoca el fondo con un Depth of Field Gaussiano (se calcula a media resolución).
/// Al volver a la vista general todo regresa a la máxima calidad.
/// ControlMaceta llama a Enfocar() / QuitarEnfoque() al cambiar de vista.
/// </summary>
public class DetalleEnfoque : MonoBehaviour
{
    [Serializable]
    public class ParteLOD
    {
        public MeshFilter filtro;   // parte de la maceta (LOD0)
        public Mesh mallaBaja;      // misma parte en baja resolución (LOD2)
        [NonSerialized] public Mesh mallaAlta;
    }

    [Header("Referencias")]
    public Transform maceta;
    [Tooltip("Raíz de la casa (opcional). Se busca 'Casa_Simple' si está vacío.")]
    public Transform casa;

    [Header("Mallas de baja resolución (se llenan solas en el editor)")]
    public List<ParteLOD> partes = new List<ParteLOD>();

    [Header("Desenfoque del fondo")]
    public bool usarDesenfoque = true;
    [Tooltip("El desenfoque necesita la textura de profundidad y una pasada extra: en celulares se desactiva salvo que marques esto.")]
    public bool desenfoqueEnMovil = false;
    [Tooltip("Distancia extra detrás del objeto enfocado donde empieza el desenfoque.")]
    public float margenDesenfoque = 0.1f;
    [Tooltip("Metros que tarda el fondo en llegar al desenfoque máximo.")]
    public float rangoDesenfoque = 1.5f;
    [Range(0.5f, 1.5f)] public float radioDesenfoque = 1.5f;
    public float velocidadTransicion = 4f;

    readonly List<Renderer> renderersMaceta = new List<Renderer>();
    readonly List<Renderer> renderersCasa = new List<Renderer>();
    readonly Dictionary<Renderer, ShadowCastingMode> sombrasOriginales = new Dictionary<Renderer, ShadowCastingMode>();

    Volume volumen;
    DepthOfField dof;
    float pesoObjetivo;

    void Awake()
    {
        if (maceta == null)
        {
            var go = GameObject.Find("Maceta_Tarea");
            if (go != null) maceta = go.transform;
        }
        if (casa == null)
        {
            var go = GameObject.Find("Casa_Simple");
            if (go != null) casa = go.transform;
        }

#if UNITY_EDITOR
        if (partes.Count == 0) LlenarDesdeFBX();
#endif
        foreach (var p in partes)
            if (p.filtro != null) p.mallaAlta = p.filtro.sharedMesh;

        if (maceta != null) renderersMaceta.AddRange(maceta.GetComponentsInChildren<Renderer>(true));
        if (casa != null) renderersCasa.AddRange(casa.GetComponentsInChildren<Renderer>(true));
        foreach (var r in renderersMaceta) sombrasOriginales[r] = r.shadowCastingMode;
        foreach (var r in renderersCasa) sombrasOriginales[r] = r.shadowCastingMode;

        if (Application.isMobilePlatform && !desenfoqueEnMovil) usarDesenfoque = false;
        if (usarDesenfoque) CrearVolumen();
    }

    void CrearVolumen()
    {
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        dof = perfil.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.highQualitySampling.Override(false); // a media resolución: más barato
        dof.gaussianMaxRadius.Override(radioDesenfoque);

        volumen = gameObject.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = 10f;
        volumen.weight = 0f;
        volumen.sharedProfile = perfil;
        volumen.enabled = false;
    }

    void Update()
    {
        if (volumen == null) return;
        volumen.weight = Mathf.MoveTowards(volumen.weight, pesoObjetivo, velocidadTransicion * Time.deltaTime);
        volumen.enabled = volumen.weight > 0f || pesoObjetivo > 0f;
    }

    /// <summary>Enfoca los objetivos vistos desde "posicionCamara" y baja el detalle del resto.</summary>
    public void Enfocar(Vector3 posicionCamara, params Transform[] objetivos)
    {
        var enFoco = new HashSet<Renderer>();
        bool hayLimites = false;
        Bounds limites = default;
        foreach (var t in objetivos)
        {
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                enFoco.Add(r);
                if (!hayLimites) { limites = r.bounds; hayLimites = true; }
                else limites.Encapsulate(r.bounds);
            }
        }

        // 1. Mallas: alta resolución solo en lo enfocado
        foreach (var p in partes)
        {
            if (p.filtro == null) continue;
            bool foco = enFoco.Contains(p.filtro.GetComponent<Renderer>());
            p.filtro.sharedMesh = foco || p.mallaBaja == null ? p.mallaAlta : p.mallaBaja;
        }

        // 2. Sombras: solo las proyecta lo enfocado
        foreach (var r in renderersMaceta)
            if (r != null) r.shadowCastingMode = enFoco.Contains(r) ? sombrasOriginales[r] : ShadowCastingMode.Off;
        foreach (var r in renderersCasa)
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;

        // 3. Desenfoque de todo lo que queda detrás del objeto enfocado
        if (dof != null && hayLimites)
        {
            float inicio = Vector3.Distance(posicionCamara, limites.center) + limites.extents.magnitude + margenDesenfoque;
            dof.gaussianStart.Override(inicio);
            dof.gaussianEnd.Override(inicio + rangoDesenfoque);
            pesoObjetivo = 1f;
        }
    }

    /// <summary>Vuelve todo a máxima calidad.</summary>
    public void QuitarEnfoque()
    {
        foreach (var p in partes)
            if (p.filtro != null && p.mallaAlta != null) p.filtro.sharedMesh = p.mallaAlta;
        foreach (var kv in sombrasOriginales)
            if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
        pesoObjetivo = 0f;
    }

    void OnDisable() => QuitarEnfoque();

#if UNITY_EDITOR
    /// <summary>Busca en el FBX original las mallas del grupo LOD2 que corresponden a cada parte.</summary>
    [ContextMenu("Llenar mallas de baja resolución desde el FBX")]
    public void LlenarDesdeFBX()
    {
        partes.Clear();
        if (maceta == null) return;
        var fuente = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(maceta.gameObject);
        if (fuente == null) return;

        var bajas = new Dictionary<string, Mesh>();
        foreach (var mf in fuente.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.transform.parent == null || !mf.transform.parent.name.Contains("LOD2")) continue;
            bajas[QuitarSufijo(mf.name)] = mf.sharedMesh;
        }

        string rutaFBX = UnityEditor.AssetDatabase.GetAssetPath(fuente);
        int trisAlta = 0, trisBaja = 0;
        foreach (var mf in maceta.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            // Mallas personalizadas (panel solar, plantas con color): usan su versión "_Baja" si existe;
            // si no (panel solar) se quedan siempre en alta resolución.
            string rutaMalla = UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (rutaMalla != rutaFBX)
            {
                string rutaBaja = rutaMalla.Substring(0, rutaMalla.LastIndexOf('/') + 1) + mf.sharedMesh.name + "_Baja.asset";
                var bajaPropia = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(rutaBaja);
                if (bajaPropia != null)
                {
                    partes.Add(new ParteLOD { filtro = mf, mallaBaja = bajaPropia });
                    trisAlta += mf.sharedMesh.triangles.Length / 3;
                    trisBaja += bajaPropia.triangles.Length / 3;
                }
                continue;
            }
            if (!bajas.TryGetValue(QuitarSufijo(mf.name), out var baja) || baja == null || baja == mf.sharedMesh) continue;
            partes.Add(new ParteLOD { filtro = mf, mallaBaja = baja });
            trisAlta += mf.sharedMesh.triangles.Length / 3;
            trisBaja += baja.triangles.Length / 3;
        }
        Debug.Log($"DetalleEnfoque: {partes.Count} partes con LOD. Triángulos alta = {trisAlta}, baja = {trisBaja}.");
    }

    static string QuitarSufijo(string nombre) => Regex.Replace(nombre, @"\.\d+$", "");
#endif
}
