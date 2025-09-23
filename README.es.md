# Where I Left Off — Notas rápidas al cerrar Unity

> Deja una nota al salir. Recíbela al volver.

**Where I Left Off** añade un flujo rápido para escribir una nota justo **antes de cerrar Unity** y te la muestra **al arrancar**. Incluye un **Browser** con búsqueda para revisar notas y abrir referencias del proyecto.

## ✨ Características
- **Popup al salir**: escribe una nota antes de cerrar (o cancela el cierre).
- **Recordatorio al abrir**: muestra la nota de la última sesión o del último día.
- **Browser con búsqueda**: filtra por texto/fecha y abre/ping de referencias.
- **Cero fricción**: sin servicios externos ni configuración obligatoria.
- **Datos locales**: JSON bajo `Library/` (no ensucia tu control de versiones).
- **Localización automática**: si tu Unity/SO está en español, UI y docs en **ES**; en cualquier otro caso, **EN**.

## 📦 Instalación (UPM)
1. Abre **Window → Package Manager**.
2. Pulsa **Add package from disk…** y elige `package.json` en `Packages/com.devforge.whereileftoff/`.
   - Alternativa: añade el repositorio/URL en tu `manifest.json`.

## 🚀 Quickstart (60 s)
1. Trabaja normal.
2. Al **cerrar Unity**, escribe una **frase** en el popup y guarda.
3. Al **abrir**, verás el recordatorio. Para gestionar notas:
   - **Tools → DevForge → Where I Left Off → Browser**

## 🧭 Menús
- **Tools → DevForge → Where I Left Off → Browser**
- **Tools → DevForge → Where I Left Off → Open Documentation**

## ⚙️ Ajustes
Abre **Edit → Preferences… → Where I Left Off** (o ⚙️ en el Browser).

Opciones:
- Mostrar popup al **salir**.
- Mostrar popup al **arrancar** (última sesión o último día).
- Limpiar borradores temporales.
- Forzar idioma (opcional, pruebas): `wilo.forceLang = ES | EN`.

## 📁 Datos
- Notas: `Library/WhereILeftOff/*.json`
- Estado de usuario: `Library/WhereILeftOff/User/*`

> Estos archivos **no** deberían versionarse. Para migrar entre máquinas, cópialos manualmente.

## 🖱️ Uso
- **Salir**: escribe y guarda. Puedes **cancelar** el cierre.
- **Abrir**: verás el recordatorio; ciérralo o entra al **Browser**.
- **Browser**: busca por texto, haz `Ping`/`Open` en referencias, edita o duplica notas.

## 🧪 Compatibilidad
- Probado en **Unity 6**.
- Paquete **solo Editor** (excluido del build).
- UI en IMGUI (estable). Soporte UI Toolkit opcional en el futuro.

## ❓ FAQ
- **¿Puedo desactivar los popups?** Sí, en **Preferences** o desde el Browser.
- **¿Sincroniza en la nube?** No. Diseño local y simple.
- **¿Exportar/Importar?** Copia los JSON de `Library/WhereILeftOff/`.
- **¿Multilenguaje?** Detecta ES; por defecto usa EN.

## 💬 Soporte
- Incidencias y feedback: issue en el repo o contacto directo.
