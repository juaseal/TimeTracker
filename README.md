# TimeTracker

Aplicación de escritorio para Windows que permite registrar el tiempo por proyecto, épica, actividad y comentario.

## Funciones principales

- Cronómetro con tareas recientes.
- Edición directa del parte diario, portapapeles y deshacer.
- Resumen semanal agrupado por día y preparado para SAP.
- Jornadas configurables mediante plantillas y periodos de calendario.
- Informes navegables por proyecto, épica, tarea y comentario.
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

La base de datos se crea en `data/timetracker.db`, junto al ejecutable. La carpeta `data` contiene información personal y está excluida del repositorio. Para trasladar el histórico a otro equipo, copia por separado el ejecutable y esa carpeta.
