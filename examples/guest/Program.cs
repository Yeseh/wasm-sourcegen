using System.Runtime.CompilerServices;
using Wasm.SourceGen;

namespace guest;

partial class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello from _start!");
    }

    [WasmExport("hello")]
    public static int HelloFrom()
    {
        Console.WriteLine("Hello from WASI");
        return 1;
    }
    
    [WasmExport("string_param")]
    public static int StringParam(string name)
    {
        Console.WriteLine($"Hello {name}, from WASI");
        return 1;
    }
    // TODO: Test this
    [WasmExport("array_param")]
    public static int ArrayParam(string name, int[] nrs)
    {
        Console.WriteLine($"Counted: {nrs.Sum()}");
        return 1;
    }

    // TODO: Double check generation of dotnet_target_instance and import decl
    [WasmExport("object_import_param")]
    public static int ObjectImportParam()
    {
        var klass = new OtherClass();
        Interop.MakeOtherClass(klass);
        return 1;
    }
}

public class OtherClass
{
    private const string Name = "Secret";

    [WasmExport("class_hello")]
    public void Hello() 
    {
        Console.WriteLine("Hello from a class instance");
    }

    [WasmExport("this_context")]
    public void ThisContext(string name)
    {
        Console.WriteLine($"Hello, {Name} from {name}");
    }
}

public static class Interop
{

    [MethodImpl(MethodImplOptions.InternalCall)]
    [WasmImport("env", "hello")]
    public static extern void Hello();

    [MethodImpl(MethodImplOptions.InternalCall)]
    [WasmImport("env", "make_other_class")]
    public static extern void MakeOtherClass(OtherClass klass);
}
