# Estado de preparación para Microsoft Store

## Implementado en esta rama
- Rama comercial aislada.
- Datos y logs en LocalAppData, compatibles con una instalación MSIX.
- Migración automática desde la base portable situada junto al ejecutable.
- Rotación del log local y mensajes de error sin detalles técnicos en pantalla.
- Windows 10 2004 como versión mínima explícita.
- Metadatos de ensamblado y versión 1.0.0.
- Exportación completa a CSV.
- Copia de seguridad consistente mediante SQLite Backup API.
- Acceso a la carpeta local de datos y pantalla Acerca de/privacidad.
- Suite de regresión sin dependencias externas.
- CI para Windows.
- Plantilla de manifiesto, recursos y script MSIX para x64/ARM64.
- Borradores de privacidad, avisos de terceros y ficha es-ES.

## Requiere decisiones o credenciales del editor
- Reservar el nombre definitivo en Partner Center.
- Sustituir identidad, publisher y nombre legal al crear el paquete.
- Definir correo, web pública de soporte y URL pública de privacidad.
- Elegir precio, mercados, prueba y licencia comercial/EULA.
- Aprobar marca e iconografía definitiva.

## Pendiente antes del envío
- Instalar Windows SDK y generar el primer MSIX.
- Validar instalación, actualización y desinstalación en máquinas limpias.
- Ejecutar Windows App Certification Kit.
- Auditoría completa con Narrator y Accessibility Insights.
- Localización inglesa y extracción de cadenas a recursos.
- Capturas comerciales y material de Store.
- Inventario completo de licencias transitivas.
- Pruebas de migración desde todos los builds distribuidos.