using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Controlador principal de la Maceta Inteligente.
/// Flujo:
///  1. Pantalla Inicial con "Bienvenido a la Maceta Inteligente" y botón "Inspeccionar".
///  2. Al presionar "Inspeccionar", se muestran los botones de zoom:
///       - 🌿 Zoom Plantas: Acercamiento con buena amplitud al follaje (sin cortar flores).
///       - ☀️ Luz Solar: Vista contrapicada desde abajo en las plantas hacia arriba (intacta).
///       - ⚡ Panel Solar: Vista cenital desde arriba del panel solar; por ángulo de visión el panel cubre toda la maceta.
///       - 🔄 Vista General: Regresa a la vista panorámica y oculta la ventana informativa.
///  3. Ventana informativa ampliada con textos grandes y título sin desbordamiento.
/// </summary>
public class ControlMaceta : MonoBehaviour
{
    public enum ModoCamara
    {
        General = 0,
        Plantas = 1,
        LuzSolar = 2,
        PanelSolar = 3,
        Agua = 3 // retrocompatibilidad
    }

    [Header("Referencias de Paneles")]
    public GameObject panelInicial;
    public GameObject panelBotones;
    public Button botonInspeccionar;

    [Header("Ventana Informativa Flotante")]
    public GameObject ventanaInfo;
    public Text textoTituloInfo;
    public Text textoDescripcionInfo;
    public GameObject contenedorNivelAgua;
    public Image barraNivelAgua;
    public Text textoNivelAgua;

    [Header("Cámara y Maceta")]
    public Camera camara;
    public Transform maceta;

    [Header("Objetivos 3D (Se buscan automáticamente)")]
    public Transform objetivoPlantas;
    public Transform objetivoLuz;
    public Transform objetivoPanelSolar;
    [HideInInspector] public Transform objetivoAgua;

    [Header("Nombres de las partes del modelo 3D")]
    public string nombrePlantas = "03_Plantas_Follaje";
    public string nombreLuz = "07B_Plafon_Luz_Crecimiento";
    public string nombrePanelSolar = "07A_Panel_Solar_Autosuficiente";
    [HideInInspector] public string nombreAgua = "07A_Panel_Solar_Autosuficiente";

    [Header("Ajustes de Zoom")]
    [Range(1f, 15f)]
    public float velocidadCamara = 5f;
    [Tooltip("Margen para encuadre de las plantas con buena amplitud sin cortar ninguna.")]
    public float margenPlantas = 0.42f;
    [Tooltip("Margen para encuadrar el panel solar desde arriba cubriendo la maceta.")]
    public float margenPanelSolar = 0.35f;
    [HideInInspector] public float margenAgua = 0.35f;

    [Header("Botones")]
    public Button botonPlantas;
    public Button botonLuz;
    public Button botonPanelSolar;
    [HideInInspector] public Button botonAgua;
    public Button botonGeneral;

    // Estado interno
    public ModoCamara modoActual = ModoCamara.General;
    private Vector3 posicionOriginal;
    private Quaternion rotacionOriginal;
    private bool posicionOriginalGuardada = false;

    private Vector3 posDestino;
    private Quaternion rotDestino;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInicializarEnPlay()
    {
        if (FindFirstObjectByType<ControlMaceta>() == null)
        {
            var go = new GameObject("ControlMaceta");
            go.AddComponent<ControlMaceta>();
        }
    }

    void Awake()
    {
        margenPlantas = 0.42f;
        margenPanelSolar = 0.35f;
        margenAgua = 0.35f;
        nombrePlantas = "03_Plantas_Follaje";
        nombreLuz = "07B_Plafon_Luz_Crecimiento";
        nombrePanelSolar = "07A_Panel_Solar_Autosuficiente";
        nombreAgua = "07A_Panel_Solar_Autosuficiente";

        BuscarReferencias();
        LimpiarParticulas();
    }

    void Start()
    {
        if (camara == null) camara = Camera.main;
        if (camara != null) camara.nearClipPlane = 0.3f; // Restaurado a 0.3f estándar para evitar cortes indebidos

        if (camara != null && !posicionOriginalGuardada)
        {
            posicionOriginal = camara.transform.position;
            rotacionOriginal = camara.transform.rotation;
            posDestino = posicionOriginal;
            rotDestino = rotacionOriginal;
            posicionOriginalGuardada = true;
        }

        var canvasExistente = FindFirstObjectByType<Canvas>();
        if (canvasExistente == null || panelInicial == null || panelBotones == null || ventanaInfo == null)
        {
            ConstruirCanvasRuntime();
        }

        MostrarPantallaInicial();
    }

    void Update()
    {
        ActualizarMovimientoCamara();
    }

    void LimpiarParticulas()
    {
        var particulas = GameObject.Find("Particulas_Gotas_Agua");
        if (particulas != null) Destroy(particulas);
    }

    public void BuscarReferencias()
    {
        if (camara == null) camara = Camera.main;

        if (maceta == null)
        {
            var goMaceta = GameObject.Find("Maceta_Tarea");
            if (goMaceta == null) goMaceta = GameObject.Find("Maceta_autosuficiente");
            if (goMaceta != null) maceta = goMaceta.transform;
            else
            {
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r.name.Contains("Plantas") || r.name.Contains("Carcasa"))
                    {
                        maceta = r.transform.root;
                        break;
                    }
                }
            }
        }

        // Asignar específicamente los objetos objetivos
        var tPlantas = BuscarTransform(nombrePlantas, "03_Plantas_Follaje", "Plantas", "Follaje");
        if (tPlantas != null) objetivoPlantas = tPlantas;

        var tLuz = BuscarTransform(nombreLuz, "07B_Plafon_Luz_Crecimiento", "Plafon", "Luz");
        if (tLuz != null) objetivoLuz = tLuz;

        // PANEL SOLAR FOTOVOLTAICO: Buscar la cubierta solar superior (07A)
        var tPanel = BuscarTransform(nombrePanelSolar, "07A_Panel_Solar_Autosuficiente", "Panel_Solar", "07A", "Panel");
        if (tPanel == null) tPanel = BuscarTransform(nombreLuz, "07B_Plafon_Luz_Crecimiento", "Plafon");
        if (tPanel != null)
        {
            objetivoPanelSolar = tPanel;
            objetivoAgua = tPanel;
        }
    }

    // ───────────────────────── Flujo de Pantallas ─────────────────────────

    public void MostrarPantallaInicial()
    {
        if (panelInicial != null) panelInicial.SetActive(true);
        if (panelBotones != null) panelBotones.SetActive(false);
        if (ventanaInfo != null) ventanaInfo.SetActive(false);
        VistaGeneral();
    }

    public void ComenzarInspeccion()
    {
        if (panelInicial != null) panelInicial.SetActive(false);
        if (panelBotones != null) panelBotones.SetActive(true);
        if (ventanaInfo != null) ventanaInfo.SetActive(false);
        VistaGeneral();
    }

    // ───────────────────────── Acciones de los Botones ─────────────────────────

    public void ZoomPlantas()
    {
        if (modoActual == ModoCamara.Plantas)
        {
            VistaGeneral();
        }
        else
        {
            CambiarModo(ModoCamara.Plantas);
            MostrarVentanaInfo(
                "🌿 IMPORTANCIA DE LAS PLANTAS",
                "Las plantas son el núcleo biológico de la maceta inteligente.\n\n" +
                "• Fotosíntesis: Convierten la luz en energía vital para crecer.\n" +
                "• Purificación: Filtran el aire absorbiendo CO2 y generando oxígeno.\n" +
                "• Monitoreo: Sensores vigilan su hidratación y nutrición en tiempo real.",
                mostrarMedidor: false
            );
        }
    }

    public void ZoomLuzSolar()
    {
        if (modoActual == ModoCamara.LuzSolar)
        {
            VistaGeneral();
        }
        else
        {
            CambiarModo(ModoCamara.LuzSolar);
            MostrarVentanaInfo(
                "☀️ ILUMINACIÓN SOLAR ACTIVA",
                "Lámpara fotosintética orientada hacia el follaje.\n\n" +
                "• Simulación Solar: Modula la luz pulsando según ciclos solares.\n" +
                "• Estimulación: Favorece la clorofila para que crezcan en interiores sin depender del sol exterior.",
                mostrarMedidor: false
            );
        }
    }

    public void ZoomPanelSolar()
    {
        if (modoActual == ModoCamara.PanelSolar)
        {
            VistaGeneral();
        }
        else
        {
            CambiarModo(ModoCamara.PanelSolar);
            MostrarVentanaInfo(
                "⚡ PANEL SOLAR FOTOVOLTAICO",
                "Módulo fotovoltaico de captación solar montado en la cubierta superior.\n\n" +
                "• Generación Limpia: Convierte la radiación solar en electricidad continua.\n" +
                "• Autosuficiencia Total: Alimenta el microcontrolador, sensores y luces LED.\n" +
                "• Ángulo Cenital: Orientado hacia arriba para captación máxima sin sombras.",
                mostrarMedidor: true,
                textoMedidor: "Eficiencia Solar: 94% (4.7W / 5.0W) • Carga Óptima",
                colorMedidor: new Color(0.95f, 0.72f, 0.12f),
                progresoMedidor: 0.94f
            );
        }
    }

    public void ZoomAgua() => ZoomPanelSolar();

    public void VistaGeneral()
    {
        CambiarModo(ModoCamara.General);
        if (ventanaInfo != null) ventanaInfo.SetActive(false);
    }

    public void CerrarVentanaInfo()
    {
        if (ventanaInfo != null) ventanaInfo.SetActive(false);
    }

    void MostrarVentanaInfo(string titulo, string descripcion, bool mostrarMedidor = false, string textoMedidor = "", Color? colorMedidor = null, float progresoMedidor = 1f, bool mostrarNivelAgua = false)
    {
        if (ventanaInfo == null) return;

        bool mostrar = mostrarMedidor || mostrarNivelAgua;
        ventanaInfo.SetActive(true);

        // La ventana informativa se coloca a la DERECHA para dejar la vista 3D limpia
        var rtV = ventanaInfo.GetComponent<RectTransform>();
        if (rtV != null)
        {
            rtV.anchorMin = new Vector2(1f, 0.5f);
            rtV.anchorMax = new Vector2(1f, 0.5f);
            rtV.pivot = new Vector2(1f, 0.5f);
            rtV.anchoredPosition = new Vector2(-40, 30);
        }

        if (textoTituloInfo != null) textoTituloInfo.text = titulo;
        if (textoDescripcionInfo != null)
        {
            textoDescripcionInfo.text = descripcion;

            var rtDesc = textoDescripcionInfo.rectTransform;
            if (mostrar)
            {
                // Deja suficiente espacio arriba de la barra de medición (sin achocarse)
                rtDesc.anchorMin = new Vector2(0f, 0.28f);
                rtDesc.anchorMax = new Vector2(1f, 0.82f);
                rtDesc.offsetMin = new Vector2(25, 0);
                rtDesc.offsetMax = new Vector2(-25, 0);
            }
            else
            {
                // Vista sin medidor
                rtDesc.anchorMin = new Vector2(0f, 0.08f);
                rtDesc.anchorMax = new Vector2(1f, 0.82f);
                rtDesc.offsetMin = new Vector2(25, 0);
                rtDesc.offsetMax = new Vector2(-25, 0);
            }
        }

        if (contenedorNivelAgua != null)
        {
            contenedorNivelAgua.SetActive(mostrar);
            if (mostrar)
            {
                if (barraNivelAgua != null)
                {
                    barraNivelAgua.fillAmount = progresoMedidor;
                    if (colorMedidor.HasValue) barraNivelAgua.color = colorMedidor.Value;
                }
                if (textoNivelAgua != null && !string.IsNullOrEmpty(textoMedidor))
                {
                    textoNivelAgua.text = textoMedidor;
                }
            }
        }
    }

    public void CambiarModo(ModoCamara nuevoModo)
    {
        modoActual = nuevoModo;
        if (camara != null)
        {
            if (nuevoModo == ModoCamara.PanelSolar)
                camara.nearClipPlane = 0.1f;
            else
                camara.nearClipPlane = 0.3f; // Intacta: 0.3f para Luz Solar, Plantas y General
        }
        CalcularDestinoCamara(nuevoModo, out posDestino, out rotDestino);
    }

    // ───────────────────────── Movimiento de Cámara ─────────────────────────

    void ActualizarMovimientoCamara()
    {
        if (camara == null) return;

        float t = 1f - Mathf.Exp(-velocidadCamara * Time.deltaTime);
        camara.transform.position = Vector3.Lerp(camara.transform.position, posDestino, t);
        camara.transform.rotation = Quaternion.Slerp(camara.transform.rotation, rotDestino, t);
    }

    void CalcularDestinoCamara(ModoCamara modo, out Vector3 posicion, out Quaternion rotacion)
    {
        if (modo == ModoCamara.General)
        {
            posicion = posicionOriginal;
            rotacion = rotacionOriginal;
            return;
        }

        float fovRad = (camara != null ? camara.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;

        // 🌿 1. SÚPER ZOOM A LAS PLANTAS (Alejada un poquito más para verlas con más amplitud sin cortar ninguna)
        if (modo == ModoCamara.Plantas)
        {
            Transform objPlantas = objetivoPlantas != null ? objetivoPlantas : BuscarTransform("03_Plantas_Follaje", "Plantas_Follaje", "Plantas");
            if (objPlantas != null)
            {
                Bounds bPlantas = ObtenerLimites(objPlantas);
                Vector3 centro = bPlantas.center;
                float radio = Mathf.Max(bPlantas.extents.magnitude, 0.15f);

                // Ángulo frontal-superior enfocado en las hojas y flores
                Vector3 dirBase = (posicionOriginal - centro).normalized;
                Vector3 dirFinal = (dirBase + new Vector3(0.04f, 0.22f, 0f)).normalized;

                // Zoom alejado un poquito más (margenPlantas = 0.42f) para dar mayor amplitud
                float distancia = Mathf.Max(0.30f, (radio * margenPlantas) / Mathf.Tan(fovRad));

                posicion = centro + dirFinal * distancia;
                rotacion = Quaternion.LookRotation((centro - posicion).normalized);
                return;
            }
        }

        // ☀️ 2. LUZ SOLAR (Desde las plantas mirando hacia arriba - EXCELENTE)
        if (modo == ModoCamara.LuzSolar)
        {
            Transform objLuz = objetivoLuz != null ? objetivoLuz : BuscarTransform(nombreLuz, "Plafon", "Luz", "07B");
            Transform objPlantas = objetivoPlantas != null ? objetivoPlantas : BuscarTransform(nombrePlantas, "Plantas", "Follaje");

            if (objLuz != null)
            {
                Bounds bLuz = ObtenerLimites(objLuz);
                Vector3 centroLuz = bLuz.center;

                Vector3 origenMirada;
                if (objPlantas != null)
                {
                    Bounds bPlantas = ObtenerLimites(objPlantas);
                    origenMirada = bPlantas.center + (posicionOriginal - centroLuz).normalized * 0.15f;
                    origenMirada.y = bPlantas.center.y - 0.05f;
                }
                else
                {
                    origenMirada = centroLuz - Vector3.up * 0.45f;
                }

                posicion = origenMirada;

                Vector3 dirHaciaArriba = (centroLuz - posicion).normalized;
                Vector3 referenciaUp = (maceta != null ? -maceta.forward : Vector3.back);
                rotacion = Quaternion.LookRotation(dirHaciaArriba, referenciaUp);
                return;
            }
        }

        // ⚡ 3. PANEL SOLAR FOTOVOLTAICO (Vista cenital pura idéntica a la captura de referencia)
        if (modo == ModoCamara.PanelSolar || modo == ModoCamara.Agua)
        {
            Transform objPanel = objetivoPanelSolar != null ? objetivoPanelSolar : BuscarTransform(nombrePanelSolar, "07A_Panel_Solar_Autosuficiente", "Panel_Solar", "07A", "Panel");
            if (objPanel == null) objPanel = BuscarTransform(nombreLuz, "07B_Plafon_Luz_Crecimiento", "Plafon");

            if (objPanel != null)
            {
                Bounds bPanel = ObtenerLimites(objPanel);
                Vector3 centroPanel = bPanel.center;

                // Dimensiones del panel en el plano horizontal (X = ancho horizontal, Z = largo vertical)
                float anchoX = Mathf.Max(bPanel.size.x, 0.20f);
                float largoZ = Mathf.Max(bPanel.size.z, 0.12f);

                float fovRadVertical = (camara != null ? camara.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;
                float aspectCam = (camara != null && camara.aspect > 0.1f) ? camara.aspect : (16f / 9f);

                // Calibración exacta para encuadrar el panel al ~84% del ancho y ~78% del alto exacto como en la captura
                float distPorAncho = (anchoX / 0.84f) / (2f * Mathf.Tan(fovRadVertical) * aspectCam);
                float distPorLargo = (largoZ / 0.78f) / (2f * Mathf.Tan(fovRadVertical));
                float distancia = Mathf.Max(distPorAncho, distPorLargo);
                distancia = Mathf.Clamp(distancia, 0.32f, 1.20f);

                // Vista Cenital (Top-Down): Cámara centrada directamente sobre el centro del panel solar
                posicion = new Vector3(centroPanel.x, centroPanel.y + distancia, centroPanel.z);

                // Rotación: Mirando verticalmente hacia abajo (Pitch 90°), panel horizontal idéntico a la captura
                float anguloY = maceta != null ? maceta.eulerAngles.y : 0f;
                rotacion = Quaternion.Euler(90f, anguloY, 0f);
                return;
            }
        }

        posicion = posicionOriginal;
        rotacion = rotacionOriginal;
    }

    Bounds ObtenerLimites(Transform t)
    {
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }
        return new Bounds(t.position, Vector3.one * 0.2f);
    }

    public Transform BuscarBotellaExterior()
    {
        // 1. Buscar la botella montada al exterior de la maceta ("05_Tanque_Nutrientes_NPK")
        Transform t05 = BuscarTransform("05_Tanque_Nutrientes_NPK", "05_Tanque", "Tanque_Nutrientes", "Tanque");
        if (t05 != null) return t05;

        // 2. Buscar entre todos los transforms de la escena
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            // Omitir el módulo interior 04_
            if (t.name.StartsWith("04_") || t.name.IndexOf("Sistema_Agua", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (t.name.StartsWith("05_") || t.name.IndexOf("Tanque", StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        return objetivoAgua;
    }

    Transform BuscarTransform(params string[] pistas)
    {
        if (maceta != null)
        {
            var enMaceta = BuscarRecursivo(maceta, pistas);
            if (enMaceta != null) return enMaceta;
        }

        Transform[] todos = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in todos)
        {
            foreach (var pista in pistas)
            {
                if (t.name.IndexOf(pista, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
        }
        return null;
    }

    Transform BuscarRecursivo(Transform raiz, string[] pistas)
    {
        foreach (var pista in pistas)
        {
            if (raiz.name.IndexOf(pista, StringComparison.OrdinalIgnoreCase) >= 0)
                return raiz;
        }

        for (int i = 0; i < raiz.childCount; i++)
        {
            var res = BuscarRecursivo(raiz.GetChild(i), pistas);
            if (res != null) return res;
        }
        return null;
    }

    // ───────────────────────── Creación Runtime del Canvas ─────────────────────────

    void ConstruirCanvasRuntime()
    {
        var canvasExistente = GameObject.Find("Canvas_Maceta");
        if (canvasExistente != null)
        {
            VincularElementosCanvas(canvasExistente);
            return;
        }

        var canvasGO = new GameObject("Canvas_Maceta", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 1. Pantalla Inicial (Bienvenida)
        panelInicial = new GameObject("Panel_Inicial", typeof(RectTransform), typeof(Image));
        panelInicial.transform.SetParent(canvasGO.transform, false);
        var rtIni = panelInicial.GetComponent<RectTransform>();
        rtIni.anchorMin = Vector2.zero;
        rtIni.anchorMax = Vector2.one;
        rtIni.offsetMin = rtIni.offsetMax = Vector2.zero;
        panelInicial.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var cuadro = new GameObject("Cuadro_Centro", typeof(RectTransform), typeof(Image));
        cuadro.transform.SetParent(panelInicial.transform, false);
        var rtCuadro = cuadro.GetComponent<RectTransform>();
        rtCuadro.anchorMin = new Vector2(0.5f, 0.5f);
        rtCuadro.anchorMax = new Vector2(0.5f, 0.5f);
        rtCuadro.pivot = new Vector2(0.5f, 0.5f);
        rtCuadro.sizeDelta = new Vector2(800, 360);
        cuadro.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.95f);

        var goTit = new GameObject("Texto_Titulo", typeof(RectTransform), typeof(Text));
        goTit.transform.SetParent(cuadro.transform, false);
        var rtTit = goTit.GetComponent<RectTransform>();
        rtTit.anchorMin = new Vector2(0.5f, 1f);
        rtTit.anchorMax = new Vector2(0.5f, 1f);
        rtTit.pivot = new Vector2(0.5f, 1f);
        rtTit.anchoredPosition = new Vector2(0, -40);
        rtTit.sizeDelta = new Vector2(740, 120);
        var txtTit = goTit.GetComponent<Text>();
        txtTit.text = "Bienvenido a la\nMaceta Inteligente";
        txtTit.font = ObtenerFuenteRuntime();
        txtTit.fontSize = 38;
        txtTit.fontStyle = FontStyle.Bold;
        txtTit.alignment = TextAnchor.MiddleCenter;
        txtTit.color = Color.white;

        var goBtnInsp = new GameObject("Boton_Inspeccionar", typeof(RectTransform), typeof(Image), typeof(Button));
        goBtnInsp.transform.SetParent(cuadro.transform, false);
        var rtBtnInsp = goBtnInsp.GetComponent<RectTransform>();
        rtBtnInsp.anchorMin = new Vector2(0.5f, 0f);
        rtBtnInsp.anchorMax = new Vector2(0.5f, 0f);
        rtBtnInsp.pivot = new Vector2(0.5f, 0f);
        rtBtnInsp.anchoredPosition = new Vector2(0, 40);
        rtBtnInsp.sizeDelta = new Vector2(320, 65);
        goBtnInsp.GetComponent<Image>().color = new Color(0.30f, 0.69f, 0.31f);
        botonInspeccionar = goBtnInsp.GetComponent<Button>();
        botonInspeccionar.onClick.AddListener(ComenzarInspeccion);

        var goTxtInsp = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        goTxtInsp.transform.SetParent(goBtnInsp.transform, false);
        var rtTxtInsp = goTxtInsp.GetComponent<RectTransform>();
        rtTxtInsp.anchorMin = Vector2.zero;
        rtTxtInsp.anchorMax = Vector2.one;
        rtTxtInsp.offsetMin = rtTxtInsp.offsetMax = Vector2.zero;
        var txtInsp = goTxtInsp.GetComponent<Text>();
        txtInsp.text = "Inspeccionar";
        txtInsp.font = ObtenerFuenteRuntime();
        txtInsp.fontSize = 28;
        txtInsp.fontStyle = FontStyle.Bold;
        txtInsp.alignment = TextAnchor.MiddleCenter;
        txtInsp.color = Color.white;

        // 2. Ventana Informativa Flotante
        ventanaInfo = new GameObject("Ventana_Informativa", typeof(RectTransform), typeof(Image));
        ventanaInfo.transform.SetParent(canvasGO.transform, false);
        var rtVInfo = ventanaInfo.GetComponent<RectTransform>();
        rtVInfo.anchorMin = new Vector2(1f, 0.5f);
        rtVInfo.anchorMax = new Vector2(1f, 0.5f);
        rtVInfo.pivot = new Vector2(1f, 0.5f);
        rtVInfo.anchoredPosition = new Vector2(-40, 30);
        rtVInfo.sizeDelta = new Vector2(540, 400);
        ventanaInfo.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

        // Botón Cerrar
        var goBtnCerrar = new GameObject("Boton_Cerrar", typeof(RectTransform), typeof(Image), typeof(Button));
        goBtnCerrar.transform.SetParent(ventanaInfo.transform, false);
        var rtBtnCerrar = goBtnCerrar.GetComponent<RectTransform>();
        rtBtnCerrar.anchorMin = new Vector2(1f, 1f);
        rtBtnCerrar.anchorMax = new Vector2(1f, 1f);
        rtBtnCerrar.pivot = new Vector2(1f, 1f);
        rtBtnCerrar.anchoredPosition = new Vector2(-15, -15);
        rtBtnCerrar.sizeDelta = new Vector2(40, 40);
        goBtnCerrar.GetComponent<Image>().color = new Color(0.25f, 0.30f, 0.38f, 0.85f);
        goBtnCerrar.GetComponent<Button>().onClick.AddListener(CerrarVentanaInfo);

        var goTxtCerrar = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        goTxtCerrar.transform.SetParent(goBtnCerrar.transform, false);
        var rtTxtCerrar = goTxtCerrar.GetComponent<RectTransform>();
        rtTxtCerrar.anchorMin = Vector2.zero;
        rtTxtCerrar.anchorMax = Vector2.one;
        rtTxtCerrar.offsetMin = rtTxtCerrar.offsetMax = Vector2.zero;
        var txtCerrar = goTxtCerrar.GetComponent<Text>();
        txtCerrar.text = "✕";
        txtCerrar.font = ObtenerFuenteRuntime();
        txtCerrar.fontSize = 22;
        txtCerrar.fontStyle = FontStyle.Bold;
        txtCerrar.alignment = TextAnchor.MiddleCenter;
        txtCerrar.color = Color.white;

        // Título de la tarjeta informativa
        var goTitInfo = new GameObject("Texto_Titulo_Info", typeof(RectTransform), typeof(Text));
        goTitInfo.transform.SetParent(ventanaInfo.transform, false);
        var rtTitInfo = goTitInfo.GetComponent<RectTransform>();
        rtTitInfo.anchorMin = new Vector2(0f, 1f);
        rtTitInfo.anchorMax = new Vector2(1f, 1f);
        rtTitInfo.pivot = new Vector2(0f, 1f);
        rtTitInfo.anchoredPosition = new Vector2(25, -20);
        rtTitInfo.sizeDelta = new Vector2(450, 45);
        textoTituloInfo = goTitInfo.GetComponent<Text>();
        textoTituloInfo.text = "TÍTULO INFORMATIVO";
        textoTituloInfo.font = ObtenerFuenteRuntime();
        textoTituloInfo.fontSize = 24;
        textoTituloInfo.fontStyle = FontStyle.Bold;
        textoTituloInfo.alignment = TextAnchor.MiddleLeft;
        textoTituloInfo.color = new Color(0.35f, 0.88f, 0.55f);

        // Descripción de la tarjeta (LETRA GRANDE, SIN SOBRESALIR)
        var goDescInfo = new GameObject("Texto_Desc_Info", typeof(RectTransform), typeof(Text));
        goDescInfo.transform.SetParent(ventanaInfo.transform, false);
        var rtDescInfo = goDescInfo.GetComponent<RectTransform>();
        rtDescInfo.anchorMin = new Vector2(0f, 0.28f);
        rtDescInfo.anchorMax = new Vector2(1f, 0.82f);
        rtDescInfo.offsetMin = new Vector2(25, 0);
        rtDescInfo.offsetMax = new Vector2(-25, 0);
        textoDescripcionInfo = goDescInfo.GetComponent<Text>();
        textoDescripcionInfo.text = "Descripción...";
        textoDescripcionInfo.font = ObtenerFuenteRuntime();
        textoDescripcionInfo.fontSize = 22;
        textoDescripcionInfo.fontStyle = FontStyle.Normal;
        textoDescripcionInfo.alignment = TextAnchor.UpperLeft;
        textoDescripcionInfo.lineSpacing = 1.25f;
        textoDescripcionInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoDescripcionInfo.verticalOverflow = VerticalWrapMode.Truncate;
        textoDescripcionInfo.color = new Color(0.95f, 0.97f, 1f);

        // Medidor de Nivel de Agua
        contenedorNivelAgua = new GameObject("Contenedor_Nivel_Agua", typeof(RectTransform));
        contenedorNivelAgua.transform.SetParent(ventanaInfo.transform, false);
        var rtContAgua = contenedorNivelAgua.GetComponent<RectTransform>();
        rtContAgua.anchorMin = new Vector2(0f, 0f);
        rtContAgua.anchorMax = new Vector2(1f, 0.24f);
        rtContAgua.offsetMin = new Vector2(25, 18);
        rtContAgua.offsetMax = new Vector2(-25, 0);

        var goBarraFondo = new GameObject("Fondo_Barra", typeof(RectTransform), typeof(Image));
        goBarraFondo.transform.SetParent(contenedorNivelAgua.transform, false);
        var rtBF = goBarraFondo.GetComponent<RectTransform>();
        rtBF.anchorMin = new Vector2(0f, 0.48f);
        rtBF.anchorMax = new Vector2(1f, 0.95f);
        rtBF.offsetMin = rtBF.offsetMax = Vector2.zero;
        goBarraFondo.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 0.9f);

        var goBarraFill = new GameObject("Fill_Barra", typeof(RectTransform), typeof(Image));
        goBarraFill.transform.SetParent(goBarraFondo.transform, false);
        var rtBFill = goBarraFill.GetComponent<RectTransform>();
        rtBFill.anchorMin = Vector2.zero;
        rtBFill.anchorMax = Vector2.one;
        rtBFill.offsetMin = rtBFill.offsetMax = Vector2.zero;
        barraNivelAgua = goBarraFill.GetComponent<Image>();
        barraNivelAgua.color = new Color(0.10f, 0.65f, 0.95f);
        barraNivelAgua.type = Image.Type.Filled;
        barraNivelAgua.fillMethod = Image.FillMethod.Horizontal;
        barraNivelAgua.fillAmount = 0.85f;

        var goTxtNivel = new GameObject("Texto_Nivel", typeof(RectTransform), typeof(Text));
        goTxtNivel.transform.SetParent(contenedorNivelAgua.transform, false);
        var rtTxtNivel = goTxtNivel.GetComponent<RectTransform>();
        rtTxtNivel.anchorMin = new Vector2(0f, 0f);
        rtTxtNivel.anchorMax = new Vector2(1f, 0.44f);
        rtTxtNivel.offsetMin = rtTxtNivel.offsetMax = Vector2.zero;
        textoNivelAgua = goTxtNivel.GetComponent<Text>();
        textoNivelAgua.text = "Nivel de Agua: 85% (850 ml / 1000 ml) • Óptimo";
        textoNivelAgua.font = ObtenerFuenteRuntime();
        textoNivelAgua.fontSize = 17;
        textoNivelAgua.fontStyle = FontStyle.Bold;
        textoNivelAgua.alignment = TextAnchor.MiddleLeft;
        textoNivelAgua.color = new Color(0.68f, 0.88f, 1f);

        // 3. Panel Inferior con los 4 Botones
        panelBotones = new GameObject("Panel_Botones", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        panelBotones.transform.SetParent(canvasGO.transform, false);
        var rtPanel = panelBotones.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.5f, 0f);
        rtPanel.anchorMax = new Vector2(0.5f, 0f);
        rtPanel.pivot = new Vector2(0.5f, 0f);
        rtPanel.anchoredPosition = new Vector2(0, 40);
        rtPanel.sizeDelta = new Vector2(1100, 120);
        panelBotones.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var layout = panelBotones.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 16, 16);
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        botonPlantas = CrearBotonSimpleRuntime(panelBotones.transform, "Boton_Plantas", "🌿 Zoom Plantas", new Color(0.30f, 0.69f, 0.31f), ZoomPlantas);
        botonLuz = CrearBotonSimpleRuntime(panelBotones.transform, "Boton_Luz", "☀️ Luz Solar", new Color(0.95f, 0.65f, 0.15f), ZoomLuzSolar);
        botonPanelSolar = CrearBotonSimpleRuntime(panelBotones.transform, "Boton_PanelSolar", "⚡ Panel Solar", new Color(0.12f, 0.32f, 0.68f), ZoomPanelSolar);
        botonAgua = botonPanelSolar;
        botonGeneral = CrearBotonSimpleRuntime(panelBotones.transform, "Boton_General", "🔄 Vista General", new Color(0.40f, 0.45f, 0.55f), VistaGeneral);

        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        panelInicial.SetActive(true);
        panelBotones.SetActive(false);
        ventanaInfo.SetActive(false);
    }

    void VincularElementosCanvas(GameObject canvasGO)
    {
        panelInicial = canvasGO.transform.Find("Panel_Inicial")?.gameObject;
        panelBotones = canvasGO.transform.Find("Panel_Botones")?.gameObject;
        ventanaInfo = canvasGO.transform.Find("Ventana_Informativa")?.gameObject;

        if (panelInicial != null)
        {
            botonInspeccionar = panelInicial.transform.Find("Cuadro_Centro/Boton_Inspeccionar")?.GetComponent<Button>();
            if (botonInspeccionar != null)
            {
                botonInspeccionar.onClick.RemoveAllListeners();
                botonInspeccionar.onClick.AddListener(ComenzarInspeccion);
            }
        }

        if (ventanaInfo != null)
        {
            textoTituloInfo = ventanaInfo.transform.Find("Texto_Titulo_Info")?.GetComponent<Text>();
            textoDescripcionInfo = ventanaInfo.transform.Find("Texto_Desc_Info")?.GetComponent<Text>();
            contenedorNivelAgua = ventanaInfo.transform.Find("Contenedor_Nivel_Agua")?.gameObject;
            barraNivelAgua = ventanaInfo.transform.Find("Contenedor_Nivel_Agua/Fondo_Barra/Fill_Barra")?.GetComponent<Image>();
            textoNivelAgua = ventanaInfo.transform.Find("Contenedor_Nivel_Agua/Texto_Nivel")?.GetComponent<Text>();

            var btnCerrar = ventanaInfo.transform.Find("Boton_Cerrar")?.GetComponent<Button>();
            if (btnCerrar != null)
            {
                btnCerrar.onClick.RemoveAllListeners();
                btnCerrar.onClick.AddListener(CerrarVentanaInfo);
            }
        }

        if (panelBotones != null)
        {
            botonPlantas = panelBotones.transform.Find("Boton_Plantas")?.GetComponent<Button>();
            if (botonPlantas != null) { botonPlantas.onClick.RemoveAllListeners(); botonPlantas.onClick.AddListener(ZoomPlantas); }

            botonLuz = panelBotones.transform.Find("Boton_Luz")?.GetComponent<Button>();
            if (botonLuz != null) { botonLuz.onClick.RemoveAllListeners(); botonLuz.onClick.AddListener(ZoomLuzSolar); }

            botonPanelSolar = panelBotones.transform.Find("Boton_PanelSolar")?.GetComponent<Button>();
            if (botonPanelSolar == null) botonPanelSolar = panelBotones.transform.Find("Boton_Agua")?.GetComponent<Button>();
            if (botonPanelSolar != null)
            {
                botonPanelSolar.onClick.RemoveAllListeners();
                botonPanelSolar.onClick.AddListener(ZoomPanelSolar);
                var img = botonPanelSolar.GetComponent<Image>();
                if (img != null) img.color = new Color(0.12f, 0.32f, 0.68f);
                var txt = botonPanelSolar.GetComponentInChildren<Text>();
                if (txt != null) txt.text = "⚡ Panel Solar";
            }
            botonAgua = botonPanelSolar;

            botonGeneral = panelBotones.transform.Find("Boton_General")?.GetComponent<Button>();
            if (botonGeneral != null) { botonGeneral.onClick.RemoveAllListeners(); botonGeneral.onClick.AddListener(VistaGeneral); }
        }
    }

    Button CrearBotonSimpleRuntime(Transform padre, string nombreGO, string texto, Color colorFondo, UnityAction accion)
    {
        var go = new GameObject(nombreGO, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = colorFondo;

        var btn = go.GetComponent<Button>();
        var colores = btn.colors;
        colores.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = colores;
        btn.onClick.AddListener(accion);

        var goTexto = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        goTexto.transform.SetParent(go.transform, false);
        var rt = goTexto.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var t = goTexto.GetComponent<Text>();
        t.text = texto;
        t.font = ObtenerFuenteRuntime();
        t.fontSize = 24;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;

        return btn;
    }

    Font ObtenerFuenteRuntime()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
