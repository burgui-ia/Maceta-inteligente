using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menú "Maceta > Actualizar pantalla de inicio": rediseña la tarjeta de bienvenida
/// (Canvas_Maceta / Panel_Inicial / Cuadro_Centro) con el logo de la Universidad Marista,
/// una mini introducción del proyecto y el nombre del autor.
/// El botón "Inspeccionar" se conserva (mismo objeto y misma ruta, así ControlMaceta lo sigue
/// encontrando) y el panel de botones de zoom NO se toca.
/// </summary>
public static class ActualizarPantallaInicio
{
    const string RutaLogo = "Assets/Imagenes/LogoMarista.png";

    // Colores
    static readonly Color Fondo = new Color(0.08f, 0.10f, 0.14f, 0.96f);
    static readonly Color Dorado = new Color(0.91f, 0.73f, 0.18f);
    static readonly Color Blanco = new Color(0.97f, 0.97f, 0.95f);
    static readonly Color Suave = new Color(0.75f, 0.83f, 0.78f);
    static readonly Color Gris = new Color(0.60f, 0.65f, 0.70f);

    [MenuItem("Maceta/Actualizar pantalla de inicio")]
    public static void Actualizar()
    {
        var canvas = GameObject.Find("Canvas_Maceta");
        var panel = canvas != null ? canvas.transform.Find("Panel_Inicial") : null;
        if (panel == null) { Debug.LogError("No se encontró Canvas_Maceta/Panel_Inicial en la escena."); return; }

        var cuadro = panel.Find("Cuadro_Centro");
        if (cuadro == null) { Debug.LogError("No se encontró Panel_Inicial/Cuadro_Centro."); return; }

        // Conservar el botón Inspeccionar; borrar todo lo demás de la tarjeta
        var boton = cuadro.Find("Boton_Inspeccionar");
        for (int i = cuadro.childCount - 1; i >= 0; i--)
        {
            var hijo = cuadro.GetChild(i);
            if (hijo != boton) Undo.DestroyObjectImmediate(hijo.gameObject);
        }

        var redondeado = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ── Tarjeta ──
        var rtCuadro = (RectTransform)cuadro;
        Undo.RecordObject(rtCuadro, "Pantalla de inicio");
        rtCuadro.anchorMin = rtCuadro.anchorMax = rtCuadro.pivot = new Vector2(0.5f, 0.5f);
        rtCuadro.anchoredPosition = Vector2.zero;
        rtCuadro.sizeDelta = new Vector2(1280, 640);
        var imgCuadro = cuadro.GetComponent<Image>();
        Undo.RecordObject(imgCuadro, "Pantalla de inicio");
        imgCuadro.color = Fondo;
        imgCuadro.sprite = redondeado;
        imgCuadro.type = Image.Type.Sliced;
        imgCuadro.pixelsPerUnitMultiplier = 0.4f; // esquinas más redondas

        // ── Columna izquierda: logo sobre fondo blanco ──
        var fondoLogo = CrearImagen(cuadro, "Logo_Fondo", new Vector2(40, -40), new Vector2(320, 560), Color.white, redondeado);
        fondoLogo.pixelsPerUnitMultiplier = 0.4f;
        var logo = CrearImagen(fondoLogo.transform, "Logo_Marista", new Vector2(30, -85), new Vector2(260, 390), Color.white, CargarLogo());
        logo.preserveAspect = true;

        // ── Columna derecha: textos ──
        float x = 410, ancho = 830;
        CrearTexto(cuadro, "Texto_Universidad", "UNIVERSIDAD MARISTA DE MÉRIDA  ·  GRÁFICOS POR COMPUTADORA",
            20, FontStyle.Bold, Dorado, new Vector2(x, -52), new Vector2(ancho, 30));
        CrearTexto(cuadro, "Texto_Titulo", "Maceta Inteligente",
            64, FontStyle.Bold, Blanco, new Vector2(x, -88), new Vector2(ancho, 84));
        CrearTexto(cuadro, "Texto_Subtitulo", "Una maceta autosuficiente que cuida sola a tus plantas",
            26, FontStyle.Italic, Suave, new Vector2(x, -176), new Vector2(ancho, 40));
        CrearTexto(cuadro, "Texto_Introduccion",
            "Este proyecto presenta el modelado y la simulación 3D de una maceta inteligente. " +
            "Con la energía de su panel solar, la maceta riega, ilumina y nutre a las plantas de forma automática.\n\n" +
            "Presiona «Inspeccionar» para recorrer sus partes: las plantas, la luz de crecimiento y el panel solar.",
            22, FontStyle.Normal, Blanco, new Vector2(x, -236), new Vector2(ancho, 170));

        CrearImagen(cuadro, "Separador", new Vector2(x, -420), new Vector2(ancho, 2), new Color(1f, 1f, 1f, 0.15f), null);

        CrearTexto(cuadro, "Texto_Autor", "Presentado por: Esteban Burgos Gómez",
            26, FontStyle.Bold, Blanco, new Vector2(x, -438), new Vector2(ancho, 36));
        CrearTexto(cuadro, "Texto_Semestre", "5.º semestre  ·  Gráficos por Computadora",
            20, FontStyle.Normal, Gris, new Vector2(x, -476), new Vector2(ancho, 30));

        // ── Botón Inspeccionar (el mismo de antes, solo se reacomoda) ──
        if (boton != null)
        {
            var rtBoton = (RectTransform)boton;
            Undo.RecordObject(rtBoton, "Pantalla de inicio");
            rtBoton.anchorMin = rtBoton.anchorMax = new Vector2(0f, 0f);
            rtBoton.pivot = new Vector2(0f, 0f);
            rtBoton.anchoredPosition = new Vector2(x, 40);
            rtBoton.sizeDelta = new Vector2(320, 70);
            var imgBoton = boton.GetComponent<Image>();
            if (imgBoton != null)
            {
                Undo.RecordObject(imgBoton, "Pantalla de inicio");
                imgBoton.sprite = redondeado;
                imgBoton.type = Image.Type.Sliced;
            }
            boton.SetAsLastSibling();
        }
        else
        {
            Debug.LogWarning("No había botón Boton_Inspeccionar en la tarjeta; ControlMaceta lo crea si falta el Canvas.");
        }

        CrearTexto(cuadro, "Texto_Controles", "En computadora: mueve la cámara con W A S D o las flechas",
            18, FontStyle.Italic, Gris, new Vector2(x + 345, -560), new Vector2(ancho - 345, 50));

        EditorSceneManager.MarkSceneDirty(cuadro.gameObject.scene);
        Selection.activeGameObject = cuadro.gameObject;
        Debug.Log("Pantalla de inicio actualizada con el logo, la introducción y tu nombre. Guarda la escena con Ctrl+S.");
    }

    static Sprite CargarLogo()
    {
        var imp = AssetImporter.GetAtPath(RutaLogo) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"No se encontró {RutaLogo}."); return null; }
        if (imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RutaLogo);
    }

    /// Imagen anclada arriba a la izquierda del padre (pos.y negativo = hacia abajo).
    static Image CrearImagen(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color, Sprite sprite)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Pantalla de inicio");
        go.transform.SetParent(padre, false);
        Colocar((RectTransform)go.transform, pos, tam);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            if (sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        }
        return img;
    }

    static Text CrearTexto(Transform padre, string nombre, string texto, int tamano, FontStyle estilo, Color color, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        Undo.RegisterCreatedObjectUndo(go, "Pantalla de inicio");
        go.transform.SetParent(padre, false);
        Colocar((RectTransform)go.transform, pos, tam);
        var t = go.GetComponent<Text>();
        t.text = texto;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tamano;
        t.fontStyle = estilo;
        t.color = color;
        t.alignment = TextAnchor.UpperLeft;
        t.lineSpacing = 1.1f;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Colocar(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }
}
