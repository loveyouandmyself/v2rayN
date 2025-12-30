namespace ServiceLib.Models;

public class LoginResponse
{
    public string? Code { get; set; }
    public LoginData? Data { get; set; }
    public string? Msg { get; set; }
    
    public bool Success => (Code == "0" || Code == "200" || Code.IsNullOrEmpty()) && Data != null;
    public string? Name => Data?.Name;
}

public class LoginData
{
    public string? Name { get; set; }
}

