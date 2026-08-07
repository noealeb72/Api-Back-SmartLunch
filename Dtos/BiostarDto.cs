using System.Collections.Generic;
using System;

public class BiostarEventsResponseDto
{
    public BiostarEventCollectionDto EventCollection { get; set; }
    public BiostarResponseInfoDto Response { get; set; }
}

public class BiostarEventCollectionDto
{
    public List<BiostarEventRowDto> rows { get; set; }
}

public class BiostarEventRowDto
{
    public string id { get; set; }
    public DateTime datetime { get; set; }
    public string index { get; set; }
    public BiostarUserDto user_id { get; set; }
    public BiostarDeviceDto device_id { get; set; }
    public BiostarEventTypeDto event_type_id { get; set; }
}

public class BiostarUserDto
{
    public string user_id { get; set; }
    public string name { get; set; }
    public string photo_exists { get; set; }
}

public class BiostarDeviceDto
{
    public string id { get; set; }
    public string name { get; set; }
}

public class BiostarEventTypeDto
{
    public string code { get; set; }
}

public class BiostarResponseInfoDto
{
    public string code { get; set; }
    public string link { get; set; }
    public string message { get; set; }
}
