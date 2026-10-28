using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace Libro_Tienda
{
    public class Articulo
    {
        public string Tipo { get; set; } = "";
        public int Cantidad { get; set; } = 1;
    }

    public class Venta
    {
        public string Prenda { get; set; } = "";
        public int Cantidad { get; set; } = 1;
        public decimal PrecioUnitario { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Today;

        [JsonIgnore]
        public decimal Total { get { return PrecioUnitario * Cantidad; } }
    }

    public class Paca
    {
        public string Nombre { get; set; } = "";
        public decimal CostoTotal { get; set; }
        public DateTime FechaCompra { get; set; } = DateTime.Today;
        public List<Articulo> Items { get; set; } = new List<Articulo>();
        public List<Venta> Ventas { get; set; } = new List<Venta>();

        // Campos de versiones anteriores (solo para convertir datos viejos)
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Costo { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Cantidad { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? PrecioVenta { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? FechaVenta { get; set; }

        [JsonIgnore]
        public decimal TotalVendido { get { return Ventas.Sum(v => v.Total); } }
        [JsonIgnore]
        public int UnidadesVendidas { get { return Ventas.Sum(v => v.Cantidad); } }
        [JsonIgnore]
        public int UnidadesCompradas { get { return Items.Sum(a => a.Cantidad); } }
        [JsonIgnore]
        public int EnStock { get { return Items.Sum(a => Disponibles(a)); } }
        [JsonIgnore]
        public decimal Ganancia { get { return TotalVendido - CostoTotal; } }

        public int VendidasDe(string tipo)
        {
            return Ventas
                .Where(v => string.Equals(v.Prenda, tipo, StringComparison.OrdinalIgnoreCase))
                .Sum(v => v.Cantidad);
        }

        public int Disponibles(Articulo a)
        {
            return a.Cantidad - VendidasDe(a.Tipo);
        }

        public override string ToString()
        {
            return Nombre + "  (" + FechaCompra.ToShortDateString() + ")";
        }
    }

    class Fila
    {
        public Paca P;
        public Venta V;
    }

    class ItemStock
    {
        public Articulo A;
        public int Quedan;
        public override string ToString()
        {
            return A.Tipo + "  (quedan " + Quedan + ")";
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        List<Paca> pacas = new List<Paca>();
        List<Articulo> borrador = new List<Articulo>();   // contenido de la paca que se está editando
        readonly string carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Libro_Tienda");
        string archivo => Path.Combine(carpeta, "libro_tienda.json");
        bool cargando = false;
        bool cargandoD = false;
        bool ignorar = false;

        // Navegación
        Panel pageCompras, pageVentas;
        Button btnCompras, btnVentas;

        // Página Compras
        TextBox cNombre, cCosto, cTipo;
        NumericUpDown cCantItem;
        DateTimePicker cFecha;
        DataGridView gridC, gridD;
        Label lblTotC;

        // Página Ventas
        ComboBox vPaca, vPrenda;
        TextBox vPrecio;
        NumericUpDown vCantidad;
        DateTimePicker vFecha;
        DataGridView gridV;
        Label lblTotV;

        public MainForm()
        {
            Text = "Libro de Ingresos y Gastos - Tienda de Ropa";
            Size = new Size(1000, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            ConstruirCompras();
            ConstruirVentas();

            // Barra de navegación (se añade al final para que quede arriba)
            var nav = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(23, 54, 107) };
            btnCompras = CrearBotonNav("Compras", 10);
            btnVentas = CrearBotonNav("Ventas", 160);
            btnCompras.Click += (s, e) => MostrarPagina(true);
            btnVentas.Click += (s, e) => MostrarPagina(false);
            nav.Controls.Add(btnCompras);
            nav.Controls.Add(btnVentas);

            Controls.Add(pageCompras);
            Controls.Add(pageVentas);
            Controls.Add(nav);

            Cargar();
            RefrescarTodo();
            MostrarPagina(true);
        }

        // ---------- Construcción de pantallas ----------

        Button CrearBotonNav(string texto, int x)
        {
            return new Button
            {
                Text = texto,
                Location = new Point(x, 7),
                Size = new Size(140, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
        }

        void MostrarPagina(bool compras)
        {
            pageCompras.Visible = compras;
            pageVentas.Visible = !compras;
            Pintar(btnCompras, compras);
            Pintar(btnVentas, !compras);
        }

        void Pintar(Button b, bool activo)
        {
            b.BackColor = activo ? Color.White : Color.FromArgb(60, 100, 170);
            b.ForeColor = activo ? Color.FromArgb(23, 54, 107) : Color.White;
        }

        DataGridView CrearGrid(string[] columnas)
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };
            foreach (var c in columnas)
            {
                int i = g.Columns.Add(c, c);
                g.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            return g;
        }

        Button CrearBoton(string texto, int x, int y, int ancho)
        {
            return new Button { Text = texto, Location = new Point(x, y), Width = ancho, Height = 30 };
        }

        TextBox CrearAutocompletar(int x, int y, int ancho)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Width = ancho,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.CustomSource
            };
        }

        void ConstruirCompras()
        {
            pageCompras = new Panel { Dock = DockStyle.Fill };

            gridC = CrearGrid(new[] { "Paca", "Costo total", "Fecha compra", "Prendas", "Vendidas", "En stock", "Total vendido", "Ganancia" });
            gridC.SelectionChanged += (s, e) => CargarCompra();
            pageCompras.Controls.Add(gridC);

            var top = new Panel { Dock
