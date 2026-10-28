using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace Libro_Tienda
{
    public class Prenda
    {
        public string Nombre { get; set; } = "";
        public decimal Costo { get; set; }
        public decimal? PrecioVenta { get; set; }
        public DateTime FechaCompra { get; set; } = DateTime.Today;
        public DateTime? FechaVenta { get; set; }

        public override string ToString()
        {
            return Nombre + "  (costo " + Costo.ToString("N2") + ")";
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
        List<Prenda> prendas = new List<Prenda>();
        readonly string carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Libro_Tienda");
        string archivo => Path.Combine(carpeta, "libro_tienda.json");
        bool cargando = false;

        // Navegación
        Panel pageCompras, pageVentas;
        Button btnCompras, btnVentas;

        // Página Compras
        TextBox cNombre, cCosto;
        DateTimePicker cFecha;
        DataGridView gridC;
        Label lblTotC;

        // Página Ventas
        ComboBox vPrenda;
        TextBox vPrecio;
        DateTimePicker vFecha;
        DataGridView gridV;
        Label lblTotV;

        public MainForm()
        {
            Text = "Libro de Ingresos y Gastos - Tienda de Ropa";
            Size = new Size(950, 620);
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

        Button CrearBoton(string texto, int x, int ancho)
        {
            return new Button { Text = texto, Location = new Point(x, 65), Width = ancho, Height = 30 };
        }

        void ConstruirCompras()
        {
            pageCompras = new Panel { Dock = DockStyle.Fill };

            gridC = CrearGrid(new[] { "Prenda", "Costo", "Fecha compra", "Estado" });
            gridC.SelectionChanged += (s, e) => CargarCompra();
            pageCompras.Controls.Add(gridC);

            var top = new Panel { Dock = DockStyle.Top, Height = 110 };
            pageCompras.Controls.Add(top);

            top.Controls.Add(new Label { Text = "Nombre de la prenda", Location = new Point(10, 10), AutoSize = true });
            cNombre = new TextBox { Location = new Point(10, 30), Width = 220 };
            top.Controls.Add(cNombre);

            top.Controls.Add(new Label { Text = "Costo de compra", Location = new Point(245, 10), AutoSize = true });
            cCosto = new TextBox { Location = new Point(245, 30), Width = 100 };
            top.Controls.Add(cCosto);

            top.Controls.Add(new Label { Text = "Fecha de compra", Location = new Point(360, 10), AutoSize = true });
            cFecha = new DateTimePicker { Location = new Point(360, 30), Width = 110, Format = DateTimePickerFormat.Short };
            top.Controls.Add(cFecha);

            var bAgregar = CrearBoton("Agregar", 10, 90);
            bAgregar.Click += (s, e) => AgregarCompra();
            top.Controls.Add(bAgregar);

            var bGuardar = CrearBoton("Guardar cambios", 110, 120);
            bGuardar.Click += (s, e) => GuardarCompra();
            top.Controls.Add(bGuardar);

            var bEliminar = CrearBoton("Eliminar", 240, 90);
            bEliminar.Click += (s, e) => EliminarCompra();
            top.Controls.Add(bEliminar);

            var bLimpiar = CrearBoton("Limpiar", 340, 90);
            bLimpiar.Click += (s, e) => LimpiarCompra();
            top.Controls.Add(bLimpiar);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.WhiteSmoke };
            lblTotC = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0) };
            bottom.Controls.Add(lblTotC);
            pageCompras.Controls.Add(bottom);
        }

        void ConstruirVentas()
        {
            pageVentas = new Panel { Dock = DockStyle.Fill };

            gridV = CrearGrid(new[] { "Prenda", "Costo", "Precio venta", "Fecha venta", "Ganancia" });
            gridV.SelectionChanged += (s, e) => CargarVenta();
            pageVentas.Controls.Add(gridV);

            var top = new Panel { Dock = DockStyle.Top, Height = 110 };
            pageVentas.Controls.Add(top);

            top.Controls.Add(new Label { Text = "Prenda (en stock)", Location = new Point(10, 10), AutoSize = true });
            vPrenda = new ComboBox { Location = new Point(10, 30), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            top.Controls.Add(vPrenda);

            top.Controls.Add(new Label { Text = "Precio de venta", Location = new Point(325, 10), AutoSize = true });
            vPrecio = new TextBox { Location = new Point(325, 30), Width = 100 };
            top.Controls.Add(vPrecio);

            top.Controls.Add(new Label { Text = "Fecha de venta", Location = new Point(440, 10), AutoSize = true });
            vFecha = new DateTimePicker { Location = new Point(440, 30), Width = 110, Format = DateTimePickerFormat.Short };
            top.Controls.Add(vFecha);

            var bAgregar = CrearBoton("Agregar", 10, 90);
            bAgregar.Click += (s, e) => AgregarVenta();
            top.Controls.Add(bAgregar);

            var bGuardar = CrearBoton("Guardar cambios", 110, 120);
            bGuardar.Click += (s, e) => GuardarVenta();
            top.Controls.Add(bGuardar);

            var bEliminar = CrearBoton("Eliminar", 240, 90);
            bEliminar.Click += (s, e) => EliminarVenta();
            top.Controls.Add(bEliminar);

            var bLimpiar = CrearBoton("Limpiar", 340, 90);
            bLimpiar.Click += (s, e) => LimpiarVenta();
            top.Controls.Add(bLimpiar);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.WhiteSmoke };
            lblTotV = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0) };
            bottom.Controls.Add(lblTotV);
            pageVentas.Controls.Add(bottom);
        }

        // ---------- Utilidades ----------

        static bool LeerDecimal(string texto, out decimal valor)
        {
            texto = (texto ?? "").Trim().Replace(',', '.');
            return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
        }

        static Prenda Seleccionada(DataGridView g)
        {
            if (g.SelectedRows.Count == 0) return null;
            return g.SelectedRows[0].Tag as Prenda;
        }

        static void SeleccionarFila(DataGridView g, Prenda p)
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

        static void QuitarSeleccion(DataGridView g)
        {
            g.CurrentCell = null;
            g.ClearSelection();
        }

        // ---------- COMPRAS ----------

        void AgregarCompra()
        {
            if (string.IsNullOrWhiteSpace(cNombre.Text))
            {
                MessageBox.Show("Escribe el nombre de la prenda.");
                return;
            }
            decimal costo;
            if (!LeerDecimal(cCosto.Text, out costo) || costo < 0)
            {
                MessageBox.Show("El costo de compra no es válido.");
                return;
            }
            prendas.Add(new Prenda
            {
                Nombre = cNombre.Text.Trim(),
                Costo = costo,
                FechaCompra = cFecha.Value.Date
            });
            Guardar();
            RefrescarTodo();
            LimpiarCompra();
        }

        void GuardarCompra()
        {
            var p = Seleccionada(gridC);
            if (p == null)
            {
                MessageBox.Show("Selecciona primero una prenda de la lista.");
                return;
            }
            if (string.IsNullOrWhiteSpace(cNombre.Text))
            {
                MessageBox.Show("Escribe el nombre de la prenda.");
                return;
            }
            decimal costo;
            if (!LeerDecimal(cCosto.Text, out costo) || costo < 0)
            {
                MessageBox.Show("El costo de compra no es válido.");
                return;
            }
            p.Nombre = cNombre.Text.Trim();
            p.Costo = costo;
            p.FechaCompra = cFecha.Value.Date;
            Guardar();
            RefrescarTodo();
            SeleccionarFila(gridC, p);
        }

        void EliminarCompra()
        {
            var p = Seleccionada(gridC);
            if (p == null) return;
            string aviso = p.PrecioVenta.HasValue
                ? "Esta prenda ya está vendida; al eliminarla también se borra su venta.\n\n"
                : "";
            if (MessageBox.Show(aviso + "¿Eliminar \"" + p.Nombre + "\"?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            prendas.Remove(p);
            Guardar();
            RefrescarTodo();
            LimpiarCompra();
        }

        void LimpiarCompra()
        {
            cNombre.Text = "";
            cCosto.Text = "";
            cFecha.Value = DateTime.Today;
            QuitarSeleccion(gridC);
            cNombre.Focus();
        }

        void CargarCompra()
        {
            if (cargando) return;
            var p = Seleccionada(gridC);
            if (p == null) return;
            cNombre.Text = p.Nombre;
            cCosto.Text = p.Costo.ToString("0.##", CultureInfo.InvariantCulture);
            cFecha.Value = p.FechaCompra;
        }

        // ---------- VENTAS ----------

        void RefrescarCombo(Prenda incluir)
        {
            vPrenda.Items.Clear();
            foreach (var p in prendas.Where(x => !x.PrecioVenta.HasValue))
                vPrenda.Items.Add(p);
            if (incluir != null && !vPrenda.Items.Contains(incluir))
                vPrenda.Items.Add(incluir);
        }

        void AgregarVenta()
        {
            var p = vPrenda.SelectedItem as Prenda;
            if (p == null)
            {
                MessageBox.Show("Elige una prenda en stock. Si no aparece ninguna, regístrala primero en Compras.");
                return;
            }
            if (p.PrecioVenta.HasValue)
            {
                MessageBox.Show("Esa prenda ya está vendida. Usa \"Guardar cambios\" para modificar la venta.");
                return;
            }
            decimal precio;
            if (!LeerDecimal(vPrecio.Text, out precio) || precio < 0)
            {
                MessageBox.Show("El precio de venta no es válido.");
                return;
            }
            p.PrecioVenta = precio;
            p.FechaVenta = vFecha.Value.Date;
            Guardar();
            RefrescarTodo();
            LimpiarVenta();
        }

        void GuardarVenta()
        {
            var p = Seleccionada(gridV);
            if (p == null)
            {
                MessageBox.Show("Selecciona primero una venta de la lista.");
                return;
            }
            decimal precio;
            if (!LeerDecimal(vPrecio.Text, out precio) || precio < 0)
            {
                MessageBox.Show("El precio de venta no es válido.");
                return;
            }
            p.PrecioVenta = precio;
            p.FechaVenta = vFecha.Value.Date;
            Guardar();
            RefrescarTodo();
            SeleccionarFila(gridV, p);
        }

        void EliminarVenta()
        {
            var p = Seleccionada(gridV);
            if (p == null) return;
            if (MessageBox.Show("¿Anular la venta de \"" + p.Nombre + "\"?\nLa prenda volverá a estar en stock.",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            p.PrecioVenta = null;
            p.FechaVenta = null;
            Guardar();
            RefrescarTodo();
            LimpiarVenta();
        }

        void LimpiarVenta()
        {
            RefrescarCombo(null);
            vPrenda.Enabled = true;
            vPrenda.SelectedIndex = -1;
            vPrecio.Text = "";
            vFecha.Value = DateTime.Today;
            QuitarSeleccion(gridV);
        }

        void CargarVenta()
        {
            if (cargando) return;
            var p = Seleccionada(gridV);
            if (p == null) return;
            RefrescarCombo(p);
            vPrenda.SelectedItem = p;
            vPrenda.Enabled = false;
            vPrecio.Text = p.PrecioVenta.HasValue
                ? p.PrecioVenta.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";
            vFecha.Value = p.FechaVenta ?? DateTime.Today;
        }

        // ---------- Refrescar pantallas ----------

        void RefrescarTodo()
        {
            cargando = true;

            gridC.Rows.Clear();
            foreach (var p in prendas)
            {
                int i = gridC.Rows.Add(p.Nombre, p.Costo.ToString("N2"),
                    p.FechaCompra.ToShortDateString(),
                    p.PrecioVenta.HasValue ? "Vendida" : "En stock");
                gridC.Rows[i].Tag = p;
            }

            gridV.Rows.Clear();
            foreach (var p in prendas.Where(x => x.PrecioVenta.HasValue))
            {
                int i = gridV.Rows.Add(p.Nombre, p.Costo.ToString("N2"),
                    p.PrecioVenta.Value.ToString("N2"),
                    p.FechaVenta.HasValue ? p.FechaVenta.Value.ToShortDateString() : "",
                    (p.PrecioVenta.Value - p.Costo).ToString("N2"));
                gridV.Rows[i].Tag = p;
            }

            QuitarSeleccion(gridC);
            QuitarSeleccion(gridV);
            RefrescarCombo(null);
            vPrenda.Enabled = true;
            vPrenda.SelectedIndex = -1;

            var vendidas = prendas.Where(p => p.PrecioVenta.HasValue).ToList();
            decimal invertido = prendas.Sum(p => p.Costo);
            decimal enStock = prendas.Where(p => !p.PrecioVenta.HasValue).Sum(p => p.Costo);
            decimal vendido = vendidas.Sum(p => p.PrecioVenta.Value);
            decimal ganancia = vendidas.Sum(p => p.PrecioVenta.Value - p.Costo);

            lblTotC.Text = "Total invertido: " + invertido.ToString("N2") +
                "   |   En stock (sin vender): " + enStock.ToString("N2") +
                "   |   Prendas: " + prendas.Count;

            lblTotV.Text = "Total vendido: " + vendido.ToString("N2") +
                "   |   Ganancia: " + ganancia.ToString("N2") +
                "   |   Prendas vendidas: " + vendidas.Count;

            cargando = false;
        }

        // ---------- Guardar / cargar ----------

        void Guardar()
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                File.WriteAllText(archivo, JsonSerializer.Serialize(prendas,
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
                if (File.Exists(archivo))
                    prendas = JsonSerializer.Deserialize<List<Prenda>>(File.ReadAllText(archivo)) ?? new List<Prenda>();
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo leer el registro: " + ex.Message);
            }
        }
    }
}
