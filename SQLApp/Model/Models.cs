using System.Globalization;
using SQLite;

namespace SQLApp.Model;

[Table("Cliente")]
public class Cliente
{
    [PrimaryKey, AutoIncrement] public int CodigoCliente { get; set; }
    public string Nome { get; set; } = "";
    public long RG { get; set; }
    public string Telefone { get; set; } = "";
    public override string ToString() => $"{CodigoCliente} - {Nome}";
}

[Table("Produto")]
public class Produto
{
    [PrimaryKey, AutoIncrement] public int CodigoProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public string Tipo { get; set; } = "";
    // SQLite não possui MONEY. Texto decimal preserva o valor sem conversão para double.
    [Column("Preco")] public string PrecoSql { get; set; } = "0";
    [Ignore] public decimal Preco
    {
        get => decimal.Parse(PrecoSql, CultureInfo.InvariantCulture);
        set => PrecoSql = value.ToString(CultureInfo.InvariantCulture);
    }
    public int QtdEstoque { get; set; }
    public override string ToString() => $"{CodigoProduto} - {NomeProduto}";
}

[Table("Compra")]
public class Compra
{
    [PrimaryKey, AutoIncrement] public int CodigoCompra { get; set; }
    public DateTime DataCompra { get; set; } = DateTime.Today;
    [Column("ValorTotal")] public string ValorTotalSql { get; set; } = "0";
    [Ignore] public decimal ValorTotal
    {
        get => decimal.Parse(ValorTotalSql, CultureInfo.InvariantCulture);
        set => ValorTotalSql = value.ToString(CultureInfo.InvariantCulture);
    }
    public int CodigoCliente { get; set; }
    public override string ToString() => $"Compra {CodigoCompra} | {DataCompra:dd/MM/yyyy} | Valor total: {ValorTotal}";
}

[Table("ItemCompra")]
public class ItemCompra
{
    [PrimaryKey, AutoIncrement] public int CodigoItem { get; set; }
    public int CodigoProduto { get; set; }
    public int CodigoCompra { get; set; }
    public int Quantidade { get; set; }
    [Column("ValorParcial")] public string ValorParcialSql { get; set; } = "0";
    [Ignore] public decimal ValorParcial
    {
        get => decimal.Parse(ValorParcialSql, CultureInfo.InvariantCulture);
        set => ValorParcialSql = value.ToString(CultureInfo.InvariantCulture);
    }
    public override string ToString() => $"Item {CodigoItem} | produto {CodigoProduto} | quantidade {Quantidade} | Valor parcial: {ValorParcial}";
}

public sealed class RegraNegocioException(string mensagem) : Exception(mensagem);
