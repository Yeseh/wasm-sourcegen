namespace Wasm.SourceGen;

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class WasmExportAttribute : Attribute
{
    public string Function { get;  }

    public WasmExportAttribute(string functionName)
    {
        this.Function = functionName;
        
        if (string.IsNullOrWhiteSpace(this.Function))
        {
            throw new ArgumentException("Function name cannot be an empty string");
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class WasmImportAttribute : Attribute
{
    public string Module { get; }

    public string Function { get;  }

    public WasmImportAttribute(string module, string functionName)
    {
        this.Module = module;
        this.Function = functionName;

        if (string.IsNullOrWhiteSpace(this.Module))
        {
            throw new ArgumentException("Module name cannot be an empty string");
        }
        
        if (string.IsNullOrWhiteSpace(this.Function))
        {
            throw new ArgumentException("Function name cannot be an empty string");
        }
    }
}
