# Script para probar el endpoint /api/inicio/totem
# Valida que retorne correctamente el menú del día y el primer turno

param(
    [string]$ApiBaseUrl = "http://localhost:8000",
    [int]$Legajo = 0  # ⚠️ CAMBIAR: Legajo del usuario a probar
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Prueba Endpoint /api/inicio/totem" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($Legajo -le 0) {
    Write-Host "⚠️  Debes proporcionar un legajo válido" -ForegroundColor Yellow
    Write-Host "   Uso: .\Scripts\ProbarInicioTotem.ps1 -Legajo 1234" -ForegroundColor Gray
    exit 1
}

# Verificar que la API esté disponible
Write-Host "Verificando que la API esté disponible..." -ForegroundColor Yellow
try {
    $testResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/login/Autentificar" -Method POST -Body '{}' -ContentType "application/json" -ErrorAction Stop -TimeoutSec 5
    Write-Host "✅ API disponible" -ForegroundColor Green
}
catch {
    Write-Host "❌ La API no está disponible en $ApiBaseUrl" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Llamar al endpoint
Write-Host "Llamando a /api/inicio/totem?legajo=$Legajo..." -ForegroundColor Yellow
try {
    $url = "$ApiBaseUrl/api/inicio/totem?legajo=$Legajo"
    $response = Invoke-RestMethod -Uri $url -Method GET -ErrorAction Stop
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  RESULTADO" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Validar usuario
    if ($response.usuario) {
        Write-Host "✅ Usuario encontrado:" -ForegroundColor Green
        Write-Host "   ID: $($response.usuario.id)" -ForegroundColor White
        Write-Host "   Nombre: $($response.usuario.nombre) $($response.usuario.apellido)" -ForegroundColor White
        Write-Host "   Legajo: $($response.usuario.legajo)" -ForegroundColor White
        Write-Host "   Planta: $($response.usuario.plantaNombre) (ID: $($response.usuario.plantaId))" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host "❌ No se retornó información del usuario" -ForegroundColor Red
    }
    
    # Validar turnos
    if ($response.turnos -and $response.turnos.Count -gt 0) {
        Write-Host "✅ Turnos encontrados: $($response.turnos.Count)" -ForegroundColor Green
        $primerTurno = $response.turnos[0]
        Write-Host "   Primer turno: $($primerTurno.nombre) (ID: $($primerTurno.id))" -ForegroundColor White
        Write-Host "   Horario: $($primerTurno.horaDesde) - $($primerTurno.horaHasta)" -ForegroundColor White
        Write-Host ""
        
        # Validar menú del día
        if ($response.menuDelDia -and $response.menuDelDia.Count -gt 0) {
            Write-Host "✅ Menú del día encontrado: $($response.menuDelDia.Count) platos" -ForegroundColor Green
            
            # Validar que todos los platos correspondan al primer turno
            $todosCorresponden = $true
            $platosConStock = 0
            $platosSinStock = 0
            
            foreach ($plato in $response.menuDelDia) {
                if ($plato.turnoId -ne $primerTurno.id) {
                    $todosCorresponden = $false
                    Write-Host "   ⚠️  Plato '$($plato.platoNombre)' no corresponde al primer turno (turnoId: $($plato.turnoId))" -ForegroundColor Yellow
                }
                
                if ($plato.disponible -gt 0) {
                    $platosConStock++
                } else {
                    $platosSinStock++
                }
            }
            
            if ($todosCorresponden) {
                Write-Host "   ✅ Todos los platos corresponden al primer turno" -ForegroundColor Green
            } else {
                Write-Host "   ❌ Algunos platos NO corresponden al primer turno" -ForegroundColor Red
            }
            
            Write-Host "   ✅ Platos con stock: $platosConStock" -ForegroundColor Green
            if ($platosSinStock -gt 0) {
                Write-Host "   ⚠️  Platos sin stock: $platosSinStock" -ForegroundColor Yellow
            }
            
            Write-Host ""
            Write-Host "   Primeros 5 platos del menú:" -ForegroundColor Yellow
            $response.menuDelDia | Select-Object -First 5 | ForEach-Object {
                Write-Host "     - $($_.platoNombre) (Disponible: $($_.disponible), Turno: $($_.turnoNombre))" -ForegroundColor White
            }
        } else {
            Write-Host "⚠️  No hay platos en el menú del día" -ForegroundColor Yellow
            Write-Host "   Esto puede ser normal si:" -ForegroundColor Gray
            Write-Host "     - No hay menú configurado para hoy" -ForegroundColor Gray
            Write-Host "     - No hay platos con stock disponible" -ForegroundColor Gray
            Write-Host "     - Los platos no coinciden con los filtros del usuario" -ForegroundColor Gray
        }
    } else {
        Write-Host "❌ No hay turnos disponibles" -ForegroundColor Red
        Write-Host "   Verifica que haya turnos activos en la base de datos" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  VALIDACIÓN COMPLETA" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Resumen de validaciones
    $validaciones = @()
    if ($response.usuario) { $validaciones += "✅ Usuario retornado" } else { $validaciones += "❌ Usuario faltante" }
    if ($response.turnos -and $response.turnos.Count -gt 0) { $validaciones += "✅ Turnos retornados" } else { $validaciones += "❌ Turnos faltantes" }
    if ($response.menuDelDia -and $response.menuDelDia.Count -gt 0) { $validaciones += "✅ Menú retornado" } else { $validaciones += "⚠️  Menú vacío" }
    
    $primerTurno = $response.turnos[0]
    $menuCorresponde = $true
    if ($response.menuDelDia) {
        foreach ($plato in $response.menuDelDia) {
            if ($plato.turnoId -ne $primerTurno.id) {
                $menuCorresponde = $false
                break
            }
        }
    }
    if ($menuCorresponde -and $response.menuDelDia -and $response.menuDelDia.Count -gt 0) {
        $validaciones += "✅ Menú corresponde al primer turno"
    } elseif ($response.menuDelDia -and $response.menuDelDia.Count -gt 0) {
        $validaciones += "❌ Menú NO corresponde al primer turno"
    }
    
    Write-Host ""
    foreach ($validacion in $validaciones) {
        Write-Host $validacion
    }
    
}
catch {
    $errorMsg = $_.Exception.Message
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        $errorMsg = "$errorMsg`n$responseBody"
    }
    
    Write-Host ""
    Write-Host "❌ Error llamando al endpoint:" -ForegroundColor Red
    Write-Host $errorMsg -ForegroundColor Red
    Write-Host ""
    Write-Host "Posibles causas:" -ForegroundColor Yellow
    Write-Host "  - El legajo no existe en la base de datos" -ForegroundColor White
    Write-Host "  - El usuario no tiene planta asignada" -ForegroundColor White
    Write-Host "  - No hay turnos disponibles" -ForegroundColor White
    Write-Host "  - Error en la base de datos" -ForegroundColor White
    exit 1
}

