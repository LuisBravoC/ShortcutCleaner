# CopiarIconos - Limpiador Automático de Iconos de Escritorio

## Descripción

Servicio de Windows que elimina automáticamente archivos de acceso directo (.lnk) del escritorio que no son válidos.

## Funcionalidades Principales

- Monitoreo automático del escritorio del usuario
- Detección de escritorios múltiples (Desktop local y OneDrive)
- Eliminación de accesos directos inválidos
- Ejecución como servicio de Windows con permisos LocalSystem
- Compatibilidad con entornos OneDrive y escritorios sincronizados

## Ubicaciones Monitoreadas

- Desktop local: `C:\Users\[usuario]\Desktop`
- OneDrive Desktop: `C:\Users\[usuario]\OneDrive\Escritorio`
- Detección automática de múltiples cuentas OneDrive

## Ubicaciones de Origen por defecto

- Public Desktop: `C:\Windows\Setup\Files\Public`
- User Desktop: `C:\Windows\Setup\Files\Links`

## Configuración del Servicio

- **Nombre**: IconMonitorService
- **Contexto de ejecución**: LocalSystem
- **Inicio**: Automático
- **Permisos**: Acceso completo a directorios del usuario