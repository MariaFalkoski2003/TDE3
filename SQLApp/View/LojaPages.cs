using System.Globalization;
using SQLApp.Model;
using SQLApp.ModelView;

namespace SQLApp.View;

public static class LojaContext
{
    public static LojaService Banco { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "tde.db3"));
}

public abstract class LojaPage : ContentPage
{
    protected readonly LojaService Banco = LojaContext.Banco;
    protected static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private bool _ocupado;
    protected LojaPage(string titulo) { Title = titulo; }
    protected async Task Executar(Func<Task> acao)
    {
        if (_ocupado) return;
        _ocupado = true;
        if (Content != null) Content.IsEnabled = false;
        try { await acao(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlertAsync("Não foi possível concluir", ex.Message, "OK");
        }
        finally { if (Content != null) Content.IsEnabled = true; _ocupado = false; }
    }
    protected Button Botao(string texto, Func<Task> acao, string id)
    {
        var b = new Button { Text = texto, AutomationId = id, Margin = new Thickness(0, 3) };
        b.Clicked += async (_, _) => await Executar(acao);
        return b;
    }
    protected static Entry Campo(string dica, string id, Keyboard? teclado = null) => new()
    { Placeholder = dica, AutomationId = id, Keyboard = teclado ?? Keyboard.Default };
    protected static VerticalStackLayout Formulario(params Microsoft.Maui.Controls.View[] views)
    {
        var layout = new VerticalStackLayout { Spacing = 7 };
        foreach (var view in views) layout.Children.Add(view);
        return layout;
    }
    protected static Label Texto(string texto) => new() { Text = texto, FontSize = 14 };
    protected static Label Titulo(string texto) => new() { Text = texto, FontSize = 22, FontAttributes = FontAttributes.Bold };
    protected static FlexLayout Botoes(params Button[] botoes)
    {
        var linha = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        foreach (var b in botoes) { b.Margin = new Thickness(0, 0, 8, 0); linha.Children.Add(b); }
        return linha;
    }
    protected static int Inteiro(string? valor, string nome, bool zero = false)
    {
        if (!int.TryParse(valor?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < (zero ? 0 : 1))
            throw new RegraNegocioException($"{nome}: informe um inteiro {(zero ? "não negativo" : "positivo")} válido.");
        return n;
    }
    protected static long RG(string? texto)
    {
        if (!long.TryParse(texto?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            throw new RegraNegocioException("RG deve conter somente dígitos, até 10 posições.");
        return n;
    }
    protected static decimal Valor(string? texto)
    {
        var s = texto?.Trim() ?? "";
        if (!decimal.TryParse(s.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n))
            throw new RegraNegocioException("Valor inválido. Use, por exemplo, 12,50 ou 12.50.");
        return n;
    }
    protected static CollectionView Lista<T>(string id, Func<T, string> descrever, Action<T> selecionar)
    {
        var lista = new CollectionView { AutomationId = id, SelectionMode = SelectionMode.Single, HeightRequest = 240,
            EmptyView = "Nenhum registro cadastrado." };
        lista.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label { Padding = 10, LineBreakMode = LineBreakMode.WordWrap };
            label.BindingContextChanged += (_, _) => { if (label.BindingContext is T item) label.Text = descrever(item); };
            return label;
        });
        lista.SelectionChanged += (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is T item) { selecionar(item); lista.SelectedItem = null; }
        };
        return lista;
    }
    protected void Corpo(VerticalStackLayout conteudo)
    {
        if (Title != "Menu principal")
            conteudo.Children.Insert(0, Botao("Menu principal", () => Shell.Current.GoToAsync("//inicio"), "VoltarMenu"));
        Content = new ScrollView { Content = conteudo, Padding = new Thickness(18), HorizontalOptions = LayoutOptions.Fill };
    }
}

public sealed class MenuPage : LojaPage
{
    public MenuPage() : base("Menu principal")
    {
        Corpo(Formulario(Titulo("SQLApp • Loja"), Texto("Banco de dados relacional local"),
            Botao("Cadastro de Clientes", () => Shell.Current.GoToAsync("//clientes"), "MenuClientes"),
            Botao("Cadastro de Produtos", () => Shell.Current.GoToAsync("//produtos"), "MenuProdutos"),
            Botao("Compras e Itens", () => Shell.Current.GoToAsync("//compras"), "MenuCompras"),
            Texto("1. Cadastre clientes e produtos.\n2. Salve uma compra para o cliente.\n3. Cadastre os itens da compra.\n\nOs dados ficam salvos neste dispositivo.")));
    }
}

public sealed class ClientesPage : LojaPage
{
    private int _id;
    private readonly Label _selecao = Texto("Novo cliente");
    private readonly Entry _consulta = Campo("Código para consultar", "ClienteConsulta", Keyboard.Numeric);
    private readonly Entry _nome = Campo("Nome (até 50 caracteres)", "ClienteNome");
    private readonly Entry _rg = Campo("RG numérico (até 10 dígitos)", "ClienteRG", Keyboard.Numeric);
    private readonly Entry _telefone = Campo("Telefone (até 14 caracteres)", "ClienteTelefone", Keyboard.Telephone);
    private readonly CollectionView _lista;
    public ClientesPage() : base("Cadastro de Clientes")
    {
        _lista = Lista<Cliente>("ListaClientes", c => $"{c}\nRG: {c.RG} | Tel.: {c.Telefone}", Preencher);
        Corpo(Formulario(Titulo("Clientes"), _selecao, _consulta,
            Botao("Consultar código", async () => Preencher(await Banco.ClienteAsync(Inteiro(_consulta.Text, "Código"))), "ConsultarCliente"),
            _nome, _rg, _telefone,
            Botoes(Botao("Salvar cliente", Salvar, "SalvarCliente"), Botao("Novo / Limpar", () => { Limpar(); return Task.CompletedTask; }, "NovoCliente"),
                Botao("Excluir cliente", Excluir, "ExcluirCliente")),
            Botao("Atualizar lista", Atualizar, "AtualizarClientes"), Texto("Toque em um cliente para editar ou excluir."), _lista));
    }
    protected override async void OnAppearing() { base.OnAppearing(); await Executar(Atualizar); }
    private async Task Atualizar() => _lista.ItemsSource = await Banco.ClientesAsync();
    private void Preencher(Cliente c) { _id = c.CodigoCliente; _selecao.Text = $"Editando cliente {_id}"; _nome.Text = c.Nome; _rg.Text = c.RG.ToString(); _telefone.Text = c.Telefone; }
    private void Limpar() { _id = 0; _selecao.Text = "Novo cliente"; _consulta.Text = _nome.Text = _rg.Text = _telefone.Text = ""; }
    private async Task Salvar()
    {
        await Banco.SalvarClienteAsync(new Cliente { CodigoCliente = _id, Nome = _nome.Text, RG = RG(_rg.Text), Telefone = _telefone.Text });
        Limpar(); await Atualizar(); await DisplayAlertAsync("Sucesso", "Cliente salvo.", "OK");
    }
    private async Task Excluir()
    {
        if (_id == 0) throw new RegraNegocioException("Selecione ou consulte um cliente.");
        await Banco.ExcluirClienteAsync(_id); Limpar(); await Atualizar();
    }
}

public sealed class ProdutosPage : LojaPage
{
    private int _id;
    private readonly Label _selecao = Texto("Novo produto");
    private readonly Entry _consulta = Campo("Código para consultar", "ProdutoConsulta", Keyboard.Numeric);
    private readonly Entry _nome = Campo("Nome do produto (até 50 caracteres)", "ProdutoNome");
    private readonly Entry _tipo = Campo("Tipo (até 50 caracteres)", "ProdutoTipo");
    private readonly Entry _preco = Campo("Preço, ex.: 12,50 (sem milhar)", "ProdutoPreco", Keyboard.Numeric);
    private readonly Entry _estoque = Campo("Estoque disponível", "ProdutoEstoque", Keyboard.Numeric);
    private readonly CollectionView _lista;
    public ProdutosPage() : base("Cadastro de Produtos")
    {
        _lista = Lista<Produto>("ListaProdutos", p => $"{p}\n{p.Tipo} | R$ {p.Preco.ToString("N2", Pt)}", Preencher);
        Corpo(Formulario(Titulo("Produtos"), _selecao, _consulta,
            Botao("Consultar código", async () => Preencher(await Banco.ProdutoAsync(Inteiro(_consulta.Text, "Código"))), "ConsultarProduto"),
            _nome, _tipo, _preco, _estoque,
            Botoes(Botao("Salvar produto", Salvar, "SalvarProduto"), Botao("Novo / Limpar", () => { Limpar(); return Task.CompletedTask; }, "NovoProduto"),
                Botao("Excluir produto", Excluir, "ExcluirProduto")),
            Botao("Atualizar lista", Atualizar, "AtualizarProdutos"), Texto("Toque em um produto para editar ou excluir."), _lista));
    }
    protected override async void OnAppearing() { base.OnAppearing(); await Executar(Atualizar); }
    private async Task Atualizar()
    {
        _lista.ItemsSource = await Banco.ProdutosAsync();
    }
    private void Preencher(Produto p) { _id = p.CodigoProduto; _selecao.Text = $"Editando produto {_id}"; _nome.Text = p.NomeProduto; _tipo.Text = p.Tipo; _preco.Text = p.Preco.ToString(Pt); _estoque.Text = p.QtdEstoque.ToString(); }
    private void Limpar() { _id = 0; _selecao.Text = "Novo produto"; _consulta.Text = _nome.Text = _tipo.Text = _preco.Text = _estoque.Text = ""; }
    private async Task Salvar()
    {
        await Banco.SalvarProdutoAsync(new Produto { CodigoProduto = _id, NomeProduto = _nome.Text, Tipo = _tipo.Text, Preco = Valor(_preco.Text), QtdEstoque = Inteiro(_estoque.Text, "Estoque", true) });
        Limpar(); await Atualizar(); await DisplayAlertAsync("Sucesso", "Produto salvo.", "OK");
    }
    private async Task Excluir()
    {
        if (_id == 0) throw new RegraNegocioException("Selecione ou consulte um produto.");
        await Banco.ExcluirProdutoAsync(_id); Limpar(); await Atualizar();
    }
}

public sealed class ComprasPage : LojaPage
{
    private int _compraId, _itemId;
    private readonly Label _selecao = Texto("Nova compra");
    private readonly Entry _total = Campo("Valor total", "CompraValorTotal", Keyboard.Numeric);
    private readonly Label _itemSelecao = Texto("Novo item");
    private readonly Entry _consulta = Campo("Código da compra para consultar", "CompraConsulta", Keyboard.Numeric);
    private readonly Picker _cliente = new() { Title = "Selecione o cliente", AutomationId = "CompraCliente" };
    private readonly Entry _data = new() { Text = DateTime.Today.ToString("dd/MM/yyyy"), Placeholder = "Data da compra (dd/MM/aaaa)", AutomationId = "CompraData" };
    private readonly Picker _produto = new() { Title = "Selecione o produto", AutomationId = "ItemProduto" };
    private readonly Entry _quantidade = Campo("Quantidade inteira", "ItemQuantidade", Keyboard.Numeric);
    private readonly Entry _parcial = Campo("Valor parcial", "ItemValorParcial", Keyboard.Numeric);
    private readonly CollectionView _compras, _itens;
    public ComprasPage() : base("Compras e Itens")
    {
        _compras = Lista<Compra>("ListaCompras", c => $"{c} | cliente {c.CodigoCliente}", c => { _ = Executar(() => AbrirCompra(c.CodigoCompra)); });
        _itens = Lista<ItemCompra>("ListaItens", i => i.ToString(), i =>
        {
            _itemId = i.CodigoItem; _itemSelecao.Text = $"Editando item {_itemId}";
            _produto.SelectedItem = (_produto.ItemsSource as List<Produto>)?.FirstOrDefault(p => p.CodigoProduto == i.CodigoProduto);
            _quantidade.Text = i.Quantidade.ToString(); _parcial.Text = i.ValorParcial.ToString(Pt);
        });
        Corpo(Formulario(Titulo("Compra"), _selecao, _consulta,
            Botao("Consultar compra", () => AbrirCompra(Inteiro(_consulta.Text, "Código")), "ConsultarCompra"),
            _cliente, Texto("Data da compra (dd/MM/aaaa)"), _data, Texto("Valor total"), _total,
            Botoes(Botao("Salvar compra", SalvarCompra, "SalvarCompra"), Botao("Nova / Limpar", () => { LimparCompra(); return Task.CompletedTask; }, "NovaCompra"),
                Botao("Excluir compra", ExcluirCompra, "ExcluirCompra")),
            Texto("Salve a compra antes de incluir itens. Selecione um item na lista para alterá-lo."),
            Titulo("Itens da compra selecionada"), _itemSelecao, _produto, _quantidade, Texto("Valor parcial"), _parcial,
            Botoes(Botao("Salvar item", SalvarItem, "SalvarItem"), Botao("Novo item", () => { LimparItem(); return Task.CompletedTask; }, "NovoItem"),
                Botao("Excluir item", ExcluirItem, "ExcluirItem")), _itens,
            Titulo("Compras cadastradas"), Botao("Atualizar compras e opções", Atualizar, "AtualizarCompras"), _compras));
    }
    protected override async void OnAppearing() { base.OnAppearing(); await Executar(Atualizar); }
    private async Task Atualizar()
    {
        var clienteId = (_cliente.SelectedItem as Cliente)?.CodigoCliente;
        var produtoId = (_produto.SelectedItem as Produto)?.CodigoProduto;
        var clientes = await Banco.ClientesAsync(); var produtos = await Banco.ProdutosAsync();
        _cliente.ItemsSource = clientes; _produto.ItemsSource = produtos;
        _cliente.SelectedItem = clientes.FirstOrDefault(c => c.CodigoCliente == clienteId);
        _produto.SelectedItem = produtos.FirstOrDefault(p => p.CodigoProduto == produtoId);
        _compras.ItemsSource = await Banco.ComprasAsync();
        if (_compraId > 0) await AtualizarItens();
    }
    private async Task AbrirCompra(int id)
    {
        var c = await Banco.CompraAsync(id);
        _compraId = c.CodigoCompra; _selecao.Text = $"Editando compra {_compraId}";
        _cliente.SelectedItem = (_cliente.ItemsSource as List<Cliente>)?.FirstOrDefault(x => x.CodigoCliente == c.CodigoCliente);
        _data.Text = c.DataCompra.ToString("dd/MM/yyyy"); LimparItem(); await AtualizarItens();
    }
    private async Task AtualizarItens()
    {
        _itens.ItemsSource = await Banco.ItensAsync(_compraId);
        var c = await Banco.CompraAsync(_compraId);
        _total.Text = c.ValorTotal.ToString(Pt);
    }
    private void LimparItem() { _itemId = 0; _itemSelecao.Text = "Novo item"; _produto.SelectedItem = null; _quantidade.Text = _parcial.Text = ""; }
    private void LimparCompra()
    {
        _compraId = 0; _selecao.Text = "Nova compra"; _consulta.Text = ""; _cliente.SelectedItem = null; _data.Text = DateTime.Today.ToString("dd/MM/yyyy");
        _itens.ItemsSource = null; _total.Text = ""; LimparItem();
    }
    private async Task SalvarCompra()
    {
        if (_cliente.SelectedItem is not Cliente c) throw new RegraNegocioException("Selecione um cliente cadastrado.");
        if (!DateTime.TryParseExact(_data.Text, "dd/MM/yyyy", Pt, DateTimeStyles.None, out var data))
            throw new RegraNegocioException("Informe uma data válida no formato dd/MM/aaaa.");
        _compraId = await Banco.SalvarCompraAsync(new Compra { CodigoCompra = _compraId, CodigoCliente = c.CodigoCliente, DataCompra = data, ValorTotal = Valor(_total.Text) });
        await Atualizar(); await AbrirCompra(_compraId); await DisplayAlertAsync("Sucesso", "Compra salva.", "OK");
    }
    private async Task ExcluirCompra()
    {
        if (_compraId == 0) throw new RegraNegocioException("Selecione ou consulte uma compra.");
        await Banco.ExcluirCompraAsync(_compraId); LimparCompra(); await Atualizar();
    }
    private async Task SalvarItem()
    {
        if (_compraId == 0) throw new RegraNegocioException("Salve ou selecione uma compra antes de incluir itens.");
        if (_produto.SelectedItem is not Produto p) throw new RegraNegocioException("Selecione um produto cadastrado.");
        await Banco.SalvarItemAsync(new ItemCompra { CodigoItem = _itemId, CodigoCompra = _compraId, CodigoProduto = p.CodigoProduto, Quantidade = Inteiro(_quantidade.Text, "Quantidade"), ValorParcial = Valor(_parcial.Text) });
        LimparItem(); await Atualizar(); await DisplayAlertAsync("Sucesso", "Item salvo.", "OK");
    }
    private async Task ExcluirItem()
    {
        if (_itemId == 0) throw new RegraNegocioException("Selecione um item na lista.");
        await Banco.ExcluirItemAsync(_itemId); LimparItem(); await Atualizar();
    }
}
