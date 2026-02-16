namespace SLText.Core.Engine.LSP;

public class SignatureHelpResult
{
    public int ActiveParameter { get; set; }
    public List<SignatureItem> Signatures { get; set; } = new();
}