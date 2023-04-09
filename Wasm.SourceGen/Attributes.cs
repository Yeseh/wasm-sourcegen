namespace Wasm.SourceGen;

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class ExportAttribute : Attribute
{
    public string Function { get;  }

    public ExportAttribute(string functionName)
    {
        this.Function = functionName;
        
        if (string.IsNullOrWhiteSpace(this.Function))
        {
            throw new ArgumentException("Function name cannot be an empty string");
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class ImportAttribute : Attribute
{
    public string Module { get; }

    public string Function { get;  }

    public ImportAttribute(string module, string functionName)
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
