public class UpdateRequest
{
    public string Id { get; set; }
    public Vendor Vendor { get; set; }
    public Name Name { get; set; }
    public Description Description { get; set; }
    public List<Right> Rights { get; set; }
    public List<string> AllowedRedirectUrls { get; set; }
    public List<string> ClientId { get; set; }
}

public class Vendor
{
    public string ID { get; set; }
}

public class Name
{
    public string En { get; set; }
    public string Nb { get; set; }
    public string Nn { get; set; }
}

public class Description
{
    public string En { get; set; }
    public string Nb { get; set; }
    public string Nn { get; set; }
}

public class Right
{
    public List<Resource> Resource { get; set; }
}

public class Resource
{
    public string Value { get; set; }
    public string Id { get; set; }
}