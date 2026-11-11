using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Menú "Maceta > Crear Canvas en la Jerarquía".
/// Construye la interfaz con:
///   1. Pantalla Inicial con "Bienvenido a la Maceta Inteligente" y botón "Inspeccionar".
///   2. Panel inferior con los botones de zoom:
///       - 🌿 Zoom Plantas (acercamiento MUCHO mayor al follaje, apenas se ven los bordes de la maceta).
///       - ☀️ Luz Solar (cámara desde abajo mirando hacia arriba - conservada).
///       - 💧 Botella de Agua (zoom cercano directo a la botella de agua).
///       - 🔄 Vista General (regresar al encuadre panorámico).
///   3. Ventana Informativa lateral derecha con textos grandes legibles y título sin desbordarse.
/// </summary>
public static class CrearCanvasMaceta
{
    [MenuItem("Maceta/Crear Canvas en la Jerarquía")]
    public static void Crear()
    {
        if (!EditorSceneManager.GetActiveScene().isLoaded || string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }

        // ── 1. Asegurar o crear ControlMaceta ──
        var control = Object.FindAnyObjectByType<ControlMaceta>();
        if (control == null)
        {
            var goControl = new GameObject("ControlMaceta");
            Undo.RegisterCreatedObjectUndo(goControl, "Crear ControlMaceta");
            control = goControl.AddComponent<ControlMaceta>();
        }

        control.camara = Camera.main;
        if (control.camara != null)
        {
            control.camara.nearClipPlane = 0.3f; // Restaurado a 0.3f estándar
            EditorUtility.SetDirty(control.camara);
        }
        control.nombrePlantas = "03_Plantas_Follaje";
        control.nombreLuz = "07B_Plafon_Luz_Crecimiento";
        control.nombrePanelSolar = "07A_Panel_Solar_Autosuficiente";
        control.nombreAgua = "07A_Panel_Solar_Autosuficiente";
        control.margenPlantas = 0.42f; // Alejada un poquito más para verlas con más amplitud
        control.margenPanelSolar = 0.35f; // Enfoque cenital al panel solar cubriendo la maceta
        control.margenAgua = 0.35f;
        control.velocidadCamara = 5f;
        control.BuscarReferencias();

        // ── 2. Asegurar o recrear el Canvas ──
        var canvasGO = GameObject.Find("Canvas_Maceta");
        if (canvasGO != null)
        {
            Undo.DestroyObjectImmediate(canvasGO);
        }

        canvasGO = new GameObject("Canvas_Maceta", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Crear Canvas Maceta");

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ── 3. Pantalla Inicial (Bienvenida) ──
        var panelInicial = new GameObject("Panel_Inicial", typeof(RectTransform), typeof(Image));
        panelInicial.transform.SetParent(canvasGO.transform, false);
        var rtIni = panelInicial.GetComponent<RectTransform>();
        rtIni.anchorMin = Vector2.zero;
        rtIni.anchorMax = Vector2.one;
        rtIni.offsetMin = rtIni.offsetMax = Vector2.zero;
        panelInicial.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        control.panelInicial = panelInicial;

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
        txtTit.font = ObtenerFuente();
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
        var btnInsp = goBtnInsp.GetComponent<Button>();
        var coloresInsp = btnInsp.colors;
        coloresInsp.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        coloresInsp.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btnInsp.colors = coloresInsp;
        UnityEventTools.AddPersistentListener(btnInsp.onClick, control.ComenzarInspeccion);
        control.botonInspeccionar = btnInsp;

        var goTxtInsp = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        goTxtInsp.transform.SetParent(goBtnInsp.transform, false);
        var rtTxtInsp = goTxtInsp.GetComponent<RectTransform>();
        rtTxtInsp.anchorMin = Vector2.zero;
        rtTxtInsp.anchorMax = Vector2.one;
        rtTxtInsp.offsetMin = rtTxtInsp.offsetMax = Vector2.zero;
        var txtInsp = goTxtInsp.GetComponent<Text>();
        txtInsp.text = "Inspeccionar";
        txtInsp.font = ObtenerFuente();
        txtInsp.fontSize = 28;
        txtInsp.fontStyle = FontStyle.Bold;
        txtInsp.alignment = TextAnchor.MiddleCenter;
        txtInsp.color = Color.white;

        // ── 4. Ventana Informativa Flotante (AMPLIADA Y CON TEXTOS GRANDES) ──
        var ventanaInfo = new GameObject("Ventana_Informativa", typeof(RectTransform), typeof(Image));
        ventanaInfo.transform.SetParent(canvasGO.transform, false);
        var rtVInfo = ventanaInfo.GetComponent<RectTransform>();
        rtVInfo.anchorMin = new Vector2(1f, 0.5f);
        rtVInfo.anchorMax = new Vector2(1f, 0.5f);
        rtVInfo.pivot = new Vector2(1f, 0.5f);
        rtVInfo.anchoredPosition = new Vector2(-40, 30);
        rtVInfo.sizeDelta = new Vector2(540, 400);
        ventanaInfo.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        control.ventanaInfo = ventanaInfo;

        // Botón Cerrar en tarjeta
        var goBtnCerrar = new GameObject("Boton_Cerrar", typeof(RectTransform), typeof(Image), typeof(Button));
        goBtnCerrar.transform.SetParent(ventanaInfo.transform, false);
        var rtBtnCerrar = goBtnCerrar.GetComponent<RectTransform>();
        rtBtnCerrar.anchorMin = new Vector2(1f, 1f);
        rtBtnCerrar.anchorMax = new Vector2(1f, 1f);
        rtBtnCerrar.pivot = new Vector2(1f, 1f);
        rtBtnCerrar.anchoredPosition = new Vector2(-15, -15);
        rtBtnCerrar.sizeDelta = new Vector2(40, 40);
        goBtnCerrar.GetComponent<Image>().color = new Color(0.25f, 0.30f, 0.38f, 0.85f);
        var btnCerrar = goBtnCerrar.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(btnCerrar.onClick, control.CerrarVentanaInfo);

        var goTxtCerrar = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        goTxtCerrar.transform.SetParent(goBtnCerrar.transform, false);
        var rtTxtCerrar = goTxtCerrar.GetComponent<RectTransform>();
        rtTxtCerrar.anchorMin = Vector2.zero;
        rtTxtCerrar.anchorMax = Vector2.one;
        rtTxtCerrar.offsetMin = rtTxtCerrar.offsetMax = Vector2.zero;
        var txtCerrar = goTxtCerrar.GetComponent<Text>();
        txtCerrar.text = "✕";
        txtCerrar.font = ObtenerFuente();
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
        var txtTitInfo = goTitInfo.GetComponent<Text>();
        txtTitInfo.text = "TÍTULO INFORMATIVO";
        txtTitInfo.font = ObtenerFuente();
        txtTitInfo.fontSize = 24;
        txtTitInfo.fontStyle = FontStyle.Bold;
        txtTitInfo.alignment = TextAnchor.MiddleLeft;
        txtTitInfo.color = new Color(0.35f, 0.88f, 0.55f);
        control.textoTituloInfo = txtTitInfo;

        // Descripción de la tarjeta (LETRA GRANDE, SIN SOBRESALIR)
        var goDescInfo = new GameObject("Texto_Desc_Info", typeof(RectTransform), typeof(Text));
        goDescInfo.transform.SetParent(ventanaInfo.transform, false);
        var rtDescInfo = goDescInfo.GetComponent<RectTransform>();
        rtDescInfo.anchorMin = new Vector2(0f, 0.28f);
        rtDescInfo.anchorMax = new Vector2(1f, 0.82f);
        rtDescInfo.offsetMin = new Vector2(25, 0);
        rtDescInfo.offsetMax = new Vector2(-25, 0);
        var txtDescInfo = goDescInfo.GetComponent<Text>();
        txtDescInfo.text = "Descripción del componente...";
        txtDescInfo.font = ObtenerFuente();
        txtDescInfo.fontSize = 22;
        txtDescInfo.fontStyle = FontStyle.Normal;
        txtDescInfo.alignment = TextAnchor.UpperLeft;
        txtDescInfo.lineSpacing = 1.25f;
        txtDescInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
        txtDescInfo.verticalOverflow = VerticalWrapMode.Truncate;
        txtDescInfo.color = new Color(0.95f, 0.97f, 1f);
        control.textoDescripcionInfo = txtDescInfo;

        // Contenedor del medidor de agua
        var contenedorAgua = new GameObject("Contenedor_Nivel_Agua", typeof(RectTransform));
        contenedorAgua.transform.SetParent(ventanaInfo.transform, false);
        var rtContAgua = contenedorAgua.GetComponent<RectTransform>();
        rtContAgua.anchorMin = new Vector2(0f, 0f);
        rtContAgua.anchorMax = new Vector2(1f, 0.24f);
        rtContAgua.offsetMin = new Vector2(25, 18);
        rtContAgua.offsetMax = new Vector2(-25, 0);
        control.contenedorNivelAgua = contenedorAgua;

        var goBarraFondo = new GameObject("Fondo_Barra", typeof(RectTransform), typeof(Image));
        goBarraFondo.transform.SetParent(contenedorAgua.transform, false);
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
        var imgFill = goBarraFill.GetComponent<Image>();
        imgFill.color = new Color(0.10f, 0.65f, 0.95f);
        imgFill.type = Image.Type.Filled;
        imgFill.fillMethod = Image.FillMethod.Horizontal;
        imgFill.fillAmount = 0.85f;
        control.barraNivelAgua = imgFill;

        var goTxtNivel = new GameObject("Texto_Nivel", typeof(RectTransform), typeof(Text));
        goTxtNivel.transform.SetParent(contenedorAgua.transform, false);
        var rtTxtNivel = goTxtNivel.GetComponent<RectTransform>();
        rtTxtNivel.anchorMin = new Vector2(0f, 0f);
        rtTxtNivel.anchorMax = new Vector2(1f, 0.44f);
        rtTxtNivel.offsetMin = rtTxtNivel.offsetMax = Vector2.zero;
        var txtNivel = goTxtNivel.GetComponent<Text>();
        txtNivel.text = "Nivel de Agua: 85% (850 ml / 1000 ml) • Óptimo";
        txtNivel.font = ObtenerFuente();
        txtNivel.fontSize = 17;
        txtNivel.fontStyle = FontStyle.Bold;
        txtNivel.alignment = TextAnchor.MiddleLeft;
        txtNivel.color = new Color(0.68f, 0.88f, 1f);
        control.textoNivelAgua = txtNivel;

        // ── 5. Panel Inferior con los 4 Botones ──
        var panelBotones = new GameObject("Panel_Botones", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        panelBotones.transform.SetParent(canvasGO.transform, false);
        var rtPanel = panelBotones.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.5f, 0f);
        rtPanel.anchorMax = new Vector2(0.5f, 0f);
        rtPanel.pivot = new Vector2(0.5f, 0f);
        rtPanel.anchoredPosition = new Vector2(0, 40);
        rtPanel.sizeDelta = new Vector2(1100, 120);
        panelBotones.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        control.panelBotones = panelBotones;

        var layout = panelBotones.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 16, 16);
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        control.botonPlantas = CrearBoton(panelBotones.transform, "Boton_Plantas", "🌿 Zoom Plantas", new Color(0.30f, 0.69f, 0.31f), control.ZoomPlantas);
        control.botonLuz = CrearBoton(panelBotones.transform, "Boton_Luz", "☀️ Luz Solar", new Color(0.95f, 0.65f, 0.15f), control.ZoomLuzSolar);
        control.botonPanelSolar = CrearBoton(panelBotones.transform, "Boton_PanelSolar", "⚡ Panel Solar", new Color(0.12f, 0.32f, 0.68f), control.ZoomPanelSolar);
        control.botonAgua = control.botonPanelSolar;
        control.botonGeneral = CrearBoton(panelBotones.transform, "Boton_General", "🔄 Vista General", new Color(0.40f, 0.45f, 0.55f), control.VistaGeneral);

        // ── 6. EventSystem ──
        var eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(esGO, "Crear EventSystem");
        }

        // Estado inicial de visibilidad
        panelInicial.SetActive(true);
        panelBotones.SetActive(false);
        ventanaInfo.SetActive(false);

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("✔ Canvas en Jerarquía actualizado: Botón de Panel Solar configurado con vista cenital que cubre la maceta.");
    }

    static Button CrearBoton(Transform padre, string nombreGO, string texto, Color colorFondo, UnityAction accion)
    {
        var go = new GameObject(nombreGO, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = colorFondo;

        var boton = go.GetComponent<Button>();
        var colores = boton.colors;
        colores.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        boton.colors = colores;
        UnityEventTools.AddPersistentListener(boton.onClick, accion);

        var goTexto = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        goTexto.transform.SetParent(go.transform, false);
        var rt = goTexto.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var t = goTexto.GetComponent<Text>();
        t.text = texto;
        t.font = ObtenerFuente();
        t.fontSize = 24;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;

        return boton;
    }

    static Font ObtenerFuente()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
