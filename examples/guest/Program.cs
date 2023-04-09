using System.Runtime.CompilerServices;
using Wasm.SourceGen;

namespace guest;

partial class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello from _start!");
    }

    [Export("hello")]
    public static int HelloFrom()
    {
        Console.WriteLine("Hello from WASI");
        return 1;
    }
    
    [Export("string_param")]
    public static int StringParam(string name)
    {
        Console.WriteLine($"Hello {name}, from WASI");
        return 1;
    }
}

public static class Interop
{

    [MethodImpl(MethodImplOptions.InternalCall)]
    [Import("env", "hello")]
    public static extern void Hello();
}
