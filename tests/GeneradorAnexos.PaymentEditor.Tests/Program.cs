using System.Collections;
using System.Reflection;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Serialization;
using GeneradorAnexos.WinUI.Controls;
using Microsoft.UI.Xaml.Controls;

int checks = 0;
void Check(string titulo, bool ok) { if (!ok) throw new InvalidOperationException(titulo); checks++; Console.WriteLine("OK " + titulo); }
object Campo(object o, string nombre) => o.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
object Fila(object tabla, int i) => ((IList)Campo(tabla, "_filas"))[i]!;
TextBox Condicion(TablaPagos t, int i) => (TextBox)Fila(t,i).GetType().GetProperty("EditorCondicion")!.GetValue(Fila(t,i))!;
var tdr = new TablaPagos { MostrarEliminar = true };
var anexos = new TablaPagos { MostrarEliminar = true };
bool sincronizando = false;
int notificaciones = 0;
void Reflejar(TablaPagos origen, TablaPagos destino)
{
    if (sincronizando) return;
    sincronizando = true;
    try { destino.Importar(origen.Exportar()); }
    finally { sincronizando = false; }
}
tdr.TotalCambiado += (_, _) => { notificaciones++; Reflejar(tdr, anexos); };
anexos.TotalCambiado += (_, _) => Reflejar(anexos, tdr);
tdr.EstablecerCantidad(2);
Check("Dos pagos reflejados con total 100", anexos.Cantidad == 2 && anexos.Total == 100);
Check("Alta publica una sola notificación completa", notificaciones == 1);
Condicion(tdr,0).Text = "Conformidad de Logística";
Check("Editar condición TDR actualiza Anexo sin cambiar porcentajes", anexos.Exportar()[0]!.Condicion == "Conformidad de Logística" && anexos.Total == 100);
Condicion(anexos,1).Text = "Informe final del proveedor";
Check("Editar condición Anexo actualiza TDR", tdr.Exportar()[1]!.Condicion == "Informe final del proveedor");
var caja = Condicion(anexos,1);
anexos.Importar(tdr.Exportar());
Check("Reflejar los mismos datos conserva el editor activo", ReferenceEquals(caja, Condicion(anexos,1)));
anexos.Importar([new() { Condicion="A", Porcentaje=40 },new() { Condicion="B", Porcentaje=60 }]);
Check("Porcentajes personalizados se reflejan completos", tdr.Exportar()[0]!.Porcentaje == 40 && tdr.Exportar()[1]!.Porcentaje == 60);
notificaciones = 0;
tdr.EstablecerCantidad(3);
Check("Tercer pago redistribuye 100 y conserva condiciones", anexos.Total == 100 && anexos.Cantidad == 3 && anexos.Exportar()[1]!.Condicion == "B");
Check("No se propagan totales parciales durante redistribución", notificaciones == 1);
tdr.Eliminar(1);
Check("Eliminar pago central elimina la misma condición en Anexo", anexos.Cantidad == 2 && anexos.Total == 100 && anexos.Exportar()[0]!.Condicion == "A" && anexos.Exportar().All(p=>p!.Condicion != "B"));
var datos = PayloadJson.Deserialize(PayloadJson.Serialize(new BorradorPayloadV1 { Tdr = new TdrPayload { Modo="multiple", Pagos=tdr.Exportar() } }));
tdr.Limpiar();
Check("Limpiar no deja pagos en Anexo", anexos.Cantidad == 0);
tdr.Importar(datos.Tdr!.Pagos);
Check("Guardar y cargar recupera pagos en ambas tablas", anexos.Cantidad == 2 && anexos.Total == 100 && anexos.Exportar()[0]!.Condicion == "A");
var entregables = new TablaEntregables();
entregables.Importar([new() { Descripcion="Primero" },new() { Descripcion="Segundo" },new() { Descripcion="Tercero" }]);
var primera = Fila(entregables,0);
var boton = (Button)Campo(primera,"_botonEliminar");
for (int i=0;i<4;i++) entregables.Cargar();
int bajas = 0;
entregables.FilaEliminada += (_,i)=> { bajas++; Check("Índice eliminado válido",i==0); };
boton.Pulsar();
Check("Un clic tras varias reconstrucciones elimina solo una fila", bajas==1 && entregables.Cantidad==2);
Console.WriteLine($"TOTAL: {checks} comprobaciones del editor correctas.");
