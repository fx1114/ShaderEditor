namespace ShaderEditor.ViewModels;

public sealed class PaletteParameters
{
    public PaletteParameters(
        double aR = 0.5, double aG = 0.5, double aB = 0.5,
        double bR = 0.5, double bG = 0.5, double bB = 0.5,
        double cR = 1.0, double cG = 1.0, double cB = 1.0,
        double dR = 0.0, double dG = 0.0, double dB = 0.0)
    {
        AR = aR;
        AG = aG;
        AB = aB;
        BR = bR;
        BG = bG;
        BB = bB;
        CR = cR;
        CG = cG;
        CB = cB;
        DR = dR;
        DG = dG;
        DB = dB;
    }

    public double AR { get; set; }
    public double AG { get; set; }
    public double AB { get; set; }
    public double BR { get; set; }
    public double BG { get; set; }
    public double BB { get; set; }
    public double CR { get; set; }
    public double CG { get; set; }
    public double CB { get; set; }
    public double DR { get; set; }
    public double DG { get; set; }
    public double DB { get; set; }

    public PaletteParameters Clone() => new(
        AR, AG, AB,
        BR, BG, BB,
        CR, CG, CB,
        DR, DG, DB);
}
