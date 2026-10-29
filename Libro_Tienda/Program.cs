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

            var btnExportar = new Button
            {
                Text = "Exportar para imprimir",
                Location = new Point(330, 7),
                Size = new Size(200, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 184, 92),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnExportar.Click += (s, e) => ExportarReporte();
            nav.Controls.Add(btnExportar);

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

            var top = new Panel { Dock = DockStyle.Top, Height = 220 };
            pageCompras.Controls.Add(top);

            top.Controls.Add(new Label { Text = "Nombre de la paca (ej. Paca 1 mixta)", Location = new Point(10, 10), AutoSize = true });
            cNombre = new TextBox { Location = new Point(10, 30), Width = 230 };
            top.Controls.Add(cNombre);

            top.Controls.Add(new Label { Text = "Costo total de la paca", Location = new Point(255, 10), AutoSize = true });
            cCosto = new TextBox { Location = new Point(255, 30), Width = 120 };
            top.Controls.Add(cCosto);

            top.Controls.Add(new Label { Text = "Fecha de compra", Location = new Point(390, 10), AutoSize = true });
            cFecha = new DateTimePicker { Location = new Point(390, 30), Width = 110, Format = DateTimePickerFormat.Short };
            top.Controls.Add(cFecha);

            top.Controls.Add(new Label
            {
                Text = "Contenido de la paca (qué prendas trae y cuántas)",
                Location = new Point(10, 72),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            });

            top.Controls.Add(new Label { Text = "Prenda", Location = new Point(10, 98), AutoSize = true });
            cTipo = CrearAutocompletar(10, 118, 200);
            top.Controls.Add(cTipo);

            top.Controls.Add(new Label { Text = "Cantidad", Location = new Point(225, 98), AutoSize = true });
            cCantItem = new NumericUpDown { Location = new Point(225, 118), Width = 70, Minimum = 1, Maximum = 100000, Value = 1 };
            top.Controls.Add(cCantItem);

            var bAddItem = CrearBoton("Agregar / actualizar", 310, 115, 130);
            bAddItem.Click += (s, e) => AgregarAlBorrador();
            top.Controls.Add(bAddItem);

            var bDelItem = CrearBoton("Quitar de la lista", 450, 115, 120);
            bDelItem.Click += (s, e) => QuitarDelBorrador();
            top.Controls.Add(bDelItem);

            var bAgregar = CrearBoton("Agregar paca", 10, 170, 110);
            bAgregar.Click += (s, e) => AgregarCompra();
            top.Controls.Add(bAgregar);

            var bGuardar = CrearBoton("Guardar cambios", 130, 170, 120);
            bGuardar.Click += (s, e) => GuardarCompra();
            top.Controls.Add(bGuardar);

            var bEliminar = CrearBoton("Eliminar paca", 260, 170, 110);
            bEliminar.Click += (s, e) => EliminarCompra();
            top.Controls.Add(bEliminar);

            var bLimpiar = CrearBoton("Limpiar", 380, 170, 90);
            bLimpiar.Click += (s, e) => LimpiarCompra();
            top.Controls.Add(bLimpiar);

            // Lista de prendas de la paca que se está registrando / editando
            gridD = CrearGrid(new[] { "Prenda", "Cantidad", "Vendidas", "Quedan" });
            gridD.Dock = DockStyle.None;
            gridD.Location = new Point(600, 10);
            gridD.Size = new Size(375, 200);
            gridD.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gridD.SelectionChanged += (s, e) => CargarItemBorrador();
            top.Controls.Add(gridD);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.WhiteSmoke };
            lblTotC = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0) };
            bottom.Controls.Add(lblTotC);
            pageCompras.Controls.Add(bottom);
        }

        void ConstruirVentas()
        {
            pageVentas = new Panel { Dock = DockStyle.Fill };

            gridV = CrearGrid(new[] { "Paca", "Prenda", "Cantidad", "Precio c/u", "Total venta", "Fecha venta" });
            gridV.SelectionChanged += (s, e) => CargarVenta();
            pageVentas.Controls.Add(gridV);

            var top = new Panel { Dock = DockStyle.Top, Height = 110 };
            pageVentas.Controls.Add(top);

            top.Controls.Add(new Label { Text = "De qué paca salió", Location = new Point(10, 10), AutoSize = true });
            vPaca = new ComboBox { Location = new Point(10, 30), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            vPaca.SelectedIndexChanged += (s, e) => CambioPaca();
            top.Controls.Add(vPaca);

            top.Controls.Add(new Label { Text = "Prenda (con lo que queda)", Location = new Point(245, 10), AutoSize = true });
            vPrenda = new ComboBox { Location = new Point(245, 30), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            vPrenda.SelectedIndexChanged += (s, e) => AjustarMaximo();
            top.Controls.Add(vPrenda);

            top.Controls.Add(new Label { Text = "Cantidad", Location = new Point(460, 10), AutoSize = true });
            vCantidad = new NumericUpDown { Location = new Point(460, 30), Width = 70, Minimum = 1, Maximum = 100000, Value = 1 };
            top.Controls.Add(vCantidad);

            top.Controls.Add(new Label { Text = "Precio por unidad", Location = new Point(545, 10), AutoSize = true });
            vPrecio = new TextBox { Location = new Point(545, 30), Width = 100 };
            top.Controls.Add(vPrecio);

            top.Controls.Add(new Label { Text = "Fecha de venta", Location = new Point(660, 10), AutoSize = true });
            vFecha = new DateTimePicker { Location = new Point(660, 30), Width = 110, Format = DateTimePickerFormat.Short };
            top.Controls.Add(vFecha);

            var bAgregar = CrearBoton("Agregar", 10, 65, 90);
            bAgregar.Click += (s, e) => AgregarVenta();
            top.Controls.Add(bAgregar);

            var bGuardar = CrearBoton("Guardar cambios", 110, 65, 120);
            bGuardar.Click += (s, e) => GuardarVenta();
            top.Controls.Add(bGuardar);

            var bEliminar = CrearBoton("Eliminar", 240, 65, 90);
            bEliminar.Click += (s, e) => EliminarVenta();
            top.Controls.Add(bEliminar);

            var bLimpiar = CrearBoton("Limpiar", 340, 65, 90);
            bLimpiar.Click += (s, e) => LimpiarVenta();
            top.Controls.Add(bLimpiar);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.WhiteSmoke };
            lblTotV = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0) };
            bottom.Controls.Add(lblTotV);
            pageVentas.Controls.Add(bottom);
        }

        // ---------- Utilidades ----------

        static bool Igual(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static bool LeerDecimal(string texto, out decimal valor)
        {
            texto = (texto ?? "").Trim().Replace(',', '.');
            return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
        }

        static decimal AjustarCantidad(NumericUpDown n, int valor)
        {
            return Math.Max(n.Minimum, Math.Min(n.Maximum, valor));
        }

        static Paca SelPaca(DataGridView g)
        {
            if (g.SelectedRows.Count == 0) return null;
            return g.SelectedRows[0].Tag as Paca;
        }

        static Fila SelFila(DataGridView g)
        {
            if (g.SelectedRows.Count == 0) return null;
            return g.SelectedRows[0].Tag as Fila;
        }

        static void SeleccionarPaca(DataGridView g, Paca p)
        {
            foreach (DataGridViewRow r in g.Rows)
            {
                if (r.Tag == p)
                {
                    g.CurrentCell = r.Cells[0];
                    r.Selected = true;
                    break;
                }
            }
        }

        static void SeleccionarVenta(DataGridView g, Venta v)
        {
            foreach (DataGridViewRow r in g.Rows)
            {
                var f = r.Tag as Fila;
                if (f != null && f.V == v)
                {
                    g.CurrentCell = r.Cells[0];
                    r.Selected = true;
                    break;
                }
            }
        }

        static void QuitarSeleccion(DataGridView g)
        {
            g.CurrentCell = null;
            g.ClearSelection();
        }

        static List<Articulo> CopiarItems(IEnumerable<Articulo> origen)
        {
            return origen.Select(a => new Articulo { Tipo = a.Tipo, Cantidad = a.Cantidad }).ToList();
        }

        // ---------- COMPRAS: contenido de la paca (borrador) ----------

        void RefrescarBorrador()
        {
            cargandoD = true;
            gridD.Rows.Clear();
            var p = SelPaca(gridC);
            foreach (var a in borrador)
            {
                int vend = p != null ? p.VendidasDe(a.Tipo) : 0;
                int i = gridD.Rows.Add(a.Tipo, a.Cantidad, vend, a.Cantidad - vend);
                gridD.Rows[i].Tag = a;
            }
            QuitarSeleccion(gridD);
            cargandoD = false;
        }

        void AgregarAlBorrador()
        {
            string tipo = cTipo.Text.Trim();
            if (tipo.Length == 0)
            {
                MessageBox.Show("Escribe el tipo de prenda (ej. pantalón, blusa, abrigo, short).");
                return;
            }
            int cant = (int)cCantItem.Value;
            var existente = borrador.FirstOrDefault(a => Igual(a.Tipo, tipo));
            if (existente != null) existente.Cantidad = cant;
            else borrador.Add(new Articulo { Tipo = tipo, Cantidad = cant });
            cTipo.Text = "";
            cCantItem.Value = 1;
            RefrescarBorrador();
            cTipo.Focus();
        }

        void QuitarDelBorrador()
        {
            if (gridD.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecciona primero una prenda de la lista de la derecha.");
                return;
            }
            var a = gridD.SelectedRows[0].Tag as Articulo;
            if (a == null) return;
            borrador.Remove(a);
            cTipo.Text = "";
            cCantItem.Value = 1;
            RefrescarBorrador();
        }

        void CargarItemBorrador()
        {
            if (cargandoD) return;
            if (gridD.SelectedRows.Count == 0) return;
            var a = gridD.SelectedRows[0].Tag as Articulo;
            if (a == null) return;
            cTipo.Text = a.Tipo;
            cCantItem.Value = AjustarCantidad(cCantItem, a.Cantidad);
        }

        // ---------- COMPRAS (pacas) ----------

        void AgregarCompra()
        {
            if (string.IsNullOrWhiteSpace(cNombre.Text))
            {
                MessageBox.Show("Escribe el nombre de la paca.");
                return;
            }
            decimal costo;
            if (!LeerDecimal(cCosto.Text, out costo) || costo < 0)
            {
                MessageBox.Show("El costo total de la paca no es válido.");
                return;
            }
            if (borrador.Count == 0)
            {
                MessageBox.Show("Agrega al menos una prenda al contenido de la paca (tipo y cantidad).");
                return;
            }
            pacas.Add(new Paca
            {
                Nombre = cNombre.Text.Trim(),
                CostoTotal = costo,
                FechaCompra = cFecha.Value.Date,
                Items = CopiarItems(borrador)
            });
            Guardar();
            RefrescarTodo();
            LimpiarCompra();
        }

        void GuardarCompra()
        {
            var p = SelPaca(gridC);
            if (p == null)
            {
                MessageBox.Show("Selecciona primero una paca de la lista.");
                return;
            }
            if (string.IsNullOrWhiteSpace(cNombre.Text))
            {
                MessageBox.Show("Escribe el nombre de la paca.");
                return;
            }
            decimal costo;
            if (!LeerDecimal(cCosto.Text, out costo) || costo < 0)
            {
                MessageBox.Show("El costo total de la paca no es válido.");
                return;
            }

            // No permitir quitar o reducir prendas que ya se vendieron
            foreach (var viejo in p.Items)
            {
                int vend = p.VendidasDe(viejo.Tipo);
                if (vend <= 0) continue;
                var nuevo = borrador.FirstOrDefault(a => Igual(a.Tipo, viejo.Tipo));
                if (nuevo == null)
                {
                    MessageBox.Show("No puedes quitar \"" + viejo.Tipo + "\": ya vendiste " + vend + " unidades.");
                    return;
                }
                if (nuevo.Cantidad < vend)
                {
                    MessageBox.Show("De \"" + viejo.Tipo + "\" ya vendiste " + vend + " unidades, la cantidad no puede ser menor.");
                    return;
                }
            }

            p.Nombre = cNombre.Text.Trim();
            p.CostoTotal = costo;
            p.FechaCompra = cFecha.Value.Date;
            p.Items = CopiarItems(borrador);
            Guardar();
            RefrescarTodo();
            SeleccionarPaca(gridC, p);
        }

        void EliminarCompra()
        {
            var p = SelPaca(gridC);
            if (p == null) return;
            string aviso = p.Ventas.Count > 0
                ? "Esta paca tiene ventas registradas; al eliminarla también se borran sus ventas.\n\n"
                : "";
            if (MessageBox.Show(aviso + "¿Eliminar \"" + p.Nombre + "\"?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            pacas.Remove(p);
            Guardar();
            RefrescarTodo();
            LimpiarCompra();
        }

        void LimpiarCompra()
        {
            cNombre.Text = "";
            cCosto.Text = "";
            cFecha.Value = DateTime.Today;
            cTipo.Text = "";
            cCantItem.Value = 1;
            borrador.Clear();
            QuitarSeleccion(gridC);
            RefrescarBorrador();
            cNombre.Focus();
        }

        void CargarCompra()
        {
            if (cargando) return;
            var p = SelPaca(gridC);
            if (p == null) return;
            cNombre.Text = p.Nombre;
            cCosto.Text = p.CostoTotal.ToString("0.##", CultureInfo.InvariantCulture);
            cFecha.Value = p.FechaCompra;
            cTipo.Text = "";
            cCantItem.Value = 1;
            borrador = CopiarItems(p.Items);
            RefrescarBorrador();
        }

        // ---------- VENTAS ----------

        void RefrescarCombo()
        {
            ignorar = true;
            var actual = vPaca.SelectedItem as Paca;
            vPaca.Items.Clear();
            foreach (var p in pacas) vPaca.Items.Add(p);
            if (actual != null && vPaca.Items.Contains(actual))
                vPaca.SelectedItem = actual;
            else if (vPaca.Items.Count > 0)
                vPaca.SelectedIndex = vPaca.Items.Count - 1;
            else
                vPaca.SelectedIndex = -1;
            ignorar = false;
            RefrescarPrendas(vPaca.SelectedItem as Paca, null);
        }

        // Llena la lista de prendas de la paca con lo que queda en stock.
        // "ed" es la venta que se está editando (sus unidades cuentan como disponibles).
        void RefrescarPrendas(Paca p, Venta ed)
        {
            var previo = vPrenda.SelectedItem as ItemStock;
            string tipoPrevio = previo != null ? previo.A.Tipo : null;

            ignorar = true;
            vPrenda.Items.Clear();
            if (p != null)
            {
                foreach (var a in p.Items)
                {
                    int q = p.Disponibles(a);
                    if (ed != null && Igual(ed.Prenda, a.Tipo)) q += ed.Cantidad;
                    if (q > 0) vPrenda.Items.Add(new ItemStock { A = a, Quedan = q });
                }
            }
            ignorar = false;

            ItemStock sel = null;
            if (tipoPrevio != null)
                sel = vPrenda.Items.Cast<ItemStock>().FirstOrDefault(i => Igual(i.A.Tipo, tipoPrevio));
            if (sel != null) vPrenda.SelectedItem = sel;
            else vPrenda.SelectedIndex = -1;
            AjustarMaximo();
        }

        void CambioPaca()
        {
            if (ignorar) return;
            var p = vPaca.SelectedItem as Paca;
            var f = SelFila(gridV);
            Venta ed = (f != null && f.P == p) ? f.V : null;
            RefrescarPrendas(p, ed);
        }

        void AjustarMaximo()
        {
            var it = vPrenda.SelectedItem as ItemStock;
            vCantidad.Maximum = it != null ? Math.Max(1, it.Quedan) : 100000;
        }

        bool LeerPrecio(out decimal precio)
        {
            if (!LeerDecimal(vPrecio.Text, out precio) || precio < 0)
            {
                MessageBox.Show("El precio por unidad no es válido.");
                return false;
            }
            return true;
        }

        void AgregarVenta()
        {
            var p = vPaca.SelectedItem as Paca;
            if (p == null)
            {
                MessageBox.Show("Elige de qué paca salió la prenda. Si no hay ninguna, regístrala primero en Compras.");
                return;
            }
            if (p.Items.Count == 0)
            {
                MessageBox.Show("Esta paca no tiene prendas registradas. Ve a Compras, selecciónala y agrega lo que trae.");
                return;
            }
            var it = vPrenda.SelectedItem as ItemStock;
            if (it == null)
            {
                MessageBox.Show("Elige la prenda. Si no aparece, ya no quedan de ese tipo en esta paca.");
                return;
            }
            int cant = (int)vCantidad.Value;
            int quedan = p.Disponibles(it.A);
            if (cant > quedan)
            {
                MessageBox.Show("Solo quedan " + quedan + " unidades de \"" + it.A.Tipo + "\" en esta paca.");
                return;
            }
            decimal precio;
            if (!LeerPrecio(out precio)) return;

            p.Ventas.Add(new Venta
            {
                Prenda = it.A.Tipo,
                Cantidad = cant,
                PrecioUnitario = precio,
                Fecha = vFecha.Value.Date
            });
            Guardar();
            RefrescarTodo();
            LimpiarVenta();
        }

        void GuardarVenta()
        {
            var f = SelFila(gridV);
            if (f == null)
            {
                MessageBox.Show("Selecciona primero una venta de la lista.");
                return;
            }
            var nueva = vPaca.SelectedItem as Paca;
            if (nueva == null)
            {
                MessageBox.Show("Elige de qué paca salió la prenda.");
                return;
            }
            var it = vPrenda.SelectedItem as ItemStock;
            if (it == null)
            {
                MessageBox.Show("Elige la prenda.");
                return;
            }
            int cant = (int)vCantidad.Value;
            if (cant > it.Quedan)
            {
                MessageBox.Show("Solo hay " + it.Quedan + " unidades disponibles de \"" + it.A.Tipo + "\".");
                return;
            }
            decimal precio;
            if (!LeerPrecio(out precio)) return;

            f.V.Prenda = it.A.Tipo;
            f.V.Cantidad = cant;
            f.V.PrecioUnitario = precio;
            f.V.Fecha = vFecha.Value.Date;
            if (nueva != f.P)
            {
                f.P.Ventas.Remove(f.V);
                nueva.Ventas.Add(f.V);
            }
            Guardar();
            RefrescarTodo();
            SeleccionarVenta(gridV, f.V);
        }

        void EliminarVenta()
        {
            var f = SelFila(gridV);
            if (f == null) return;
            if (MessageBox.Show("¿Eliminar esta venta de " + f.V.Cantidad + " x \"" + f.V.Prenda + "\"?\nLas unidades volverán al stock de la paca.",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            f.P.Ventas.Remove(f.V);
            Guardar();
            RefrescarTodo();
            LimpiarVenta();
        }

        void LimpiarVenta()
        {
            vCantidad.Value = 1;
            vPrecio.Text = "";
            vFecha.Value = DateTime.Today;
            QuitarSeleccion(gridV);
            RefrescarPrendas(vPaca.SelectedItem as Paca, null);
        }

        void CargarVenta()
        {
            if (cargando) return;
            var f = SelFila(gridV);
            if (f == null) return;

            ignorar = true;
            vPaca.SelectedItem = f.P;
            ignorar = false;
            RefrescarPrendas(f.P, f.V);

            var it = vPrenda.Items.Cast<ItemStock>().FirstOrDefault(i => Igual(i.A.Tipo, f.V.Prenda));
            if (it != null) vPrenda.SelectedItem = it;
            else vPrenda.SelectedIndex = -1;

            vCantidad.Value = AjustarCantidad(vCantidad, f.V.Cantidad);
            vPrecio.Text = f.V.PrecioUnitario.ToString("0.##", CultureInfo.InvariantCulture);
            vFecha.Value = f.V.Fecha;
        }

        // ---------- Refrescar pantallas ----------

        void RefrescarTodo()
        {
            cargando = true;

            gridC.Rows.Clear();
            foreach (var p in pacas)
            {
                int i = gridC.Rows.Add(p.Nombre,
                    p.CostoTotal.ToString("N2"),
                    p.FechaCompra.ToShortDateString(),
                    p.UnidadesCompradas,
                    p.UnidadesVendidas,
                    p.EnStock,
                    p.TotalVendido.ToString("N2"),
                    p.Ganancia.ToString("N2"));
                gridC.Rows[i].Tag = p;
                gridC.Rows[i].Cells[7].Style.ForeColor =
                    p.Ganancia < 0 ? Color.Firebrick : Color.ForestGreen;
            }

            gridV.Rows.Clear();
            var filas = pacas
                .SelectMany(p => p.Ventas.Select(v => new Fila { P = p, V = v }))
                .OrderByDescending(f => f.V.Fecha)
                .ToList();
            foreach (var f in filas)
            {
                int i = gridV.Rows.Add(f.P.Nombre, f.V.Prenda, f.V.Cantidad,
                    f.V.PrecioUnitario.ToString("N2"),
                    f.V.Total.ToString("N2"),
                    f.V.Fecha.ToShortDateString());
                gridV.Rows[i].Tag = f;
            }

            QuitarSeleccion(gridC);
            QuitarSeleccion(gridV);

            // Sugerencias de nombres de prendas ya usadas
            var nombres = new AutoCompleteStringCollection();
            nombres.AddRange(pacas.SelectMany(p => p.Items)
                .Select(a => a.Tipo)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
            cTipo.AutoCompleteCustomSource = nombres;

            RefrescarCombo();

            decimal invertido = pacas.Sum(p => p.CostoTotal);
            decimal vendido = filas.Sum(f => f.V.Total);
            decimal vendidoHoy = filas.Where(f => f.V.Fecha.Date == DateTime.Today).Sum(f => f.V.Total);
            int unidades = filas.Sum(f => f.V.Cantidad);
            int enStock = pacas.Sum(p => p.EnStock);

            lblTotC.Text = "Invertido: " + invertido.ToString("N2") +
                "   |   Vendido: " + vendido.ToString("N2") +
                "   |   Ganancia: " + (vendido - invertido).ToString("N2") +
                "   |   Prendas en stock: " + enStock;

            lblTotV.Text = "Total vendido: " + vendido.ToString("N2") +
                "   |   Vendido hoy: " + vendidoHoy.ToString("N2") +
                "   |   Prendas vendidas: " + unidades;

            cargando = false;

            RefrescarBorrador();
        }

        // ---------- Exportar para imprimir ----------

        static string H(string s)
        {
            return System.Net.WebUtility.HtmlEncode(s ?? "");
        }

        static string M(decimal d)
        {
            return d.ToString("N2");
        }

        void ExportarReporte()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var ventas = pacas
                    .SelectMany(p => p.Ventas.Select(v => new Fila { P = p, V = v }))
                    .OrderBy(f => f.V.Fecha)
                    .ThenBy(f => f.P.Nombre)
                    .ToList();

                decimal invertido = pacas.Sum(p => p.CostoTotal);
                decimal vendido = ventas.Sum(f => f.V.Total);
                int unidadesVendidas = ventas.Sum(f => f.V.Cantidad);
                int enStock = pacas.Sum(p => p.EnStock);

                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\">");
                sb.AppendLine("<title>Libro de Ingresos y Gastos</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body{font-family:Arial,Helvetica,sans-serif;font-size:13px;color:#000;margin:24px;}");
                sb.AppendLine("h1{font-size:22px;margin:0 0 4px;}");
                sb.AppendLine("h2{font-size:17px;margin:26px 0 8px;border-bottom:2px solid #000;padding-bottom:3px;}");
                sb.AppendLine("h3{font-size:14px;margin:0 0 4px;}");
                sb.AppendLine("p{margin:3px 0 6px;}");
                sb.AppendLine(".sub{color:#444;margin-bottom:14px;}");
                sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:10px;}");
                sb.AppendLine("th,td{border:1px solid #666;padding:5px 7px;text-align:left;}");
                sb.AppendLine("th{background:#e6e6e6;}");
                sb.AppendLine(".n{text-align:right;}");
                sb.AppendLine(".paca{margin-bottom:18px;page-break-inside:avoid;}");
                sb.AppendLine(".total td{font-weight:bold;background:#f2f2f2;}");
                sb.AppendLine("button{padding:8px 16px;font-size:14px;margin-bottom:14px;cursor:pointer;}");
                sb.AppendLine("@media print{.noprint{display:none;}body{margin:10mm;}}");
                sb.AppendLine("</style></head><body>");

                sb.AppendLine("<button class=\"noprint\" onclick=\"window.print()\">Imprimir / Guardar como PDF</button>");
                sb.AppendLine("<h1>Libro de Ingresos y Gastos - Tienda de Ropa</h1>");
                sb.AppendLine("<div class=\"sub\">Reporte generado el " + H(DateTime.Now.ToString("dd/MM/yyyy HH:mm")) + "</div>");

                // Resumen general
                sb.AppendLine("<h2>Resumen general</h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Total invertido</th><th>Total vendido</th><th>Ganancia</th><th>Prendas vendidas</th><th>Prendas en stock</th></tr>");
                sb.AppendLine("<tr><td class=\"n\">" + M(invertido) + "</td><td class=\"n\">" + M(vendido) + "</td><td class=\"n\">" +
                    M(vendido - invertido) + "</td><td class=\"n\">" + unidadesVendidas + "</td><td class=\"n\">" + enStock + "</td></tr>");
                sb.AppendLine("</table>");

                // Compras (pacas)
                sb.AppendLine("<h2>Compras (pacas)</h2>");
                if (pacas.Count == 0) sb.AppendLine("<p><i>No hay pacas registradas.</i></p>");
                foreach (var p in pacas)
                {
                    sb.AppendLine("<div class=\"paca\">");
                    sb.AppendLine("<h3>" + H(p.Nombre) + " &mdash; comprada el " + H(p.FechaCompra.ToShortDateString()) + "</h3>");
                    sb.AppendLine("<p>Costo total: <b>" + M(p.CostoTotal) + "</b> &nbsp;|&nbsp; Vendido: <b>" + M(p.TotalVendido) +
                        "</b> &nbsp;|&nbsp; Ganancia: <b>" + M(p.Ganancia) + "</b></p>");
                    if (p.Items.Count == 0)
                    {
                        sb.AppendLine("<p><i>Esta paca no tiene prendas registradas.</i></p>");
                    }
                    else
                    {
                        sb.AppendLine("<table>");
                        sb.AppendLine("<tr><th>Prenda</th><th class=\"n\">Cantidad comprada</th><th class=\"n\">Vendidas</th><th class=\"n\">Quedan</th></tr>");
                        foreach (var a in p.Items)
                        {
                            int vend = p.VendidasDe(a.Tipo);
                            sb.AppendLine("<tr><td>" + H(a.Tipo) + "</td><td class=\"n\">" + a.Cantidad + "</td><td class=\"n\">" +
                                vend + "</td><td class=\"n\">" + (a.Cantidad - vend) + "</td></tr>");
                        }
                        sb.AppendLine("<tr class=\"total\"><td>Total</td><td class=\"n\">" + p.UnidadesCompradas + "</td><td class=\"n\">" +
                            p.Items.Sum(a => p.VendidasDe(a.Tipo)) + "</td><td class=\"n\">" + p.EnStock + "</td></tr>");
                        sb.AppendLine("</table>");
                    }
                    sb.AppendLine("</div>");
                }

                // Ventas
                sb.AppendLine("<h2>Ventas</h2>");
                if (ventas.Count == 0)
                {
                    sb.AppendLine("<p><i>No hay ventas registradas.</i></p>");
                }
                else
                {
                    sb.AppendLine("<table>");
                    sb.AppendLine("<tr><th>Fecha</th><th>Paca</th><th>Prenda</th><th class=\"n\">Cantidad</th><th class=\"n\">Precio c/u</th><th class=\"n\">Total</th></tr>");
                    foreach (var f in ventas)
                    {
                        sb.AppendLine("<tr><td>" + H(f.V.Fecha.ToShortDateString()) + "</td><td>" + H(f.P.Nombre) + "</td><td>" +
                            H(f.V.Prenda) + "</td><td class=\"n\">" + f.V.Cantidad + "</td><td class=\"n\">" +
                            M(f.V.PrecioUnitario) + "</td><td class=\"n\">" + M(f.V.Total) + "</td></tr>");
                    }
                    sb.AppendLine("<tr class=\"total\"><td colspan=\"3\">Total vendido</td><td class=\"n\">" + unidadesVendidas +
                        "</td><td></td><td class=\"n\">" + M(vendido) + "</td></tr>");
                    sb.AppendLine("</table>");
                }

                sb.AppendLine("</body></html>");

                string dir = Path.Combine(carpeta, "Reportes");
                Directory.CreateDirectory(dir);
                string ruta = Path.Combine(dir, "Reporte_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".html");
                File.WriteAllText(ruta, sb.ToString(), new System.Text.UTF8Encoding(true));

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                MessageBox.Show("Reporte guardado en:\n" + ruta +
                    "\n\nSe abrió en tu navegador. Pulsa el botón \"Imprimir / Guardar como PDF\" (o Ctrl+P) para sacar la copia en papel.",
                    "Reporte listo");
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo crear el reporte: " + ex.Message);
            }
        }

        // ---------- Guardar / cargar ----------

        void Guardar()
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                File.WriteAllText(archivo, JsonSerializer.Serialize(pacas,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo guardar: " + ex.Message);
            }
        }

        void Cargar()
        {
            try
            {
                if (!File.Exists(archivo)) return;
                pacas = JsonSerializer.Deserialize<List<Paca>>(File.ReadAllText(archivo)) ?? new List<Paca>();

                // Convertir datos de versiones anteriores (prendas con costo por unidad)
                bool convertido = false;
                foreach (var p in pacas)
                {
                    if (p.Ventas == null) p.Ventas = new List<Venta>();
                    if (p.Items == null) p.Items = new List<Articulo>();
                    if (!p.Costo.HasValue) continue;

                    int cant = p.Cantidad ?? 1;
                    if (cant <= 0) cant = 1;
                    if (p.PrecioVenta.HasValue)
                    {
                        p.Ventas.Add(new Venta
                        {
                            Prenda = p.Nombre,
                            Cantidad = cant,
                            PrecioUnitario = p.PrecioVenta.Value,
                            Fecha = p.FechaVenta ?? p.FechaCompra
                        });
                    }
                    foreach (var v in p.Ventas)
                        if (string.IsNullOrWhiteSpace(v.Prenda)) v.Prenda = p.Nombre;

                    p.Items = new List<Articulo> { new Articulo { Tipo = p.Nombre, Cantidad = cant } };
                    p.CostoTotal = p.Costo.Value * cant;
                    p.Costo = null;
                    p.Cantidad = null;
                    p.PrecioVenta = null;
                    p.FechaVenta = null;
                    convertido = true;
                }

                if (convertido)
                {
                    try
                    {
                        string respaldo = Path.Combine(carpeta, "respaldo_antes_de_pacas.json");
                        if (!File.Exists(respaldo)) File.Copy(archivo, respaldo, false);
                    }
                    catch { }
                    Guardar();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo leer el registro: " + ex.Message);
            }
        }
    }
}
