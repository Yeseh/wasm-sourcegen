using System.Text;
using Wasmtime;

Func<int, int>? wasmMalloc = null;

// See https://aka.ms/new-console-template for more information
var modulePath = "C:/Users/JesseWellenberg/repo/wasi-sourcegen/SourceGenExample/bin/Debug/net7.0/SourceGenExample.wasm";
var engineConfig = new Config().WithReferenceTypes(true);
var engine = new Engine(engineConfig);
var module = Module.FromFile(engine,modulePath);

var wasi = new WasiConfiguration()
    .WithInheritedStandardInput()
    .WithInheritedStandardOutput()
    .WithInheritedStandardError();

var store = new Store(engine, module );
store.SetWasiConfiguration(wasi);

var linker = new Linker(engine);
linker.DefineWasi();

linker.DefineFunction("env", "hello", () =>
{
    Console.WriteLine("Hello from the Dotnet Host!");
});

var instance = linker.Instantiate(store, module);
var start = instance.GetFunction("_start");
var helloFrom = instance.GetFunction("hello");
var stringParam = instance.GetFunction<int, int>("string_param");

var mem = instance.GetMemory("memory");
var name = CreateWasmString(instance, "Jesse");

start.Invoke();
helloFrom.Invoke();
stringParam.Invoke(name);

int CreateWasmString(Instance instance, string value, int offset = 0)
{
    if (wasmMalloc == null)
    {
        wasmMalloc = instance.GetFunction<int, int>("malloc");
    }

    var len = value.Length;
    var start = wasmMalloc.Invoke(len);
    var ptr = mem.WriteString(start, value);
    return start;
}
