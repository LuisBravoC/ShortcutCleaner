# Script para configurar el servicio con permisos de usuario
param(
    [string]$ServiceName = "IconMonitorCSharpV2"
)

Write-Host "Configurando servicio con permisos de usuario..." -ForegroundColor Yellow

try {
    # Detener el servicio si está corriendo
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq "Running") {
        Write-Host "Deteniendo servicio..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 3
    }

    # Obtener el usuario actual
    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    Write-Host "Usuario actual: $currentUser" -ForegroundColor Green

    # Configurar el servicio para ejecutarse con el usuario actual
    $exePath = Join-Path $PSScriptRoot "CopiarIconos.exe"
    
    # Eliminar servicio existente
    & sc.exe delete $ServiceName 2>$null
    Start-Sleep -Seconds 2
    
    # Crear nuevo servicio con permisos específicos
    $result = & sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "Icon Monitor Service V2 (User Context)" obj= "LocalSystem"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Servicio creado exitosamente con LocalSystem" -ForegroundColor Green
        
        # Intentar darle permisos adicionales usando icacls
        Write-Host "Configurando permisos de archivo..." -ForegroundColor Yellow
        
        # Dar permisos completos al directorio del usuario
        $userProfile = $env:USERPROFILE
        $desktopPath = Join-Path $userProfile "Desktop"
        
        if (Test-Path $desktopPath) {
            icacls $desktopPath /grant "NT AUTHORITY\SYSTEM:(F)" /T /C 2>$null
            Write-Host "Permisos configurados para: $desktopPath" -ForegroundColor Green
        }
        
        # Configurar permisos para OneDrive
        $oneDrivePaths = Get-ChildItem -Path $userProfile -Directory -Filter "OneDrive*" -ErrorAction SilentlyContinue
        foreach ($oneDrivePath in $oneDrivePaths) {
            $escritorioPath = Join-Path $oneDrivePath.FullName "Escritorio"
            if (Test-Path $escritorioPath) {
                icacls $escritorioPath /grant "NT AUTHORITY\SYSTEM:(F)" /T /C 2>$null
                Write-Host "Permisos configurados para: $escritorioPath" -ForegroundColor Green
            }
        }
        
        # Intentar iniciar el servicio
        Write-Host "Iniciando servicio..." -ForegroundColor Yellow
        Start-Service -Name $ServiceName
        
        $serviceStatus = Get-Service -Name $ServiceName
        Write-Host "Estado del servicio: $($serviceStatus.Status)" -ForegroundColor $(if($serviceStatus.Status -eq "Running") {"Green"} else {"Red"})
        
    } else {
        Write-Host "Error creando el servicio. Código de salida: $LASTEXITCODE" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Configuración completada." -ForegroundColor Yellow
