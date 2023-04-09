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

    [WasmExport("array_param")]
    public static int ArrayParam(string name, int[] nrs)
    {
        Console.WriteLine($"Counted: {nrs.Sum()}");
        return 1;
    }

    [WasmExport("object_param")]
    public static int ObjectParam(OtherClass klass)
    {
        klass.OtherHello();
        return 1;
    }
}

public class OtherClass
{
    private const string Name = "Secret";

    public void OtherHello() 
    {
        Console.WriteLine("Hello from other class");
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
}
