namespace SLText.Core.Engine.LSP;

public class SignatureItem
{
    public string? Label { get; set; }       
    public string? Documentation { get; set; } 
    public List<ParameterItem> Parameters { get; set; } = new();
}