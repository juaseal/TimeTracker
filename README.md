# TaskUp

Aplicación de escritorio para Windows que permite registrar el tiempo por proyecto, épica, actividad y comentario.

## Funciones principales

- Cronómetro con tareas recientes.
- Sugerencias en Comment: busca texto en comentarios de todo el historial, sin distinguir mayúsculas, con hasta 10 resultados únicos y recientes primero. Selecciona con el ratón o flechas y Enter; Escape cierra la lista.
- Edición directa del parte diario, portapapeles y deshacer.
- Resumen semanal agrupado por día y preparado para SAP.
- Jornadas configurables mediante plantillas y periodos de calendario.
- Informes navegables por proyecto, épica, tarea y comentario.
- Pestaña History: búsqueda por proyecto, épica, actividad o comentario en todo el historial, tabla de fechas, horas, épicas, actividades y comentarios. Edición directa en las celdas con guardado automático. Intro selecciona el contenido para reemplazarlo; Ctrl+C copia, Ctrl+V pega un campo en las celdas seleccionadas o un rango desde Excel, y Ctrl+Z revierte la última edición o pegado. Las sesiones nocturnas conservan la fecha de fin.
- Almacenamiento local mediante SQLite.

## Desarrollo

Requiere Windows y el SDK de .NET 10.

```powershell
dotnet restore
dotnet run
```

## Publicación portable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

La base de datos se guarda por usuario en %LocalAppData%\TimeTracker\timetracker.db. Al iniciar por primera vez, la aplicación migra automáticamente una base portable anterior ubicada junto al ejecutable. Desde Configuración se puede exportar el historial, crear una copia de seguridad y abrir la carpeta de datos.

### Publicar desde Visual Studio

1. Haz clic derecho sobre el proyecto **TimeTracker** y selecciona **Publicar**.
2. Elige el perfil **Portable-win-x64**.
3. Pulsa **Publicar**.

El perfil genera `TaskUp.exe` como un ejecutable único y autocontenido en `bin\Release\net10.0-windows\win-x64\publish`. No necesita instalar .NET en el equipo de destino. Las bibliotecas nativas están incluidas dentro del EXE y .NET las extrae automáticamente al arrancar.


dotnet restore .\TimeTracker.csproj -r win-x64
dotnet publish .\TimeTracker.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\dist\main

## Validación

La solución incluye una suite de regresión autocontenida. Se ejecuta con dotnet run sobre el proyecto TimeTracker.Tests en configuración Release.

## Microsoft Store

La carpeta Packaging contiene el manifiesto, recursos y script de creación del MSIX. Consulta Packaging/README.md. Los textos iniciales de privacidad, avisos y ficha comercial están en docs y store.