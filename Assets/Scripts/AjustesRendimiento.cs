using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Se ejecuta solo al iniciar la app (no hay que ponerlo en ningún objeto).
///
/// En TODAS las plataformas (no cambia nada visual):
///  - Apaga la simulación de física: el proyecto no la usa y Unity la calculaba 50 veces por segundo.
///  - Quita "Raycast Target" de textos e imágenes que no son botones, para que la UI
///    revise menos elementos cada vez que se toca la pantalla.
///
/// Solo en CELULARES:
///  - Gama media (4 GB o más): 60 FPS.
///  - Gama baja (menos de 4 GB): 30 FPS (más estable, menos calor y batería).
///  - Quita el Bloom, el efecto de post-procesado más caro.
///  - Gama MUY baja (menos de 3 GB): resolución al 65 %, sombras hasta 5 m
///    y sin post-procesado (viñeta y tonemapping).
/// </summary>
public static class AjustesRendimiento
{
    const int RamGamaBaja = 4000;     // MB
    const int RamGamaMuyBaja = 3000;  // MB

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Aplicar()
    {
        // ── Todas las plataformas ──
        Physics.simulationMode = SimulationMode.Script;
        // La UI se termina de construir en Start(): se ajusta un frame después
        new GameObject("AjustesRendimiento_UI").AddComponent<AjustarUI>();

        if (!Application.isMobilePlatform) return;

        // ── Celulares ──
        int ram = SystemInfo.systemMemorySize;
        bool gamaBaja = ram < RamGamaBaja;
        bool gamaMuyBaja = ram < RamGamaMuyBaja;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = gamaBaja ? 30 : 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        foreach (var v in Object.FindObjectsByType<Volume>())
        {
            if (v.sharedProfile == null) continue;
            // v.profile crea una copia en tiempo de ejecución: no modifica el asset
            if (v.profile.TryGet<Bloom>(out var bloom)) bloom.active = false;
        }

        if (gamaMuyBaja)
        {
            // Cambios en memoria: en el APK no se guardan en el asset
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = 0.65f;
                urp.shadowDistance = 5f;
            }
            foreach (var cam in Camera.allCameras)
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        string gama = gamaMuyBaja ? "muy baja" : gamaBaja ? "baja" : "media/alta";
        Debug.Log($"AjustesRendimiento: gama {gama}, {Application.targetFrameRate} FPS, RAM {ram} MB.");
    }

    /// Quita Raycast Target de todo lo que no sea un botón u otro control.
    class AjustarUI : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            int quitados = 0;
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsInactive.Include))
            {
                if (!g.raycastTarget || g.GetComponent<Selectable>() != null) continue;
                g.raycastTarget = false;
                quitados++;
            }
            Debug.Log($"AjustesRendimiento: Raycast Target desactivado en {quitados} elementos de UI.");
            Destroy(gameObject);
        }
    }
}
