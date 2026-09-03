# Empaquetado para Microsoft Store

El script build-msix.ps1 publica una versión autocontenida y crea el MSIX mediante makeappx.exe.

## Requisitos

1. Instalar Windows 10/11 SDK desde Visual Studio Installer.
2. Reservar el producto en Partner Center.
3. Copiar exactamente Package/Identity/Name, Publisher y el nombre visible del editor.
4. Ejecutar Packaging/build-msix.ps1 pasando PackageIdentityName, Publisher, PublisherDisplayName, RuntimeIdentifier y Version.

El resultado se guarda en artifacts. Para cubrir ARM64, repetir con RuntimeIdentifier win-arm64. El cuarto componente de versión debe permanecer en cero para envíos a la Store.

El paquete no se firma localmente para el envío a Microsoft Store: Microsoft lo firma después de la certificación. Para distribución directa fuera de la Store sí se necesita una firma de confianza.

## Antes de enviar

- Instalar y probar el paquete en una cuenta de usuario estándar.
- Validar actualización desde la versión anterior conservando LocalAppData.
- Ejecutar Windows App Certification Kit.
- Sustituir los recursos gráficos provisionales por la marca comercial definitiva.