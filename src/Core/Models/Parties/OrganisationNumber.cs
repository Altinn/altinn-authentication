namespace Altinn.Platform.Authentication.Core.Models.Parties;

/// <summary>
/// Used to compare OrgNo in the format used by MaskinPorten
/// </summary>
public record OrganisationNumber()
{    
    public string Authority { get; private set; } = string.Empty;

    public string ID { get; private set; } = string.Empty;

    public static OrganisationNumber CreateFromMaskinPortenToken(string data)
    {
        OrganisationNumber org = new();
        string cleanData = RemoveSpecialCharacters(data);        

        string[] pairs = cleanData.Split(',');
        pairs[0] = pairs[0].TrimStart();
        pairs[1] = pairs[1].TrimStart();

        Dictionary<string, string> keyValuePairs = [];

        foreach (string pair in pairs)
        {
            string[] splitPair = pair.Split(':');
            if (splitPair.Length == 2)
            {
                keyValuePairs.Add(splitPair[0], splitPair[1]);
            }
            if (splitPair.Length == 3)
            {
                keyValuePairs.Add(splitPair[0], splitPair[1] + ":" + splitPair[2]);
            }
            
        }
        try
        {
            org.Authority = keyValuePairs["authority"];
            org.ID = keyValuePairs["ID"];
        }
        catch 
        {
        
        }

        return org;
    }

    public static OrganisationNumber CreateFromStringOrgNo ( string orgno)
    {
        var prefix = "0192:";

        if (orgno.StartsWith("0192:"))
        {
            prefix = "";
        }

        return new OrganisationNumber()
        {
            Authority = "iso6523-actorid-upis",
            ID = prefix + orgno
        };
    }

    public static OrganisationNumber Empty () 
    {
        return new OrganisationNumber()
        {
        };
    }

    private static string RemoveSpecialCharacters (string str)
    {
        return new string(
        str.Where(
            c => Char.IsLetterOrDigit(c) ||
                c == '.' || c == '_' || c == ' ' || c == ':' || c == ',' || c == '-')
            .ToArray());
    }
};
