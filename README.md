# 🌱 Maceta Inteligente
<img width="458" height="320" alt="image" src="https://github.com/user-attachments/assets/36ae3a28-79cb-4d26-a16c-6a86832f95db" />


Proyecto de **Gráficos por Computadora** (5.º semestre) hecho en **Unity**. Es el modelado, texturizado y renderizado en 3D de una **maceta inteligente autosuficiente**: un sistema de cultivo que se riega, se nutre y se alimenta de energía por sí mismo.

---

## 📖 Concepto

La maceta inteligente busca que una planta pueda crecer con la menor intervención humana posible. Junta en un solo diseño los elementos que necesita una planta:

| Componente | Función en el concepto |
|---|---|
| **Panel solar** | Genera la energía que alimenta los sensores, la bomba y la iluminación. |
| **Luz solar / iluminación** | Luz de apoyo para la fotosíntesis cuando no hay suficiente luz natural. |
| **Tanque de agua** | Reserva de agua para el riego automático. |
| **Tanque de nutrientes NPK** | Guarda fertilizante de Nitrógeno (N), Fósforo (P) y Potasio (K) que se suministra a la planta. |
| **Tierra / sustrato** | Medio de cultivo donde crecen las raíces. |
| **Follaje** | La planta que se cultiva. |
| **Carcasa y soporte** | Estructura que protege los componentes y sostiene el sistema. |
| **Botones (3)** | Panel de control para que el usuario interactúe con la maceta. |

---

## 🎨 Lo que incluye el proyecto

### Modelos 3D (`Assets/FBX/`)
- `Maceta_autosuficiente.fbx`: modelo principal de la maceta inteligente.
- `Maceta_Tarea.fbx`: versión del modelo hecha para la tarea.
- `Tanque.fbx`: tanque de agua / nutrientes.
- `Rock Pack Vol. 2.fbx`: rocas decorativas para el entorno.

### Materiales (`Assets/Materiales/`)
Hay un material por cada parte del sistema: `Agua`, `Tierra`, `Follaje_plantas`, `Carcasa`, `Soporte`, `Panel solar`, `Tanque_Nutrientes_NPK`, `Luz_solar` y `Boton_1`, `Boton_2`, `Boton_3`.

### Shader personalizado (`Assets/Shaders/Luz solar.shadergraph`)
Shader Graph animado para la luz de la maceta. Usa el nodo **Time** y una función **Seno** para interpolar (**Lerp**) entre dos colores. El resultado va a la **emisión** del material, así que la luz **pulsa** con el tiempo, como si la lámpara estuviera encendida.

### Escenas (`Assets/Scenes/`)
- `SampleScene.unity`: escena principal con la maceta, iluminación direccional y un *Global Volume* para el post-procesado.
- `Renderizado_camara.unity`: escena para preparar el renderizado desde la cámara.

---

## 🛠️ Tecnologías

- **Unity 6** (`6000.6.3f1`)
- **Universal Render Pipeline (URP)** con perfiles para PC y móvil
- **Shader Graph** para el shader de la luz
- **Input System** de Unity
- Modelos importados en formato **FBX**

---

## 🚀 Cómo abrir el proyecto

1. Instala **Unity Hub** y el editor **Unity 6000.6.3f1** (o una versión compatible de Unity 6).
2. Clona el repositorio:
   ```bash
   git clone https://github.com/burgui-ia/Maceta-inteligente.git
   ```
3. En Unity Hub, entra a **Add → Add project from disk** y selecciona la carpeta clonada.
4. Abre el proyecto. La primera vez Unity regenera la carpeta `Library`, así que puede tardar unos minutos.
5. Abre la escena `Assets/Scenes/SampleScene.unity` y presiona **Play** o revisa la vista de escena.

---

## 📁 Estructura

```
Maceta_inteligente/
├── Assets/
│   ├── FBX/            # Modelos 3D
│   ├── Materiales/     # Materiales de cada componente
│   ├── Scenes/         # Escenas de Unity
│   ├── Settings/       # Configuración de URP y post-procesado
│   └── Shaders/        # Shader Graph de la luz solar
├── Packages/           # Dependencias del proyecto
└── ProjectSettings/    # Configuración de Unity
```

---

## 👤 Autor

**burgui-ia**: proyecto para la materia de *Gráficos por Computadora*.
