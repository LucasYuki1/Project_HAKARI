// Casos de teste das regras de taint (semgrep --test). "ruleid" marca a linha que precisa ser
// apontada; "ok" marca a linha que não pode ser apontada. Não é código do Hakari: não compila.

public class CasosTextoLivre
{
    public async Task<ReporteResponse> SemFiltro(ReporteRequest pedido)
    {
        // ruleid: hakari.taint.texto-livre-sem-filtro
        var nova = Ocorrencia.AbrirPorCidadao(poste, cidadao, pedido.Categoria, pedido.Descricao, Agora);
        return null;
    }

    public async Task<ReporteResponse> ViaVariavel(ReporteRequest pedido)
    {
        var texto = pedido.Descricao.Trim();
        // ruleid: hakari.taint.texto-livre-sem-filtro
        var nova = Ocorrencia.AbrirPorCidadao(poste, cidadao, pedido.Categoria, texto, Agora);
        return null;
    }

    public async Task<ReporteResponse> ComFiltro(ReporteRequest pedido)
    {
        var descricao = privacidade.Filtrar(pedido.Descricao);
        // ok: hakari.taint.texto-livre-sem-filtro
        var nova = Ocorrencia.AbrirPorCidadao(poste, cidadao, pedido.Categoria, descricao.Texto, Agora);
        return null;
    }

    public async Task<OcorrenciaDetalhe> ResolverSemFiltro(int id, int tecnicoId, string solucao, CancellationToken ct)
    {
        // ruleid: hakari.taint.texto-livre-sem-filtro
        ocorrencia.Resolver(tecnico, solucao, Agora);
        return null;
    }

    public async Task<OcorrenciaDetalhe> ResolverComFiltro(int id, int tecnicoId, string solucao, CancellationToken ct)
    {
        var segura = privacidade.Filtrar(solucao);
        // ok: hakari.taint.texto-livre-sem-filtro
        ocorrencia.Resolver(tecnico, segura.Texto, Agora);
        return null;
    }

    public void EventoDoSistema(Ocorrencia ocorrencia)
    {
        // ok: hakari.taint.texto-livre-sem-filtro
        ocorrencia.RegistrarEvento("Alerta normalizado", Agora);
    }
}

public class CasosLog
{
    public void Cadastro(CadastroRequest pedido, ILogger log)
    {
        // ruleid: hakari.taint.pii-em-log
        log.LogInformation("Novo cadastro de {Email}", pedido.Email);
        // ruleid: hakari.taint.pii-em-log
        log.LogWarning($"Telefone inválido: {pedido.Telefone}");
        // ruleid: hakari.taint.pii-em-log
        Console.WriteLine("Senha recebida: " + pedido.Senha);
        // ok: hakari.taint.pii-em-log
        log.LogInformation("Cadastro concluído para o usuário {Id}", cidadao.Id);
        // ok: hakari.taint.pii-em-log
        log.LogInformation("Atendido por {Tecnico}", tecnico.NomeAbreviado());
        // ok: hakari.taint.pii-em-log
        log.LogInformation("Nó {Codigo} isolado", poste.Codigo);
    }

    public void Reporte(ReporteRequest pedido, ILogger log)
    {
        var filtrado = privacidade.Filtrar(pedido.Descricao);
        // ok: hakari.taint.pii-em-log
        log.LogInformation("Reporte recebido: {Texto}", filtrado.Texto);
        // ruleid: hakari.taint.pii-em-log
        log.LogDebug("Reporte bruto: {Texto}", pedido.Descricao);
    }
}

public class CasosExcecao
{
    public void Validar(CadastroRequest pedido)
    {
        if (existe)
            // ruleid: hakari.taint.pii-em-excecao
            throw new ConflitoException($"O e-mail {pedido.Email} já está em uso");
        if (outro)
            // ok: hakari.taint.pii-em-excecao
            throw new ConflitoException("Este e-mail já está em uso");
    }
}

public class CasosToken
{
    public void Gerar(Usuario usuario)
    {
        var claims = new[]
        {
            // ok: hakari.taint.pii-no-token
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            // ruleid: hakari.taint.pii-no-token
            new Claim("name", usuario.Nome),
            // ruleid: hakari.taint.pii-no-token
            new Claim("email", usuario.Email),
            // ok: hakari.taint.pii-no-token
            new Claim("role", usuario.Perfil.ToString())
        };
    }
}

public class CasosSenha
{
    public void Cadastrar(CadastroRequest pedido)
    {
        // ok: hakari.taint.senha-sem-hash
        var certo = new Cidadao(pedido.Nome, email, pedido.Telefone, hasher.Gerar(pedido.Senha), agora);
        // ruleid: hakari.taint.senha-sem-hash
        var errado = new Cidadao(pedido.Nome, email, pedido.Telefone, pedido.Senha, agora);
    }

    public static async Task SemearAsync(HakariDbContext db, IHasherDeSenha hasher, string? senhaTecnico)
    {
        // ok: hakari.taint.senha-sem-hash
        db.Usuarios.Add(new Tecnico("Técnico", "tecnico@hakari.dev", "11900000000", hasher.Gerar(senhaTecnico), "MNT-1", agora));
    }

    public static async Task SemearErrado(HakariDbContext db, string? senhaTecnico)
    {
        // ruleid: hakari.taint.senha-sem-hash
        db.Usuarios.Add(new Tecnico("Técnico", "tecnico@hakari.dev", "11900000000", senhaTecnico, "MNT-1", agora));
    }
}

public class CasosLimites
{
    public void EventoComMetodo(Ocorrencia ocorrencia, Alerta alerta)
    {
        // ok: hakari.taint.texto-livre-sem-filtro
        ocorrencia.RegistrarEvento($"Alerta normalizado: {alerta.Trigger.Descricao()}", Agora);
    }

    // Limitação conhecida do Semgrep 1.183 para C#: depois de uma expressão "?? throw", o motor de
    // taint considera o resto do método inalcançável e não segue o fluxo. O Hakari cobre esse ponto
    // cego com o tipo TextoSeguro (o compilador recusa texto sem filtro) e com o teste de canários.
    public async Task<ReporteResponse> DepoisDeThrowExpression(int posteId, ReporteRequest pedido)
    {
        var poste = await db.Postes.FirstOrDefaultAsync(p => p.Id == posteId)
                    ?? throw new NaoEncontradoException("Poste não encontrado");
        // todoruleid: hakari.taint.texto-livre-sem-filtro
        var nova = Ocorrencia.AbrirPorCidadao(poste, cidadao, pedido.Categoria, pedido.Descricao, Agora);
        return null;
    }
}
