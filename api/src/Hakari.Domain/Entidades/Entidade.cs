namespace Hakari.Domain.Entidades;

/// <summary>Base de todas as entidades: todas têm um Id gerado pelo banco.</summary>
public abstract class Entidade
{
    public int Id { get; protected set; }

    /// <summary>
    /// Compara pela referência e, se já persistida, pelo Id. Evita que duas entidades novas
    /// (Id 0) sejam tratadas como a mesma.
    /// </summary>
    public bool Mesma(Entidade? outra) =>
        outra is not null && (ReferenceEquals(this, outra) || (Id != 0 && Id == outra.Id && GetType() == outra.GetType()));
}
