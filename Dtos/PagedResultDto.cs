using System;
using System.Collections.Generic;

namespace smartlunch_api.Dtos
{
    public class PagedResultDto<T>
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public IEnumerable<T> items { get; set; }

        /*public static implicit operator List<T>(PagedResultDto<ComandaListadoDto> v)
        {
            throw new NotImplementedException();
        }*/
    }
}
