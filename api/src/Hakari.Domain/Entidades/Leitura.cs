namespace Hakari.Domain.Entidades;

/// <summary>Heartbeat do nó (a cada 30 s): o que os sensores viam naquele momento.</summary>
public class Leitura : Entidade
{
    private Leitura() { }

    public Leitura(Poste poste, DateTime recebidaEm, int ldr, int ldrReferencia, int distanciaCm,
                   bool presenca, int brilho, int rssi)
    {
        Poste = poste;
        RecebidaEm = recebidaEm;
        Ldr = ldr;
        LdrReferencia = ldrReferencia;
        DistanciaCm = distanciaCm;
        Presenca = presenca;
        Brilho = brilho;
        Rssi = rssi;
    }

    public int PosteId { get; private set; }
    public Poste Poste { get; private set; } = null!;
    public DateTime RecebidaEm { get; private set; }
    public int Ldr { get; private set; }
    public int LdrReferencia { get; private set; }
    public int DistanciaCm { get; private set; }
    public bool Presenca { get; private set; }
    public int Brilho { get; private set; }
    public int Rssi { get; private set; }
}
