# Script para probar la integración con Biostar de forma recurrente
# Ejecuta llamadas cada 2-3 minutos al endpoint de eventos de Biostar

param(
    [string]$ApiBaseUrl = "http://localhost:8000",
    [int]$IntervaloMinutos = 2,
    [int]$MaxEjecuciones = 0,  # 0 = infinito
    [string]$LogPath = ".\Logs\BiostarTest.log"
)

# Crear directorio de logs si no existe
$logDir = Split-Path -Parent $LogPath
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Función para escribir logs
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogPath -Value $logMessage
}

# Función para llamar al endpoint de Biostar
function Invoke-BiostarEvents {
    try {
        $url = "$ApiBaseUrl/api/biostar/events"
        Write-Log "Llamando a: $url"
        
        $response = Invoke-RestMethod -Uri $url -Method POST -ContentType "application/json" -ErrorAction Stop
        
        if ($response.tieneDatos) {
            Write-Log "✅ Evento encontrado: Legajo=$($response.evento.legajo), Nombre=$($response.evento.nombre), FichadaId=$($response.fichadaId)" "SUCCESS"
            return $true
        } else {
            Write-Log "ℹ️  No hay eventos nuevos en Biostar" "INFO"
            return $false
        }
    }
    catch {
        $errorMsg = $_.Exception.Message
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            $errorMsg = "$errorMsg - $responseBody"
        }
        Write-Log "❌ Error llamando a Biostar: $errorMsg" "ERROR"
        return $false
    }
}

# Función para verificar que la API esté disponible
function Test-ApiAvailable {
    try {
        $url = "$ApiBaseUrl/api/login/Autentificar"
        $response = Invoke-WebRequest -Uri $url -Method POST -Body '{}' -ContentType "application/json" -ErrorAction Stop -TimeoutSec 5
        return $true
    }
    catch {
        return $false
    }
}

# ===== INICIO DEL SCRIPT =====

Write-Log "========================================"
Write-Log "  Prueba Recurrente de Biostar"
Write-Log "  API: $ApiBaseUrl"
Write-Log "  Intervalo: $IntervaloMinutos minutos"
Write-Log "  Máximo de ejecuciones: $(if ($MaxEjecuciones -eq 0) { 'Infinito' } else { $MaxEjecuciones })"
Write-Log "========================================"

# Verificar que la API esté disponible
Write-Log "Verificando que la API esté disponible..."
if (-not (Test-ApiAvailable)) {
    Write-Log "❌ La API no está disponible en $ApiBaseUrl" "ERROR"
    Write-Log "   Asegúrate de que la API esté corriendo y la URL sea correcta." "ERROR"
    exit 1
}
Write-Log "✅ API disponible" "SUCCESS"

$ejecuciones = 0
$eventosEncontrados = 0
$errores = 0

# Bucle principal
while ($true) {
    $ejecuciones++
    Write-Log ""
    Write-Log "--- Ejecución #$ejecuciones ---"
    
    $resultado = Invoke-BiostarEvents
    if ($resultado) {
        $eventosEncontrados++
    } else {
        $errores++
    }
    
    # Verificar si debemos detener
    if ($MaxEjecuciones -gt 0 -and $ejecuciones -ge $MaxEjecuciones) {
        Write-Log ""
        Write-Log "========================================"
        Write-Log "  Resumen Final"
        Write-Log "  Total ejecuciones: $ejecuciones"
        Write-Log "  Eventos encontrados: $eventosEncontrados"
        Write-Log "  Errores: $errores"
        Write-Log "========================================"
        break
    }
    
    # Esperar antes de la próxima ejecución
    $segundosEspera = $IntervaloMinutos * 60
    Write-Log "Esperando $IntervaloMinutos minutos hasta la próxima ejecución..."
    Start-Sleep -Seconds $segundosEspera
}

Write-Log "Script finalizado"

