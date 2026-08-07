# Script para probar la integración con Biostar una sola vez
# Útil para validar rápidamente que todo funciona

param(
    [string]$ApiBaseUrl = "http://localhost:8000"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Prueba de Integración Biostar" -ForegroundColor Cyan
Write-Host "  API: $ApiBaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que la API esté disponible
Write-Host "Verificando que la API esté disponible..." -ForegroundColor Yellow
try {
    $testResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/login/Autentificar" -Method POST -Body '{}' -ContentType "application/json" -ErrorAction Stop -TimeoutSec 5
    Write-Host "✅ API disponible" -ForegroundColor Green
}
catch {
    Write-Host "❌ La API no está disponible en $ApiBaseUrl" -ForegroundColor Red
    Write-Host "   Asegúrate de que la API esté corriendo y la URL sea correcta." -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Llamar al endpoint de Biostar
Write-Host "Llamando a /api/biostar/events..." -ForegroundColor Yellow
try {
    $url = "$ApiBaseUrl/api/biostar/events"
    $response = Invoke-RestMethod -Uri $url -Method POST -ContentType "application/json" -ErrorAction Stop
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  RESULTADO" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    if ($response.tieneDatos) {
        Write-Host "✅ Evento encontrado en Biostar" -ForegroundColor Green
        Write-Host ""
        Write-Host "Detalles del evento:" -ForegroundColor Yellow
        Write-Host "  Legajo: $($response.evento.legajo)" -ForegroundColor White
        Write-Host "  Nombre: $($response.evento.nombre)" -ForegroundColor White
        Write-Host "  Fecha/Hora UTC: $($response.evento.fechaHoraUtc)" -ForegroundColor White
        Write-Host "  Dispositivo: $($response.evento.deviceBiostarName) (ID: $($response.evento.deviceBiostarId))" -ForegroundColor White
        Write-Host "  Código de Evento: $($response.evento.eventCode)" -ForegroundColor White
        
        if ($response.fichadaId) {
            Write-Host ""
            Write-Host "✅ Fichada registrada en la BD" -ForegroundColor Green
            Write-Host "  Fichada ID: $($response.fichadaId)" -ForegroundColor White
        } else {
            Write-Host ""
            Write-Host "⚠️  No se pudo registrar la fichada en la BD" -ForegroundColor Yellow
            Write-Host "   (Puede ser que el usuario no exista o haya un error)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "ℹ️  No hay eventos nuevos en Biostar" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Esto puede significar:" -ForegroundColor Yellow
        Write-Host "  - No hubo fichadas en los últimos minutos (configurado en BiostarDefaultMinutesBack)" -ForegroundColor White
        Write-Host "  - El dispositivo configurado no tiene eventos" -ForegroundColor White
        Write-Host "  - El código de evento no coincide" -ForegroundColor White
        Write-Host ""
        Write-Host "Sugerencias:" -ForegroundColor Yellow
        Write-Host "  1. Realiza una fichada en Biostar con un usuario de prueba" -ForegroundColor White
        Write-Host "  2. Verifica que el legajo del usuario exista en tu base de datos" -ForegroundColor White
        Write-Host "  3. Verifica la configuración en Web.config:" -ForegroundColor White
        Write-Host "     - BiostarDefaultMinutesBack (minutos hacia atrás)" -ForegroundColor White
        Write-Host "     - BiostarDefaultDeviceId (código de evento)" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
}
catch {
    $errorMsg = $_.Exception.Message
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        $errorMsg = "$errorMsg`n$responseBody"
    }
    
    Write-Host ""
    Write-Host "❌ Error llamando a Biostar:" -ForegroundColor Red
    Write-Host $errorMsg -ForegroundColor Red
    Write-Host ""
    Write-Host "Posibles causas:" -ForegroundColor Yellow
    Write-Host "  - Biostar no está disponible o no responde" -ForegroundColor White
    Write-Host "  - Credenciales incorrectas en appSettings.secrets.config" -ForegroundColor White
    Write-Host "  - URL de Biostar incorrecta en Web.config" -ForegroundColor White
    Write-Host "  - Problemas de red/firewall" -ForegroundColor White
    exit 1
}

