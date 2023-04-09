using Wasmtime;

using static Wasm.SourceGen.WasmtimeUtils;

// See https://aka.ms/new-console-template for more information

// Only works from workspace :)
var modulePath = "/workspaces/wasm-sourcegen/examples/guest/bin/Debug/net7.0/guest.wasm";
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

linker.DefineFunction("env", "make_other_class", (Caller caller, int classPtr) => {
    var wrapper = new GuestWrapper(caller);
    wrapper.Hello(classPtr);
});

var instance = linker.Instantiate(store, module);
var start = instance.GetFunction("_start");
var helloFrom = instance.GetFunction("hello");
var objectImportParam = instance.GetFunction("object_import_param");

var stringParam = instance.GetFunction<int, int>("string_param");
var name = CreateWasmString(instance, "Neighbour");

start.Invoke();
helloFrom.Invoke();
stringParam.Invoke(name);
objectImportParam.Invoke();

public class GuestWrapper
{
    private readonly Function? class_hello;
    private readonly Function? malloc;
    private readonly Memory mem;

    public GuestWrapper(Caller inst)
    {
        class_hello = inst.GetFunction("class_hello");
        malloc = inst.GetFunction("malloc");
        mem = inst.GetMemory("memory");
    }

    public void Hello(int ptr)
    {
        class_hello.Invoke(ptr);
    }
}
