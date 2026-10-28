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

        TextBox txtNombre, txtCosto, txtVenta;
        DateTimePicker dtpCompra, dtpVenta;
        CheckBox chkVendida;
        DataGridView grid;
        Label lblTotales;

        public MainForm()
        {
            Text = "Libro de Ingresos y Gastos - Tienda de Ropa";
            Size = new Size(950, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Cuadrícula (se añade primero para que ocupe el espacio sobrante)
            grid = new DataGridView
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
            string[] cols = { "Prenda", "Costo", "Fecha compra", "Precio venta", "Fecha venta", "Ganancia", "Estado" };
            foreach (var c in cols)
            {
                int i = grid.Columns.Add(c, c);
                grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            grid.SelectionChanged += (s, e) => CargarSeleccion();
            Controls.Add(grid);

            // Panel superior (formulario)
            var top = new Panel { Dock = DockStyle.Top, Height = 110 };
            Controls.Add(top);

            top.Controls.Add(new Label { Text = "Nombre de la prenda", Location = new Point(10, 10), AutoSize = true });
            txtNombre = new TextBox { Location = new Point(10, 30), Width = 220 };
            top.Controls.Add(txtNombre);

            top.Controls.Add(new Label { Text = "Costo de compra", Location = new Point(245, 10), AutoSize = true });
            txtCosto = new TextBox { Location = new Point(245, 30), Width = 100 };
            top.Controls.Add(txtCosto);

            top.Controls.Add(new Label { Text = "Fecha de compra", Location = new Point(360, 10), AutoSize = true });
            dtpCompra = new DateTimePicker { Location = new Point(360, 30), Width = 110, Format = DateTimePickerFormat.Short };
            top.Controls.Add(dtpCompra);

            chkVendida = new CheckBox { Text = "Ya se vendió", Location = new Point(485, 32), AutoSize = true };
            chkVendida.CheckedChanged += (s, e) => ActualizarVenta();
            top.Controls.Add(chkVendida);

            top.Controls.Add(new Label { Text = "Precio de venta", Location = new Point(600, 10), AutoSize = true });
            txtVenta = new TextBox { Location = new Point(600, 30), Width = 100 };
            top.Controls.Add(txtVenta);

            top.Controls.Add(new Label { Text = "Fecha de venta", Location = new Point(715, 10), AutoSize = true });
            dtpVenta = new DateTimePicker { Location = new Point(715, 30), Width = 110, Format = DateTimePickerFormat.Short };
            top.Controls.Add(dtpVenta);

            var btnAgregar = new Button { Text = "Agregar", Location = new Point(10, 65), Width = 90, Height = 30 };
            btnAgregar.Click += (s, e) => Agregar();
            top.Controls.Add(btnAgregar);

            var btnGuardar = new Button { Text = "Guardar cambios", Location = new Point(110, 65), Width = 120, Height = 30 };
            btnGuardar.Click += (s, e) => GuardarCambios();
            top.Controls.Add(btnGuardar);

            var btnEliminar = new Button { Text = "Eliminar", Location = new Point(240, 65), Width = 90, Height = 30 };
            btnEliminar.Click += (s, e) => Eliminar();
            top.Controls.Add(btnEliminar);

            var btnLimpiar = new Button { Text = "Limpiar", Location = new Point(340, 65), Width = 90, Height = 30 };
            btnLimpiar.Click += (s, e) => LimpiarCampos();
            top.Controls.Add(btnLimpiar);

            // Panel inferior (totales)
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.WhiteSmoke };
            lblTotales = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0) };
            bottom.Controls.Add(lblTotales);
            Controls.Add(bottom);

            ActualizarVenta();
            Cargar();
            Refrescar();
        }

        void ActualizarVenta()
        {
            txtVenta.Enabled = chkVendida.Checked;
            dtpVenta.Enabled = chkVendida.Checked;
            if (!chkVendida.Checked) txtVenta.Text = "";
        }

        static bool LeerDecimal(string texto, out decimal valor)
        {
            texto = (texto ?? "").Trim().Replace(',', '.');
            return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
        }

        Prenda LeerFormulario()
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MessageBox.Show("Escribe el nombre de la prenda.");
                return null;
            }
            if (!LeerDecimal(txtCosto.Text, out decimal costo) || costo < 0)
            {
                MessageBox.Show("El costo de compra no es válido.");
                return null;
            }
            var p = new Prenda
            {
                Nombre = txtNombre.Text.Trim(),
                Costo = costo,
                FechaCompra = dtpCompra.Value.Date
            };
            if (chkVendida.Checked)
            {
                if (!LeerDecimal(txtVenta.Text, out decimal venta) || venta < 0)
                {
                    MessageBox.Show("El precio de venta no es válido.");
                    return null;
                }
                p.PrecioVenta = venta;
                p.FechaVenta = dtpVenta.Value.Date;
            }
            return p;
        }

        void Agregar()
        {
            var p = LeerFormulario();
            if (p == null) return;
            prendas.Add(p);
            Guardar();
            Refrescar();
            LimpiarCampos();
        }

        void GuardarCambios()
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Index >= prendas.Count)
            {
                MessageBox.Show("Selecciona primero una prenda de la lista.");
                return;
            }
            var p = LeerFormulario();
            if (p == null) return;
            int idx = grid.CurrentRow.Index;
            prendas[idx] = p;
            Guardar();
            Refrescar();
            if (idx < grid.Rows.Count) grid.CurrentCell = grid.Rows[idx].Cells[0];
        }

        void Eliminar()
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Index >= prendas.Count) return;
            var p = prendas[grid.CurrentRow.Index];
            if (MessageBox.Show("¿Eliminar \"" + p.Nombre + "\"?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            prendas.RemoveAt(grid.CurrentRow.Index);
            Guardar();
            Refrescar();
            LimpiarCampos();
        }

        void LimpiarCampos()
        {
            txtNombre.Text = "";
            txtCosto.Text = "";
            txtVenta.Text = "";
            chkVendida.Checked = false;
            dtpCompra.Value = DateTime.Today;
            dtpVenta.Value = DateTime.Today;
            grid.ClearSelection();
            txtNombre.Focus();
        }

        void CargarSeleccion()
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Index >= prendas.Count) return;
            var p = prendas[grid.CurrentRow.Index];
            txtNombre.Text = p.Nombre;
            txtCosto.Text = p.Costo.ToString("0.##", CultureInfo.InvariantCulture);
            dtpCompra.Value = p.FechaCompra;
            chkVendida.Checked = p.PrecioVenta.HasValue;
            ActualizarVenta();
            if (p.PrecioVenta.HasValue)
            {
                txtVenta.Text = p.PrecioVenta.Value.ToString("0.##", CultureInfo.InvariantCulture);
                dtpVenta.Value = p.FechaVenta ?? DateTime.Today;
            }
        }

        void Refrescar()
        {
            grid.Rows.Clear();
            foreach (var p in prendas)
            {
                string venta = p.PrecioVenta.HasValue ? p.PrecioVenta.Value.ToString("N2") : "";
                string fv = p.FechaVenta.HasValue ? p.FechaVenta.Value.ToShortDateString() : "";
                string gan = p.PrecioVenta.HasValue ? (p.PrecioVenta.Value - p.Costo).ToString("N2") : "";
                string estado = p.PrecioVenta.HasValue ? "Vendida" : "En stock";
                grid.Rows.Add(p.Nombre, p.Costo.ToString("N2"), p.FechaCompra.ToShortDateString(), venta, fv, gan, estado);
            }

            decimal invertido = prendas.Sum(p => p.Costo);
            var vendidas = prendas.Where(p => p.PrecioVenta.HasValue).ToList();
            decimal vendido = vendidas.Sum(p => p.PrecioVenta.Value);
            decimal ganancia = vendidas.Sum(p => p.PrecioVenta.Value - p.Costo);
            decimal enStock = prendas.Where(p => !p.PrecioVenta.HasValue).Sum(p => p.Costo);

            lblTotales.Text = "Invertido: " + invertido.ToString("N2") +
                "   |   Vendido: " + vendido.ToString("N2") +
                "   |   Ganancia: " + ganancia.ToString("N2") +
                "   |   En stock (sin vender): " + enStock.ToString("N2");
        }

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
