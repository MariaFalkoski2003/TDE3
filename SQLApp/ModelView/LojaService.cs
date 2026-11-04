using SQLite;
using SQLApp.Model;

namespace SQLApp.ModelView;

/// <summary>CRUD das quatro tabelas e validação de entrada antes do acesso ao banco.</summary>
public sealed class LojaService(string caminhoBanco) : IAsyncDisposable
{
    private readonly SemaphoreSlim _acesso = new(1, 1);
    private SQLiteConnection? _db;
    private bool _disposed;

    private SQLiteConnection Abrir()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_db != null) return _db;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminhoBanco))!);
        var db = new SQLiteConnection(caminhoBanco, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
            storeDateTimeAsTicks: false);
        try
        {
            db.Execute("PRAGMA foreign_keys = ON");
            db.RunInTransaction(() =>
            {
                db.Execute("""
                    CREATE TABLE IF NOT EXISTS Cliente (
                        CodigoCliente INTEGER PRIMARY KEY AUTOINCREMENT,
                        Nome TEXT NOT NULL CHECK(length(trim(Nome)) BETWEEN 1 AND 50),
                        RG INTEGER NOT NULL CHECK(RG BETWEEN 1 AND 9999999999),
                        Telefone TEXT NOT NULL CHECK(length(trim(Telefone)) BETWEEN 1 AND 14))
                    """);
                db.Execute("""
                    CREATE TABLE IF NOT EXISTS Produto (
                        CodigoProduto INTEGER PRIMARY KEY AUTOINCREMENT,
                        NomeProduto TEXT NOT NULL CHECK(length(trim(NomeProduto)) BETWEEN 1 AND 50),
                        Tipo TEXT NOT NULL CHECK(length(trim(Tipo)) BETWEEN 1 AND 50),
                        Preco TEXT NOT NULL,
                        QtdEstoque INTEGER NOT NULL CHECK(QtdEstoque >= 0))
                    """);
                db.Execute("""
                    CREATE TABLE IF NOT EXISTS Compra (
                        CodigoCompra INTEGER PRIMARY KEY AUTOINCREMENT,
                        DataCompra TEXT NOT NULL,
                        ValorTotal TEXT NOT NULL,
                        CodigoCliente INTEGER NOT NULL REFERENCES Cliente(CodigoCliente) ON DELETE RESTRICT)
                    """);
                db.Execute("""
                    CREATE TABLE IF NOT EXISTS ItemCompra (
                        CodigoItem INTEGER PRIMARY KEY AUTOINCREMENT,
                        CodigoProduto INTEGER NOT NULL REFERENCES Produto(CodigoProduto) ON DELETE RESTRICT,
                        CodigoCompra INTEGER NOT NULL REFERENCES Compra(CodigoCompra) ON DELETE RESTRICT,
                        Quantidade INTEGER NOT NULL CHECK(Quantidade > 0),
                        ValorParcial TEXT NOT NULL)
                    """);
            });
            return _db = db;
        }
        catch { db.Dispose(); throw; }
    }

    private async Task<T> Executar<T>(Func<SQLiteConnection, T> operacao)
    {
        await _acesso.WaitAsync().ConfigureAwait(false);
        try { return await Task.Run(() => operacao(Abrir())).ConfigureAwait(false); }
        finally { _acesso.Release(); }
    }
    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new RegraNegocioException(mensagem);
    }
    private static void Id(int id) => Exigir(id > 0, "Informe um código inteiro positivo.");
    private static string Texto(string? valor, string campo, int limite)
    {
        var texto = valor?.Trim() ?? "";
        Exigir(texto.Length > 0 && texto.Length <= limite, $"{campo} é obrigatório e deve ter até {limite} caracteres.");
        return texto;
    }
    private static T Buscar<T>(SQLiteConnection db, int id) where T : new() =>
        db.Find<T>(id) ?? throw new RegraNegocioException($"Registro de {typeof(T).Name} não encontrado.");
    private static int Salvar<T>(SQLiteConnection db, T registro, int id, Func<T, int> codigo) where T : new()
    {
        if (id == 0) db.Insert(registro);
        else { Buscar<T>(db, id); db.Update(registro); }
        return codigo(registro);
    }

    public Task<List<Cliente>> ClientesAsync() => Executar(db => db.Table<Cliente>().ToList());
    public Task<Cliente> ClienteAsync(int id) { Id(id); return Executar(db => Buscar<Cliente>(db, id)); }
    public Task<int> SalvarClienteAsync(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        Exigir(cliente.CodigoCliente >= 0, "Código de cliente inválido.");
        var c = new Cliente { CodigoCliente = cliente.CodigoCliente, Nome = Texto(cliente.Nome, "Nome", 50), RG = cliente.RG, Telefone = Texto(cliente.Telefone, "Telefone", 14) };
        Exigir(c.RG is > 0 and <= 9_999_999_999, "RG deve ser numérico e ter até 10 dígitos.");
        return Executar(db => Salvar(db, c, c.CodigoCliente, x => x.CodigoCliente));
    }
    public Task<int> ExcluirClienteAsync(int id)
    {
        Id(id);
        return Executar(db =>
        {
            Buscar<Cliente>(db, id);
            Exigir(db.Table<Compra>().Count(c => c.CodigoCliente == id) == 0, "Exclua as compras deste cliente antes de excluir o cliente.");
            return db.Delete<Cliente>(id);
        });
    }

    public Task<List<Produto>> ProdutosAsync() => Executar(db => db.Table<Produto>().ToList());
    public Task<Produto> ProdutoAsync(int id) { Id(id); return Executar(db => Buscar<Produto>(db, id)); }
    public Task<int> SalvarProdutoAsync(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);
        Exigir(produto.CodigoProduto >= 0, "Código de produto inválido.");
        var p = new Produto { CodigoProduto = produto.CodigoProduto, NomeProduto = Texto(produto.NomeProduto, "Nome do produto", 50), Tipo = Texto(produto.Tipo, "Tipo", 50), Preco = produto.Preco, QtdEstoque = produto.QtdEstoque };
        Exigir(p.Preco > 0, "Preço deve ser maior que zero.");
        Exigir(p.QtdEstoque >= 0, "Estoque não pode ser negativo.");
        return Executar(db => Salvar(db, p, p.CodigoProduto, x => x.CodigoProduto));
    }
    public Task<int> ExcluirProdutoAsync(int id)
    {
        Id(id);
        return Executar(db =>
        {
            Buscar<Produto>(db, id);
            Exigir(db.Table<ItemCompra>().Count(i => i.CodigoProduto == id) == 0, "Exclua os itens deste produto antes de excluir o produto.");
            return db.Delete<Produto>(id);
        });
    }

    public Task<List<Compra>> ComprasAsync() => Executar(db => db.Table<Compra>().ToList());
    public Task<Compra> CompraAsync(int id) { Id(id); return Executar(db => Buscar<Compra>(db, id)); }
    public Task<int> SalvarCompraAsync(Compra compra)
    {
        ArgumentNullException.ThrowIfNull(compra);
        Exigir(compra.CodigoCompra >= 0, "Código de compra inválido."); Id(compra.CodigoCliente);
        Exigir(compra.DataCompra != default, "Informe uma data válida para a compra.");
        Exigir(compra.ValorTotal >= 0, "Valor total não pode ser negativo.");
        var c = new Compra { CodigoCompra = compra.CodigoCompra, CodigoCliente = compra.CodigoCliente, DataCompra = compra.DataCompra.Date, ValorTotal = compra.ValorTotal };
        return Executar(db => { Buscar<Cliente>(db, c.CodigoCliente); return Salvar(db, c, c.CodigoCompra, x => x.CodigoCompra); });
    }
    public Task<int> ExcluirCompraAsync(int id)
    {
        Id(id);
        return Executar(db =>
        {
            Buscar<Compra>(db, id);
            Exigir(db.Table<ItemCompra>().Count(i => i.CodigoCompra == id) == 0, "Exclua os itens desta compra antes de excluir a compra.");
            return db.Delete<Compra>(id);
        });
    }

    public Task<List<ItemCompra>> ItensAsync(int codigoCompra)
    {
        Id(codigoCompra);
        return Executar(db => { Buscar<Compra>(db, codigoCompra); return db.Table<ItemCompra>().Where(i => i.CodigoCompra == codigoCompra).ToList(); });
    }
    public Task<ItemCompra> ItemAsync(int id) { Id(id); return Executar(db => Buscar<ItemCompra>(db, id)); }
    public Task<int> SalvarItemAsync(ItemCompra item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Exigir(item.CodigoItem >= 0, "Código de item inválido."); Id(item.CodigoCompra); Id(item.CodigoProduto);
        Exigir(item.Quantidade > 0, "Quantidade deve ser um inteiro maior que zero.");
        Exigir(item.ValorParcial >= 0, "Valor parcial não pode ser negativo.");
        var i = new ItemCompra { CodigoItem = item.CodigoItem, CodigoCompra = item.CodigoCompra, CodigoProduto = item.CodigoProduto, Quantidade = item.Quantidade, ValorParcial = item.ValorParcial };
        return Executar(db =>
        {
            Buscar<Compra>(db, i.CodigoCompra); Buscar<Produto>(db, i.CodigoProduto);
            return Salvar(db, i, i.CodigoItem, x => x.CodigoItem);
        });
    }
    public Task<int> ExcluirItemAsync(int id)
    {
        Id(id); return Executar(db => { Buscar<ItemCompra>(db, id); return db.Delete<ItemCompra>(id); });
    }
    public async ValueTask DisposeAsync()
    {
        await _acesso.WaitAsync().ConfigureAwait(false);
        try { _db?.Dispose(); _db = null; _disposed = true; }
        finally { _acesso.Release(); }
    }
}
