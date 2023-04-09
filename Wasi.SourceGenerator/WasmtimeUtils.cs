using Wasmtime;

namespace Wasi.SourceGenerator;

public static class WasmtimeUtils 
{
    public static int CreateWasmString(Instance instance, string value)
    {
        var mem = instance.GetMemory("memory");
        var wasmMalloc = instance.GetFunction<int, int>("malloc");
        if (wasmMalloc == null || mem == null) 
        { throw new Exception("Missing 'malloc' and/or 'memory' export in instance."); }

        var len = value.Length;
        var start = wasmMalloc.Invoke(len);
        var ptr = mem.WriteString(start, value);

        return start;
    }
}
