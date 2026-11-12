using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// En la Vista General permite mover la cámara alrededor de la maceta con el teclado:
///   A / D  o  ← / →   girar alrededor de la maceta
///   W / S  o  ↑ / ↓   acercarse / alejarse
///   Q / E             bajar / subir
///   R                 regresar a la vista inicial
/// La cámara nunca atraviesa la maceta ni sale de la casa.
/// ControlMaceta lo agrega solo; el movimiento se suaviza con su mismo Lerp.
/// </summary>
[RequireComponent(typeof(ControlMaceta))]
public class CamaraTeclado : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadGiro = 70f;        // grados por segundo
    public float velocidadZoom = 1.5f;       // metros por segundo
    public float velocidadAltura = 45f;      // grados por segundo

    [Header("Límites")]
    public float distanciaMinima = 1.2f;
    public float distanciaMaxima = 4f;
    [Range(-10f, 85f)] public float anguloMinimo = 0f;
    [Range(-10f, 85f)] public float anguloMaximo = 65f;
    [Tooltip("Separación mínima entre la cámara y las paredes de la casa.")]
    public float margenParedes = 0.3f;

    ControlMaceta control;
    Vector3 centro;
    float giro, altura, distancia;
    float giroInicial, alturaInicial, distanciaInicial;
    Bounds limitesCasa;
    bool hayCasa, listo;

    void Awake() => control = GetComponent<ControlMaceta>();

    // Se inicializa en el primer Update, cuando ControlMaceta ya guardó la vista original
    void Inicializar()
    {
        centro = CentroMaceta();
        Vector3 offset = control.PosicionVistaGeneral - centro;
        distancia = offset.magnitude;
        giro = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        altura = Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(distancia, 0.001f), -1f, 1f)) * Mathf.Rad2Deg;
        distanciaMaxima = Mathf.Max(distanciaMaxima, distancia);
        giroInicial = giro; alturaInicial = altura; distanciaInicial = distancia;

        // Límites del escenario (patio exterior; o la casa de versiones anteriores)
        var casa = GameObject.Find("Patio_Estructura");
        if (casa == null) casa = GameObject.Find("Casa_Estructura");
        if (casa != null && casa.TryGetComponent<Renderer>(out var r))
        {
            limitesCasa = r.bounds;
            limitesCasa.Expand(-2f * margenParedes);
            hayCasa = true;
        }
        listo = true;
    }

    void Update()
    {
        if (control.camara == null) return;
        if (!listo) Inicializar();

        var kb = Keyboard.current;
        if (kb == null || control.modoActual != ControlMaceta.ModoCamara.General) return;

        float h = Eje(kb.dKey.isPressed || kb.rightArrowKey.isPressed, kb.aKey.isPressed || kb.leftArrowKey.isPressed);
        float v = Eje(kb.wKey.isPressed || kb.upArrowKey.isPressed, kb.sKey.isPressed || kb.downArrowKey.isPressed);
        float y = Eje(kb.eKey.isPressed, kb.qKey.isPressed);
        bool reiniciar = kb.rKey.wasPressedThisFrame;

        if (h == 0f && v == 0f && y == 0f && !reiniciar) return;

        if (reiniciar)
        {
            giro = giroInicial; altura = alturaInicial; distancia = distanciaInicial;
        }
        else
        {
            float dt = Time.deltaTime;
            giro -= h * velocidadGiro * dt;
            altura = Mathf.Clamp(altura + y * velocidadAltura * dt, anguloMinimo, anguloMaximo);
            distancia = Mathf.Clamp(distancia - v * velocidadZoom * dt, distanciaMinima, distanciaMaxima);
        }

        Quaternion orbita = Quaternion.Euler(altura, giro, 0f);
        Vector3 pos = centro + orbita * Vector3.forward * distancia;

        // No salir de la casa
        if (hayCasa)
        {
            pos.x = Mathf.Clamp(pos.x, limitesCasa.min.x, limitesCasa.max.x);
            pos.y = Mathf.Clamp(pos.y, limitesCasa.min.y, limitesCasa.max.y);
            pos.z = Mathf.Clamp(pos.z, limitesCasa.min.z, limitesCasa.max.z);
        }

        control.FijarVistaGeneral(pos, Quaternion.LookRotation(centro - pos));
    }

    static float Eje(bool positivo, bool negativo) => (positivo ? 1f : 0f) - (negativo ? 1f : 0f);

    Vector3 CentroMaceta()
    {
        if (control.maceta != null)
        {
            var rs = control.maceta.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                return b.center;
            }
        }
        return control.camara.transform.position + control.camara.transform.forward * 2.5f;
    }
}
