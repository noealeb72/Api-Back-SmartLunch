using smartlunch_api.Dtos;
using smartlunch_api.Models;
using System;
using System.Linq;

namespace smartlunch_api.Services
{
    public class ServicioConfigTotem
    {
        // Obtener config por device_id (solo no borrados)
        public ConfigTotemDetalleDto ObtenerPorDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return null;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var dto = ctx.sl_configtotem
                    .Where(x => x.device_id == deviceId && !x.deletemark)
                    .Select(x => new ConfigTotemDetalleDto
                    {
                        Id = x.id,
                        DeviceId = x.device_id,
                        BiostarModo = x.biostar_modo,
                        BiostarIntervalSegundos = x.biostar_interval_segundos,
                        SmarttimeModo = x.smarttime_modo,
                        CreatedAt = x.created_at,
                        UpdatedAt = x.updated_at,
                        DeleteMark = x.deletemark
                    })
                    .FirstOrDefault();

                return dto;
            }
        }

        // Crear o actualizar config por device_id
        public ConfigTotemDetalleDto Guardar(ConfigTotemDto dto, string usuario)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.device_id))
                throw new Exception("device_id es obligatorio.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var existente = ctx.sl_configtotem
                    .FirstOrDefault(x => x.device_id == dto.device_id && !x.deletemark);

                if (existente == null)
                {
                    var nueva = new sl_configtotem
                    {
                        device_id = dto.device_id.Trim(),
                        biostar_modo = dto.biostar_modo,
                        biostar_interval_segundos = dto.biostar_interval_segundos,
                        smarttime_modo = dto.smarttime_modo,
                        created_at = DateTime.Now,
                        deletemark = false
                    };

                    ctx.sl_configtotem.Add(nueva);
                    ctx.SaveChanges();

                    return new ConfigTotemDetalleDto
                    {
                        Id = nueva.id,
                        DeviceId = nueva.device_id,
                        BiostarModo = nueva.biostar_modo,
                        BiostarIntervalSegundos = nueva.biostar_interval_segundos,
                        SmarttimeModo = nueva.smarttime_modo,
                        CreatedAt = nueva.created_at,
                        UpdatedAt = nueva.updated_at,
                        DeleteMark = nueva.deletemark
                    };
                }
                else
                {
                    existente.biostar_modo = dto.biostar_modo;
                    existente.biostar_interval_segundos = dto.biostar_interval_segundos;
                    existente.smarttime_modo = dto.smarttime_modo;
                    existente.updated_at = DateTime.Now;

                    ctx.SaveChanges();

                    return new ConfigTotemDetalleDto
                    {
                        Id = existente.id,
                        DeviceId = existente.device_id,
                        BiostarModo = existente.biostar_modo,
                        BiostarIntervalSegundos = existente.biostar_interval_segundos,
                        SmarttimeModo = existente.smarttime_modo,
                        CreatedAt = existente.created_at,
                        UpdatedAt = existente.updated_at,
                        DeleteMark = existente.deletemark
                    };
                }
            }
        }
    }
}
